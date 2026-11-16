"""Inventaire des créations de ressources de src/ (audit des ressources, 16/11/2026).

Recense, hors projets de tests, chaque appel qui crée une minuterie, un abonnement, un
objet Windows ou de la mémoire native, et cherche sa libération : même variable libérée plus
bas dans la même méthode, champ libéré ailleurs dans le fichier, fenêtre enfant ou minuterie
de fenêtre détruites avec leur fenêtre, partenaire nommé (KillTimer, -=, Unhook…). Les sites
qu'aucune règle ne classe prennent leur verdict lu à la main dans
docs/audit-ressources-verdicts.tsv ; docs/audit-ressources.md en tire les constats.

Usage : python scripts/audit-ressources-inventaire.py [--tsv sortie.tsv] [--md docs/audit-ressources-inventaire.md] [--check]
--check échoue (code 1) si un site n'a ni règle ni verdict, ou si un verdict lu ne
correspond plus à aucun site. verify.ps1 le lance (étape « ressources »).
Un verdict lu s'attache à l'appel, pas à sa ligne : fichier, API, occurrence et texte de
l'appel (espaces réduits). Ajouter du code au-dessus ne le périme pas ; changer l'appel, si :
il faut alors le relire.
Règle de recherche : motifs CREATIONS ci-dessous, ligne par ligne, commentaires et
déclarations P/Invoke exclus ; appels coupés sur deux lignes recollés (ligne suivante jointe
quand la parenthèse ouverte n'est pas fermée).
"""
from __future__ import annotations

import argparse
import io
import os
import re
import sys
from collections import Counter

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "src")

