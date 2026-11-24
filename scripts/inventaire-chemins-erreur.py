"""Inventaire des chemins d'erreur du code de production (audit 1.3.0, docs/audit-chemins-erreur.md).

Deux listes, reproductibles à l'identique :

1. chaque `catch` du code de production (`src/`, hors projets de tests et `src/TestSupport`) ;
2. chaque appel d'une fonction Windows (P/Invoke `DllImport`/`LibraryImport`, membres de
   `IWin32Api`, méthodes COM `[PreserveSig]`) qui rend une valeur, quand cette valeur n'est pas lue :
   appel en instruction seule, `_ = Appel(...)`, ou corps de lambda / membre `=>` (à relire).

Les commentaires et les chaînes sont neutralisés avant l'analyse (positions conservées).

Usage : python scripts/inventaire-chemins-erreur.py [--racine .] [--format md|tsv]
"""
from __future__ import annotations

import argparse
import re
import subprocess
import sys
from dataclasses import dataclass
from pathlib import Path

DOSSIERS_EXCLUS = ("Tests", "TestSupport", "obj", "bin")


def fichiers_production(src: Path) -> list[Path]:
    resultat = []
    for chemin in sorted(src.rglob("*.cs")):
        parties = chemin.relative_to(src).parts[:-1]
        if any(p.endswith(DOSSIERS_EXCLUS) or p in DOSSIERS_EXCLUS for p in parties):
            continue
        resultat.append(chemin)
    return resultat


def fichiers_revision(racine: Path, rev: str) -> list[Path]:
    """Même sélection que fichiers_production, lue dans une révision git (ordre identique)."""
    noms = subprocess.run(["git", "-C", str(racine), "ls-tree", "-r", "--name-only", rev, "--", "src"],
                          check=True, capture_output=True, text=True, encoding="utf-8").stdout.split("\n")
    chemins = sorted(racine / n for n in noms if n.endswith(".cs"))
    src = racine / "src"
    return [c for c in chemins
            if not any(p.endswith(DOSSIERS_EXCLUS) or p in DOSSIERS_EXCLUS
                       for p in c.relative_to(src).parts[:-1])]


def lire_revision(racine: Path, rev: str, chemin: Path) -> str:
    nom = chemin.relative_to(racine).as_posix()
    brut = subprocess.run(["git", "-C", str(racine), "show", f"{rev}:{nom}"],
                          check=True, capture_output=True).stdout
    # Path.read_text lit en mode texte : mêmes fins de ligne ici, pour des positions identiques.
    return brut.decode("utf-8-sig").replace("\r\n", "\n").replace("\r", "\n")


def neutraliser(texte: str) -> str:
    """Remplace commentaires et littéraux (chaînes, caractères) par des espaces, sauts de ligne gardés."""
    sortie = list(texte)
    i, n = 0, len(texte)

    def blanc(debut: int, fin: int) -> None:
        for k in range(debut, min(fin, n)):
            if sortie[k] != "\n":
                sortie[k] = " "

    while i < n:
        c = texte[i]
        if texte.startswith("//", i):
            fin = texte.find("\n", i)
            fin = n if fin < 0 else fin
            blanc(i, fin)
            i = fin
        elif texte.startswith("/*", i):
            fin = texte.find("*/", i + 2)
            fin = n if fin < 0 else fin + 2
            blanc(i, fin)
            i = fin
        elif texte.startswith('"""', i) or re.match(r'\$+"""', texte[i:i + 6] or ""):
            debut = i
            while texte[i] == "$":
                i += 1
            fin = texte.find('"""', i + 3)
            fin = n if fin < 0 else fin + 3
            blanc(debut, fin)
            i = fin
        elif c == '"' or (c in "@$" and i + 1 < n and texte[i + 1] in '"@$'):
            debut = i
            verbatim = False
            while texte[i] in "@$":
                verbatim = verbatim or texte[i] == "@"
                i += 1
            if i >= n or texte[i] != '"':
                i = debut + 1
                continue
            i += 1
            while i < n:
                if verbatim and texte.startswith('""', i):
                    i += 2
                    continue
                if not verbatim and texte[i] == "\\":
                    i += 2
                    continue
                if texte[i] == '"':
                    i += 1
                    break
                i += 1
            blanc(debut, i)
        elif c == "'":
            m = re.match(r"'(\\.[^']*|[^'\\])'", texte[i:i + 12])
            if m:
                blanc(i, i + m.end())
                i += m.end()
            else:
                i += 1
        else:
            i += 1
    return "".join(sortie)