# (catégorie, api, motif de création, motif de libération avec {v} = variable assignée)
CREATIONS = [
    # Minuteries et travail en arrière-plan
    ("minuterie", "SetTimer", r"\bSetTimer\s*\(", None),
    ("minuterie", "System.Threading.Timer", r"new\s+(System\.Threading\.)?Timer\s*\(", r"{v}\s*\??\.\s*(Dispose|Change)\b"),
    ("minuterie", "Thread", r"new\s+Thread\s*\(", None),
    ("minuterie", "ThreadPool", r"ThreadPool\.(QueueUserWorkItem|RegisterWaitForSingleObject)\s*\(", None),
    ("minuterie", "Task.Run", r"\bTask\.(Run|Delay|Factory\.StartNew)\s*\(", None),
    ("minuterie", "CancellationTokenSource", r"new\s+CancellationTokenSource\b", r"{v}\s*\??\.\s*Dispose\b"),
    # Abonnements
    ("abonnement", "event +=", r"\b(?P<evt>[A-Z]\w*)\s*\+=\s*(?!\d)", None),
    ("abonnement", "DialogNavigation.Register", r"DialogNavigation\.Register\s*\(", None),
    ("abonnement", "AppDomain", r"AppDomain\.CurrentDomain\.\w+\s*\+=", None),
    ("abonnement", "SystemEvents", r"SystemEvents\.\w+\s*\+=", None),
    # Objets Windows (USER, noyau, COM)
    ("windows", "SetWindowsHookEx", r"\bSetWindowsHookEx\w*\s*\(", None),
    ("windows", "SetWinEventHook", r"\bSetWinEventHook\s*\(", None),
    ("windows", "WTSRegisterSessionNotification", r"\bWTSRegisterSessionNotification\s*\(", None),
    ("windows", "SetWindowSubclass", r"\bSetWindowSubclass\s*\(", None),
    ("windows", "CoRegisterClassObject", r"\bCoRegisterClassObject\s*\(", None),
    ("windows", "AddFontMemResourceEx", r"\bAddFontMemResourceEx\s*\(", None),
    ("windows", "RegisterClassExW", r"\bRegisterClassExW\s*\(", None),
    ("windows", "GdiplusStartup", r"\bGdiplusStartup\s*\(", None),
    ("windows", "CreateWindowExW", r"\bCreateWindowExW\s*\(", r"DestroyWindow\s*\(\s*{v}\b"),
    ("windows", "Shell_NotifyIconW NIM_ADD", r"\bShell_NotifyIconW\s*\(\s*NIM_ADD", None),
    ("windows", "OpenProcess", r"\bOpenProcess\s*\(", r"CloseHandle\s*\(\s*{v}\b"),
    ("windows", "GlobalAlloc", r"\bGlobalAlloc\s*\(", r"GlobalFree\s*\(\s*{v}\b|SetClipboardData\s*\([^)]*{v}|CreateStreamOnHGlobal\s*\(\s*{v}"),
    ("windows", "CreateStreamOnHGlobal", r"\bCreateStreamOnHGlobal\s*\(", None),
    ("windows", "CoCreateInstance", r"\bCoCreateInstance\s*\(", None),
    ("windows", "CoInitializeEx", r"\bCoInitializeEx\s*\(", None),
    ("windows", "SafeArrayCreateVector", r"\bSafeArrayCreateVector\s*\(", r"SafeArrayDestroy\s*\(\s*{v}\b"),
    ("windows", "OpenClipboard", r"\bOpenClipboard\s*\(", None),
    ("windows", "SetCapture", r"\bSetCapture\s*\(", None),
    ("windows", "CreatePopupMenu", r"\bCreatePopupMenu\s*\(", r"DestroyMenu\s*\(\s*{v}\b|AppendMenuW\s*\([^;]*{v}"),
    ("windows", "CreateCaret", r"\bCreateCaret\s*\(", None),
    ("windows", "RegisterApplicationRestart", r"\bRegisterApplicationRestart\s*\(", None),
    ("windows", "UiaHostProviderFromHwnd", r"\bUiaHostProviderFromHwnd\s*\(", None),
    ("windows", "Mutex", r"new\s+Mutex\s*\(", None),
    ("windows", "FileStream", r"new\s+(FileStream|StreamWriter|StreamReader)\s*\(", None),
    ("windows", "GetDC", r"\bGetDC\s*\(", r"ReleaseDC\s*\([^;]*{v}\b"),
    ("windows", "BeginPaint", r"\bBeginPaint\s*\(", r"EndPaint\s*\("),
    ("windows", "SaveDC", r"\bSaveDC\s*\(", r"RestoreDC\s*\([^;]*{v}\b"),
    # Objets graphiques (GDI et GDI+)
    ("gdi", "CreateFontW", r"\bCreateFontW\s*\(", r"DeleteObject\s*\(\s*(?:ref\s+)?{v}\b"),
    ("gdi", "CreatePen", r"\bCreatePen\s*\(", r"DeleteObject\s*\(\s*(?:ref\s+)?{v}\b"),
    ("gdi", "CreateSolidBrush", r"\bCreateSolidBrush\s*\(", r"DeleteObject\s*\(\s*(?:ref\s+)?{v}\b"),
    ("gdi", "CreateBitmap", r"\bCreateBitmap\s*\(", r"DeleteObject\s*\(\s*(?:ref\s+)?{v}\b"),
    ("gdi", "CreateCompatibleBitmap", r"\bCreateCompatibleBitmap\s*\(", r"DeleteObject\s*\(\s*(?:ref\s+)?{v}\b"),
    ("gdi", "CreateCompatibleDC", r"\bCreateCompatibleDC\s*\(", r"DeleteDC\s*\(\s*{v}\b"),
    ("gdi", "CreateIconIndirect", r"\bCreateIconIndirect\s*\(", r"DestroyIcon\s*\(\s*{v}\b"),
    ("gdi", "GdipCreate", r"\bGdipCreate(?!HBITMAP)\w*\s*\(", r"Gdip(Delete\w+|DisposeImage)\s*\(\s*{v}\b"),
    ("gdi", "GdipCreateHBITMAPFromBitmap", r"\bGdipCreateHBITMAPFromBitmap\s*\(", r"DeleteObject\s*\(\s*(?:ref\s+)?{v}\b"),
    ("gdi", "GdipGetImageGraphicsContext", r"\bGdipGetImageGraphicsContext\s*\(", r"GdipDeleteGraphics\s*\(\s*{v}\b"),
    # Mémoire native et épinglage
    ("memoire", "Marshal.AllocHGlobal", r"Marshal\.AllocHGlobal\s*\(", r"FreeHGlobal\s*\(\s*{v}\b"),
    ("memoire", "Marshal.StringToHGlobalUni", r"Marshal\.StringToHGlobal\w*\s*\(", r"FreeHGlobal\s*\(\s*{v}\b"),
    ("memoire", "GCHandle.Alloc", r"GCHandle\.Alloc\s*\(", r"{v}\s*\.\s*Free\s*\("),
    ("windows", "LoadCursorW", r"\bLoadCursorW\s*\(", None),
]

# Libération nommée cherchée dans tout le fichier quand la variable n'est pas locale :
# api → motif (« {v} » = variable assignée quand il y en a une).
NAMED_PARTNERS = {
    "SetWindowsHookEx": r"UnhookWindowsHookEx\s*\(",
    "SetWinEventHook": r"UnhookWinEvent\s*\(",
    "WTSRegisterSessionNotification": r"WTSUnRegisterSessionNotification\s*\(",
    "CoRegisterClassObject": r"CoRevokeClassObject\s*\(",
    "Shell_NotifyIconW NIM_ADD": r"Shell_NotifyIconW\s*\(\s*NIM_DELETE",
    "OpenClipboard": r"CloseClipboard\s*\(",
    "SetCapture": r"ReleaseCapture\s*\(",
    "CreateCaret": r"DestroyCaret\s*\(",
    "DialogNavigation.Register": r"DialogNavigation\.Unregister\s*\(",
}

# Créations dont la libération est faite par Windows ou n'a pas d'objet : verdict d'office.
RULE_VERDICTS = {
    "LoadCursorW": "aucune : curseur système partagé (LoadCursorW(NULL, IDC_*)), jamais détruit",
}

# Verdicts lus à la main : docs/audit-ressources-verdicts.tsv (site → verdict), pour les
# sites qu'aucune règle ne classe. Le script échoue si un site reste sans verdict.
VERDICTS = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "docs", "audit-ressources-verdicts.tsv")

DECL = re.compile(r"static\s+extern|static\s+partial|\[(DllImport|LibraryImport)|^\s*(internal|public|private)?\s*(static\s+)?(IntPtr|nint|bool|int|uint)\s+\w+\s*\([^)]*\)\s*(=>|;)")
ASSIGN = re.compile(r"(?:\b(?:var|IntPtr|nint)\s+)?(?P<v>[_A-Za-z][\w\.]*)\s*(?:\?\?)?=\s*[^=]")
OUTVAR = re.compile(r"out\s+(?:IntPtr|nint|var)?\s*(?P<v>[_A-Za-z]\w*)\s*\)")
EVENTS_DECL = re.compile(r"\bevent\s+[\w<>?,\. ]+\s+(?P<n>\w+)\s*[;{=]")
WINDOW_LINES = 120


def source_files(root):
    for dirpath, dirnames, filenames in os.walk(root):
        dirnames[:] = sorted(d for d in dirnames if not d.endswith("Tests") and d not in ("bin", "obj", "TestSupport"))
        for fn in sorted(filenames):
            if fn.endswith(".cs"):
                path = os.path.join(dirpath, fn)
                yield os.path.relpath(path, root).replace("\\", "/"), path


def call_text(s):
    """Texte de l'appel qui porte un verdict lu : espaces réduits, stable quand le code bouge."""
    return re.sub(r"\s+", " ", s).strip()


def logical_lines(lines):
    """Rend (numéro, texte) en recollant un appel ouvert sur la ligne suivante."""
    i = 0
    while i < len(lines):
        text = lines[i].rstrip("\n")
        start = i
        while text.count("(") > text.count(")") and i + 1 < len(lines) and i - start < 4:
            i += 1
            text += " " + lines[i].strip()
        yield start + 1, text
        i += 1


def find_all(rx, lines, rel, limit=3):
    hits = [f"{rel}:{k + 1}" for k, line in enumerate(lines)
            if rx.search(line) and not line.strip().startswith("//")]
    return hits[:limit]