def ligne_de(texte: str, pos: int) -> int:
    return texte.count("\n", 0, pos) + 1


def fermante(texte: str, pos_ouvrante: int, ouv: str = "(", ferm: str = ")") -> int:
    profondeur = 0
    for k in range(pos_ouvrante, len(texte)):
        if texte[k] == ouv:
            profondeur += 1
        elif texte[k] == ferm:
            profondeur -= 1
            if profondeur == 0:
                return k
    return len(texte) - 1


@dataclass
class Catch:
    fichier: str
    ligne: int
    clause: str
    corps: str


@dataclass
class Fonction:
    nom: str
    retour: str
    origine: str
    set_last_error: bool


@dataclass
class Appel:
    fichier: str
    ligne: int
    fonction: str
    retour: str
    forme: str
    texte: str


def catches(chemin: Path, brut: str, net: str, racine: Path) -> list[Catch]:
    resultat = []
    for m in re.finditer(r"\bcatch\b", net):
        k = m.end()
        clause = ""
        while k < len(net) and net[k].isspace():
            k += 1
        if k < len(net) and net[k] == "(":
            f = fermante(net, k)
            clause = brut[k:f + 1]
            k = f + 1
        reste = net[k:]
        filtre = re.match(r"\s*when\s*\(", reste)
        if filtre:
            debut = k + filtre.end() - 1
            f = fermante(net, debut)
            clause += " when " + brut[debut:f + 1]
            k = f + 1
        accolade = net.find("{", k)
        fin = fermante(net, accolade, "{", "}")
        corps = " ".join(brut[accolade + 1:fin].split())
        resultat.append(Catch(chemin.relative_to(racine).as_posix(), ligne_de(net, m.start()),
                              " ".join(clause.split()), corps[:160]))
    return resultat


RE_PINVOKE = re.compile(
    r"\[(?:DllImport|LibraryImport)\s*\((?P<attr>[^\]]*)\)\s*\]"
    r"(?P<entre>(?:\s*\[[^\]]*\])*)\s*"
    r"(?:(?:public|internal|private|static|extern|partial|unsafe|new)\s+)+"
    r"(?P<retour>[\w.<>\[\]?]+)\s+(?P<nom>\w+)\s*\(")
RE_PRESERVESIG = re.compile(
    r"\[PreserveSig\]\s*(?:\[[^\]]*\]\s*)*(?P<retour>[\w.<>\[\]?]+)\s+(?P<nom>\w+)\s*\(")
RE_INTERFACE = re.compile(r"\binterface\s+IWin32Api\b")


def fonctions_windows(fichiers: list[tuple[Path, str]]) -> dict[str, Fonction]:
    connues: dict[str, Fonction] = {}
    for chemin, net in fichiers:
        for m in RE_PINVOKE.finditer(net):
            if m.group("retour") == "void":
                continue
            # LibraryImport/DllImport : SetLastError dans le texte brut de l'attribut (neutralisé ici).
            connues.setdefault(m.group("nom"), Fonction(m.group("nom"), m.group("retour"),
                                                       chemin.name, "SetLastError" in m.group("attr")))
        for m in RE_PRESERVESIG.finditer(net):
            if m.group("retour") != "void":
                connues.setdefault(m.group("nom"), Fonction(m.group("nom"), m.group("retour"),
                                                           chemin.name + " (COM)", False))
        i = RE_INTERFACE.search(net)
        if i:
            debut = net.find("{", i.end())
            corps = net[debut + 1:fermante(net, debut, "{", "}")]
            for m in re.finditer(r"^\s*(?P<retour>[\w.<>\[\]?]+)\s+(?P<nom>[A-Z]\w+)\s*\(", corps, re.M):
                if m.group("retour") not in ("void", "return"):
                    connues.setdefault(m.group("nom"), Fonction(m.group("nom"), m.group("retour"),
                                                               "IWin32Api", False))
    return connues