def rule_partner(api, s, var, rel, lines, no):
    """Verdict par règle quand la libération n'est pas dans la même méthode."""
    if api in RULE_VERDICTS:
        return RULE_VERDICTS[api]
    release = next((r for c, a, p, r in CREATIONS if a == api), None)
    # Champ (_x) : la libération est souvent dans Dispose ou WM_DESTROY, loin de la création.
    if release and var and (var.startswith("_") or "." in var):
        hits = find_all(re.compile(release.format(v=re.escape(var))), lines, rel)
        if hits:
            return "libéré : " + ", ".join(hits)
    if api == "CreateWindowExW" and "WS_CHILD" in s and re.search(r",\s*_hWnd\s*,", s):
        hits = find_all(re.compile(r"DestroyWindow\s*\(\s*_hWnd\b"), lines, rel)
        if hits:
            return "fenêtre enfant de _hWnd : détruite avec elle (" + ", ".join(hits) + ")"
    if api == "SetTimer":
        m = re.search(r"SetTimer\s*\(\s*(\w+)\s*,\s*(?:\(UIntPtr\)\s*)?(\w+)", s)
        if m and m.group(1) in ("_hWnd", "hWnd"):
            kills = find_all(re.compile(r"KillTimer\s*\([^,]+,\s*(?:\(UIntPtr\)\s*)?" + re.escape(m.group(2)) + r"\b"), lines, rel)
            destroy = find_all(re.compile(r"DestroyWindow\s*\(\s*_hWnd\b"), lines, rel, 1)
            if kills or destroy:
                return ("minuterie de fenêtre, même id remplacé : "
                        + (("KillTimer " + ", ".join(kills)) if kills else "aucun KillTimer")
                        + (f" ; morte avec la fenêtre ({destroy[0]})" if destroy else ""))
    if api == "event +=":
        m = re.search(r"([\w\.]+)\s*\+=\s*(\w+)\s*;", s)
        if m:
            hits = find_all(re.compile(re.escape(m.group(1)) + r"\s*-=\s*" + re.escape(m.group(2)) + r"\b"), lines, rel)
            if hits:
                return "désabonné : " + ", ".join(hits)
    if api == "SetWindowSubclass":
        m = re.search(r"SetWindowSubclass\s*\(\s*[\w\.]+\s*,\s*(\w+)", s)
        if m:
            hits = find_all(re.compile(r"RemoveWindowSubclass\s*\([^;]*\b" + re.escape(m.group(1)) + r"\b"), lines, rel)
            if hits:
                return "retirée : " + ", ".join(hits)
    if api == "FileStream" and re.match(r"\s*using\b", s):
        return "using : fermé en fin de bloc"
    if api in NAMED_PARTNERS:
        hits = find_all(re.compile(NAMED_PARTNERS[api]), lines, rel)
        if hits:
            return "libéré : " + ", ".join(hits)
    if api == "GdiplusStartup":
        m = re.search(r"out\s+(?:IntPtr\s+)?(\w+)", s)
        if m:
            hits = find_all(re.compile(r"GdiplusShutdown\s*\(\s*" + re.escape(m.group(1)) + r"\b"), lines, rel)
            if hits:
                return "arrêté : " + ", ".join(hits)
    return "à lire"


def fr_typo(line):
    """Espace insécable avant : ; ? ! » et après «, hors code et cibles de liens."""
    parts = re.split(r"(`[^`]*`|\]\([^)]*\))", line)
    for i in range(0, len(parts), 2):
        parts[i] = re.sub(r" ([:;?!»])", " \\1", parts[i]).replace("« ", "« ")
    return "".join(parts)