def appels_ignores(chemin: Path, brut: str, net: str, connues: dict[str, Fonction],
                   racine: Path) -> list[Appel]:
    if not connues:
        return []
    noms = "|".join(sorted(map(re.escape, connues), key=len, reverse=True))
    resultat = []
    for m in re.finditer(rf"(?<![\w])(?P<nom>{noms})\s*\(", net):
        nom = m.group("nom")
        debut = m.start()
        # Remonter le qualificatif (Win32., _api?., NativeMethods., this.).
        k = debut
        while k > 0 and (net[k - 1].isalnum() or net[k - 1] in "_.?"):
            k -= 1
        avant = net[:k].rstrip()
        # Exclure la déclaration elle-même (type de retour juste avant le nom).
        if re.search(r"(?:extern|partial|public|internal|private|static)\s+[\w.<>\[\]?]+\s*$", avant) \
                or re.search(r"\b(?:bool|int|uint|short|ushort|long|nint|nuint|IntPtr|UIntPtr)\s*$", avant):
            continue
        ouvrante = net.find("(", m.end() - 1)
        f = fermante(net, ouvrante)
        apres = net[f + 1:].lstrip()
        forme = None
        if apres.startswith(";"):
            if re.search(r"\b_\s*=\s*$", avant):
                forme = "rejet explicite (_ =)"
            elif re.search(r"=>\s*$", avant):
                # Membre `=>` : son retour est celui de l'appel, sauf membre void ; lambda : à relire.
                borne = max(avant.rfind(";"), avant.rfind("{"), avant.rfind("}"))
                entete = avant[borne + 1:-2]
                if re.search(r"\bvoid\s+\w+\s*(?:<[^>]*>)?\s*\(", entete):
                    forme = "membre void =>"
                elif re.search(r"(?:[=(,]|return)\s*(?:\([^()]*\)|\w+)\s*$", entete):
                    forme = "lambda =>"
            elif avant.endswith((";", "{", "}", ")")) or re.search(r"\belse$", avant):
                forme = "instruction seule"
        elif apres.startswith((")", ",")) and re.search(r"=>\s*$", avant):
            forme = "lambda =>"
        if forme is None:
            continue
        ligne = ligne_de(net, debut)
        texte = " ".join(brut[k:f + 1].split())
        resultat.append(Appel(chemin.relative_to(racine).as_posix(), ligne, nom,
                              connues[nom].retour, forme, texte[:140]))
    return resultat


def main() -> int:
    parseur = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parseur.add_argument("--racine", default=str(Path(__file__).resolve().parents[1]))
    parseur.add_argument("--format", choices=("md", "tsv"), default="md")
    parseur.add_argument("--rev", help="révision git à lire au lieu des fichiers du disque "
                         "(les identifiants K et W du document sont ceux de 12b6674)")
    args = parseur.parse_args()
    racine = Path(args.racine)
    src = racine / "src"
    fichiers = []
    for chemin in fichiers_production(src) if not args.rev else fichiers_revision(racine, args.rev):
        brut = (lire_revision(racine, args.rev, chemin) if args.rev
                else chemin.read_text(encoding="utf-8-sig"))
        fichiers.append((chemin, brut, neutraliser(brut)))
    connues = fonctions_windows([(c, n) for c, _, n in fichiers])
    liste_catch = [c for chemin, brut, net in fichiers for c in catches(chemin, brut, net, racine)]
    liste_appels = [a for chemin, brut, net in fichiers
                    for a in appels_ignores(chemin, brut, net, connues, racine)]
    out = sys.stdout
    if args.format == "tsv":
        for c in liste_catch:
            out.write(f"catch\t{c.fichier}:{c.ligne}\t{c.clause}\t{c.corps}\n")
        for a in liste_appels:
            out.write(f"appel\t{a.fichier}:{a.ligne}\t{a.fonction}\t{a.retour}\t{a.forme}\t{a.texte}\n")
        return 0
    out.write(f"Fichiers de production : {len(fichiers)}\n")
    out.write(f"Fonctions Windows qui rendent une valeur : {len(connues)}\n")
    out.write(f"catch : {len(liste_catch)}\n")
    out.write(f"Appels Windows au retour non lu : {len(liste_appels)}\n\n")
    out.write("| # | Emplacement | Clause | Corps |\n|---|---|---|---|\n")
    for n, c in enumerate(liste_catch, 1):
        out.write(f"| K{n} | `{c.fichier}:{c.ligne}` | `{c.clause or '(tout)'}` | {c.corps or '(vide)'} |\n")
    out.write("\n| # | Emplacement | Fonction | Retour | Forme | Appel |\n|---|---|---|---|---|---|\n")
    for n, a in enumerate(liste_appels, 1):
        out.write(f"| W{n} | `{a.fichier}:{a.ligne}` | {a.fonction} | {a.retour} | {a.forme} | `{a.texte}` |\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