def load_verdicts(path):
    """(fichier, api, occurrence, appel) → verdict. Une ligne : fichier, api, occurrence, appel,
    verdict, séparés par des tabulations."""
    out = {}
    if os.path.exists(path):
        with open(path, encoding="utf-8") as f:
            for no, line in enumerate(f, 1):
                if line.startswith("#") or not line.strip() or line.startswith("fichier\t"):
                    continue
                parts = line.rstrip("\n").split("\t")
                if len(parts) != 5 or not parts[2].isdigit():
                    sys.exit(f"{path}:{no} : attendu fichier, api, occurrence, appel, verdict")
                rel, api, occ, call, verdict = parts
                out[(rel, api, int(occ), call)] = verdict
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--tsv")
    ap.add_argument("--md", help="écrit l'inventaire complet en Markdown")
    ap.add_argument("--check", action="store_true", help="échoue si un site n'a pas de verdict")
    ap.add_argument("--src", default=ROOT, help="dossier des sources (src/ par défaut)")
    ap.add_argument("--verdicts", default=VERDICTS, help="verdicts lus (docs/audit-ressources-verdicts.tsv par défaut)")
    args = ap.parse_args()

    declared_events = set()
    files = list(source_files(args.src))
    for rel, path in files:
        with open(path, encoding="utf-8-sig") as f:
            for line in f:
                m = EVENTS_DECL.search(line)
                if m:
                    declared_events.add(m.group("n"))

    rows = []
    for rel, path in files:
        with open(path, encoding="utf-8-sig") as f:
            lines = f.readlines()
        for no, text in logical_lines(lines):
            s = text.strip()
            if s.startswith("//") or s.startswith("*") or DECL.search(s):
                continue
            for cat, api, pat, release in CREATIONS:
                m = re.search(pat, s)
                if not m:
                    continue
                if api == "event +=" and m.group("evt") not in declared_events:
                    continue
                var = None
                if release:
                    mo = OUTVAR.search(s[m.start():])
                    ma = ASSIGN.search(s[: m.start()]) if not mo else None
                    var = (mo or ma).group("v") if (mo or ma) else None
                partner = "à lire"
                if release and var:
                    rx = re.compile(release.format(v=re.escape(var)))
                    for k in range(no - 1 + 1, min(len(lines), no - 1 + WINDOW_LINES)):
                        if rx.search(lines[k]):
                            partner = f"{rel}:{k + 1}"
                            break
                elif release and not var:
                    rx = re.compile(release.replace("{v}", r"\w+"))
                    for k in range(no, min(len(lines), no + WINDOW_LINES)):
                        if rx.search(lines[k]):
                            partner = f"{rel}:{k + 1}"
                            break
                if partner == "à lire":
                    partner = rule_partner(api, s, var, rel, lines, no)
                rows.append((cat, api, f"{rel}:{no}", var or "", partner, s[:150], rel, call_text(s)))

    # Deux créations sur la même ligne logique : le site prend le nom de l'API en suffixe.
    seen = Counter(r[2] for r in rows)
    rows = [r[:2] + (f"{r[2]} {r[1]}",) + r[3:] if seen[r[2]] > 1 else r for r in rows]
    # Libération trouvée dans la même méthode : « libéré : site ».
    rows = [r if r[4] == "à lire" or not re.match(r"^[\w\./]+\.cs:\d+$", r[4]) else r[:4] + ("libéré : " + r[4],) + r[5:]
            for r in rows]
    # Ancre d'un verdict lu : fichier, API, rang de l'appel identique dans le fichier, appel.
    rank = Counter()
    anchors = []
    for r in rows:
        rank[(r[6], r[1], r[7])] += 1
        anchors.append((r[6], r[1], rank[(r[6], r[1], r[7])], r[7]))
    manual = load_verdicts(args.verdicts)
    stale = sorted(set(manual) - set(anchors))
    rows = [r[:4] + (manual[a] + " (lu)",) + r[5:] if r[4] == "à lire" and a in manual else r
            for r, a in zip(rows, anchors)]

    counts = Counter((r[0], r[1]) for r in rows)
    per_cat = Counter(r[0] for r in rows)
    unread = [r for r in rows if r[4] == "à lire"]
    if args.tsv:
        with open(args.tsv, "w", encoding="utf-8") as o:
            o.write("categorie\tapi\tsite\tvariable\tliberation\ttexte\toccurrence\tappel\n")
            for r, a in zip(rows, anchors):
                o.write("\t".join(r[:6] + (str(a[2]), a[3])) + "\n")
    if args.md:
        o = io.StringIO()
        o.write("# Inventaire des ressources de src/ (généré)\n\n")
        o.write("Produit par `python scripts/audit-ressources-inventaire.py --md "
                "docs/audit-ressources-inventaire.md` ; ne pas modifier à la main. Verdicts lus : "
                "`docs/audit-ressources-verdicts.tsv`. Lecture : [audit-ressources.md](audit-ressources.md).\n\n")
        o.write(f"{len(rows)} créations : " + ", ".join(f"{k} {v}" for k, v in sorted(per_cat.items())) + ".\n\n")
        o.write("| Catégorie | API | Nombre |\n|---|---|---|\n")
        for (cat, api), n in sorted(counts.items()):
            o.write(f"| {cat} | {api} | {n} |\n")
        for cat in sorted(per_cat):
            o.write(f"\n## {cat} ({per_cat[cat]})\n\n| Site | API | Libération |\n|---|---|---|\n")
            for r in sorted((r for r in rows if r[0] == cat), key=lambda r: (r[1], r[2])):
                o.write(f"| `{r[2]}` | {r[1]} | {r[4].replace('|', '/')} |\n")
        with open(args.md, "w", encoding="utf-8", newline="\n") as f:
            f.writelines(fr_typo(line) for line in o.getvalue().splitlines(keepends=True))
    out = sys.stdout
    out.reconfigure(encoding="utf-8")
    for (cat, api), n in sorted(counts.items()):
        out.write(f"{cat:11} {api:32} {n}\n")
    out.write(f"Total {len(rows)} : " + ", ".join(f"{k} {v}" for k, v in sorted(per_cat.items()))
              + f" ; sans verdict : {len(unread)} ; verdicts lus périmés : {len(stale)}\n")
    if args.check and (unread or stale):
        for r in unread:
            out.write(f"sans verdict : {r[2]}\t{r[1]}\t{r[5]}\n")
        for rel, api, occ, call in stale:
            out.write(f"verdict lu sans appel (appel modifié ou retiré : relire) : {rel}\t{api}\t{occ}\t{call[:150]}\n")
        sys.exit(1)


if __name__ == "__main__":
    main()
