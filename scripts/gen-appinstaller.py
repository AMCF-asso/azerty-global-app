"""Génère le `.appinstaller` du canal AMCF, et vérifie qu'il dit la vérité sur le bundle.

Lot E du plan v1.2.0. Le fichier `.appinstaller` est interrogé par App Installer, composant
de Windows : c'est lui qui va chercher la mise à jour, jamais le code de l'application. La
phrase « aucun envoi réseau automatique par l'application » reste donc vraie, et une DSI qui
refuse la vérification bloque l'URL de son côté.

    python scripts/gen-appinstaller.py --bundle <chemin.msixbundle> --print
    python scripts/gen-appinstaller.py --bundle <chemin.msixbundle> --out msix/AZERTY_Global.appinstaller
    python scripts/gen-appinstaller.py --bundle <chemin.msixbundle> --check msix/AZERTY_Global.appinstaller

**L'identité n'est jamais recopiée à la main.** Elle est lue dans le manifeste du bundle
signé, parce que le `Publisher` d'un paquet MSIX égale le sujet du certificat qui le signe :
le retaper, c'est prendre le risque exact que le plan appelle son piège n°3 — une version ou
un éditeur qui diverge, et la mise à jour boucle ou ne se déclenche jamais.

Trois faits mesurés le 2026-08-20 cadrent le contenu produit ; les trois sont contre-intuitifs
et aucun ne se devine à la lecture du seul tableau de référence de Microsoft.

1. **Le `Publisher` de l'association porte des caractères non-ASCII** — `Français`, `Puy-de-Dôme` —
   alors que la page de référence de l'élément racine dit « Only encoding="UTF-8" with no escape
   characters, and no non-ascii characters is accepted ». La regex de DN de la page `MainBundle`,
   elle, les autorise. Un `.appinstaller` public en service tranche en faveur de la regex :
   `MicaForEveryone.appinstaller` publie `CN=Đặng Bình Minh, O=Đặng Bình Minh, L=Hà Nội, C=VN`
   et sert de méthode d'installation officielle de ce projet. Rien de tout cela ne prouve
   l'installation : seul le critère d'acceptation du lot E — installation sur machine propre,
   puis constat d'une montée de version — la prouvera. D'ici là, ce script écrit les accents
   tels qu'ils sont dans le bundle et refuse tout échappement.
2. **`ShowPrompt` et `UpdateBlocksActivation` ne sont pas écrits, à dessein.** La page `OnLaunch`
   dit des applications desktop empaquetées — ce que cette app est : « For desktop applications,
   this functionality provides a silent update; the same default functionality provided by the
   OnLaunch element. » La mise à jour silencieuse et non bloquante est donc le comportement par
   défaut, et ces deux attributs seraient inertes. Les écrire aurait en plus introduit une
   dépendance à Windows 10 1903 alors que l'app cible 1809.
3. **Pas de préfixe `s4:` sur `HoursBetweenUpdateChecks`.** Le tableau de la doc l'écrit
   `s4:HoursBetweenUpdateChecks`, et ses deux pages voisines ne s'accordent même pas sur ce que
   `s4` désigne (2018 pour `UpdateSettings`, 2021 pour `OnLaunch`). Le fichier de production
   cité plus haut l'écrit sans préfixe sous le namespace `2017/2`. C'est cette forme qui est
   reprise : elle tourne.
"""
import argparse
import re
import sys
import xml.etree.ElementTree as ET
import zipfile
from pathlib import Path
from typing import NamedTuple

for stream in (sys.stdout, sys.stderr):
    reconfigure = getattr(stream, "reconfigure", None)
    if reconfigure is not None:
        try:
            reconfigure(encoding="utf-8", errors="backslashreplace")
        except (OSError, ValueError):
            pass

ROOT = Path(__file__).resolve().parent.parent

# Entrée du manifeste dans un `.msixbundle`. Un `.msix` simple porte `AppxManifest.xml` à la
# racine : ce script ne traite que le bundle, seul artefact que le canal AMCF distribue.
BUNDLE_MANIFEST_ENTRY = "AppxMetadata/AppxBundleManifest.xml"

# Namespace de l'élément racine. `2017/2` est celui du bloc de syntaxe de la doc et celui du
# fichier de production observé.
APPINSTALLER_NAMESPACE = "http://schemas.microsoft.com/appx/appinstaller/2017/2"

# URL du `.appinstaller` lui-même. App Installer compare cette valeur à l'URL par laquelle il
# a obtenu le fichier : si elles diffèrent, il suit celle-ci. Elle doit donc être l'URL
# canonique, pas une redirection.
SELF_URI = "https://download.azerty.global/AZERTY_Global.appinstaller"

# URL du bundle. Nom stable, sans version — décision d'Antoine du 2026-08-20 : une seule URL à
# autoriser dans un pare-feu, pour toujours. Conséquence côté serveur : cet objet change de
# contenu à chaque release, donc il ne peut pas être servi avec le `Cache-Control` d'un an que
# le Worker `download-msix` applique aujourd'hui à tous ses fichiers.
BUNDLE_URI = "https://download.azerty.global/AZERTY_Global.msixbundle"

# Décision d'Antoine du 2026-08-20. C'est aussi le défaut de Windows quand l'attribut est
# absent, et il est écrit quand même : une DSI qui lit le fichier ne doit pas avoir à connaître
# le défaut pour savoir ce que le poste va faire.
HOURS_BETWEEN_UPDATE_CHECKS = 24

# Éditeur du canal Store. Un bundle signé par cette identité ne s'installe pas hors Store :
# produire un `.appinstaller` qui le désigne publierait un fichier incapable d'installer quoi
# que ce soit. Refus, plutôt qu'un fichier qui a l'air correct.
STORE_PUBLISHER = "CN=7FD049E3-1C58-42E0-A07F-A9712DE19E38"

QUAD_VERSION = re.compile(r"^\d+\.\d+\.\d+\.\d+$")

# La doc interdit les caractères d'échappement dans ce fichier. Le motif cherche une référence
# d'entité ou de caractère XML, la seule forme d'échappement qui puisse s'y trouver.
XML_ESCAPE = re.compile(r"&(#[0-9]+|#x[0-9A-Fa-f]+|[A-Za-z][A-Za-z0-9]*);")

# Caractères qui obligeraient à échapper, donc à violer la contrainte ci-dessus. Le `Publisher`
# de l'association n'en contient aucun ; un futur certificat pourrait en contenir.
NEEDS_ESCAPING = ("&", "<", ">", '"')


class BundleIdentity(NamedTuple):
    """Identité lue dans le manifeste du bundle. C'est la seule source de vérité du fichier."""

    name: str
    publisher: str
    version: str


def local_name(tag: str) -> str:
    """Nom d'élément sans son namespace. Les manifestes de bundle en portent plusieurs."""
    return tag.rsplit("}", 1)[-1]


def read_bundle_identity(bundle_path: Path) -> BundleIdentity:
    """Lit `<Identity>` dans le manifeste du bundle.

    Le manifeste est passé à l'analyseur sous forme d'octets, pas de texte : c'est sa
    déclaration XML qui fixe son encodage, et le deviner ici ferait entrer une hypothèse là où
    le fichier porte la réponse.
    """
    with zipfile.ZipFile(bundle_path) as archive:
        try:
            raw = archive.read(BUNDLE_MANIFEST_ENTRY)
        except KeyError:
            raise SystemExit(
                f"{bundle_path} ne contient pas {BUNDLE_MANIFEST_ENTRY} : ce n'est pas un "
                ".msixbundle, ou son manifeste est ailleurs."
            )

    root = ET.fromstring(raw)
    for element in root.iter():
        if local_name(element.tag) != "Identity":
            continue
        missing = [key for key in ("Name", "Publisher", "Version") if key not in element.attrib]
        if missing:
            raise SystemExit(
                f"L'élément Identity de {bundle_path} n'a pas {', '.join(missing)}."
            )
        return BundleIdentity(
            name=element.attrib["Name"],
            publisher=element.attrib["Publisher"],
            version=element.attrib["Version"],
        )

    raise SystemExit(f"Aucun élément Identity dans le manifeste de {bundle_path}.")


def build(identity: BundleIdentity, hours: int = HOURS_BETWEEN_UPDATE_CHECKS,
          self_uri: str = SELF_URI, bundle_uri: str = BUNDLE_URI) -> str:
    """Construit le fichier par gabarit, pas par sérialiseur XML.

    Un sérialiseur échapperait les accents ou réordonnerait les attributs à sa convenance ;
    ici, l'octet écrit doit être celui décidé, et l'égalité littérale avec le manifeste du
    bundle est justement ce que la vérification contrôle ensuite.
    """
    if identity.publisher == STORE_PUBLISHER:
        raise SystemExit(
            "Ce bundle porte l'identité du canal Store. Un paquet signé pour le Store ne "
            "s'installe pas par .appinstaller : générer ce fichier pour lui publierait une "
            "promesse qu'aucune machine ne peut tenir. Utiliser le bundle signé par "
            "l'association."
        )

    if not QUAD_VERSION.match(identity.version):
        raise SystemExit(
            f"La version du bundle, « {identity.version} », n'est pas en notation à quatre "
            "segments. App Installer la refuse."
        )

    if not 0 <= hours <= 255:
        raise SystemExit(f"HoursBetweenUpdateChecks doit tenir entre 0 et 255, pas {hours}.")

    for value, label in ((identity.publisher, "Publisher"), (identity.name, "Name")):
        found = [char for char in NEEDS_ESCAPING if char in value]
        if found:
            raise SystemExit(
                f"Le {label} du bundle contient {' '.join(found)}, qui obligerait à écrire une "
                "séquence d'échappement — or la doc AppInstaller n'en accepte aucune dans ce "
                "fichier. Décision à remonter avant de publier."
            )

    return (
        '<?xml version="1.0" encoding="utf-8"?>\n'
        f'<AppInstaller xmlns="{APPINSTALLER_NAMESPACE}"\n'
        f'              Uri="{self_uri}"\n'
        f'              Version="{identity.version}">\n'
        f'  <MainBundle Name="{identity.name}"\n'
        f'              Publisher="{identity.publisher}"\n'
        f'              Version="{identity.version}"\n'
        f'              Uri="{bundle_uri}" />\n'
        "  <UpdateSettings>\n"
        f'    <OnLaunch HoursBetweenUpdateChecks="{hours}" />\n'
        "  </UpdateSettings>\n"
        "</AppInstaller>\n"
    )


def validate(raw: bytes, identity: BundleIdentity, hours: int = HOURS_BETWEEN_UPDATE_CHECKS,
             self_uri: str = SELF_URI, bundle_uri: str = BUNDLE_URI) -> list:
    """Rend la liste des écarts entre le fichier et le bundle qu'il prétend décrire.

    Liste vide : le fichier dit la vérité. Chaque contrôle existe parce qu'un fichier qui
    échoue dessus reste un XML valide, s'ouvre sans erreur, et casse à l'installation ou —
    pire — s'installe et ne se met jamais à jour.
    """
    problems = []

    if raw.startswith(b"\xef\xbb\xbf"):
        problems.append("Le fichier commence par un BOM UTF-8.")
    if b"\r\n" in raw:
        problems.append("Le fichier porte des fins de ligne CRLF ; ce dépôt les veut en LF.")

    escape = XML_ESCAPE.search(raw.decode("utf-8", errors="replace"))
    if escape is not None:
        problems.append(
            f"Le fichier contient la séquence d'échappement « {escape.group(0)} » ; la doc "
            "AppInstaller n'en accepte aucune."
        )

    try:
        root = ET.fromstring(raw)
    except ET.ParseError as error:
        problems.append(f"XML illisible : {error}")
        return problems

    if local_name(root.tag) != "AppInstaller":
        problems.append(f"L'élément racine est {local_name(root.tag)}, pas AppInstaller.")
        return problems

    if root.attrib.get("Uri") != self_uri:
        problems.append(
            f"Uri de l'élément racine : « {root.attrib.get('Uri')} » au lieu de « {self_uri} ». "
            "App Installer suivrait cette valeur au lieu de l'URL réellement servie."
        )
    if root.attrib.get("Version") != identity.version:
        problems.append(
            f"Version de l'élément racine : « {root.attrib.get('Version')} » au lieu de "
            f"« {identity.version} »."
        )

    bundles = [child for child in root if local_name(child.tag) == "MainBundle"]
    packages = [child for child in root if local_name(child.tag) == "MainPackage"]
    if packages:
        problems.append("Le fichier déclare un MainPackage ; le canal AMCF distribue un bundle.")
    if len(bundles) != 1:
        problems.append(f"{len(bundles)} élément(s) MainBundle au lieu d'un seul.")
        return problems

    bundle = bundles[0]
    for attribute, expected in (
        ("Name", identity.name),
        ("Publisher", identity.publisher),
        ("Version", identity.version),
    ):
        actual = bundle.attrib.get(attribute)
        if actual != expected:
            problems.append(
                f"MainBundle {attribute} : « {actual} » au lieu de « {expected} », qui est ce "
                "que porte le manifeste du bundle. L'installation échouerait."
            )
    if bundle.attrib.get("Uri") != bundle_uri:
        problems.append(
            f"MainBundle Uri : « {bundle.attrib.get('Uri')} » au lieu de « {bundle_uri} »."
        )

    settings = [child for child in root if local_name(child.tag) == "UpdateSettings"]
    if not settings:
        problems.append(
            "Pas de bloc UpdateSettings : le poste n'irait jamais chercher de mise à jour, "
            "contre la décision D7."
        )
        return problems

    background = [child for child in settings[0] if local_name(child.tag) == "AutomaticBackgroundTask"]
    if background:
        problems.append(
            "AutomaticBackgroundTask est présent : la vérification en tâche de fond a été "
            "écartée le 2026-08-20, elle crée du trafic hors usage de l'app."
        )

    launches = [child for child in settings[0] if local_name(child.tag) == "OnLaunch"]
    if len(launches) != 1:
        problems.append(f"{len(launches)} élément(s) OnLaunch au lieu d'un seul.")
        return problems

    launch = launches[0]
    declared = None
    for attribute, value in launch.attrib.items():
        if local_name(attribute) == "HoursBetweenUpdateChecks":
            declared = value
    if declared is None:
        problems.append(
            "OnLaunch n'écrit pas HoursBetweenUpdateChecks. Le défaut de Windows est 24, mais "
            "il doit rester lisible dans le fichier."
        )
    elif declared != str(hours):
        problems.append(f"HoursBetweenUpdateChecks vaut « {declared} » au lieu de « {hours} ».")

    prompt = None
    blocks = None
    for attribute, value in launch.attrib.items():
        if local_name(attribute) == "ShowPrompt":
            prompt = value
        elif local_name(attribute) == "UpdateBlocksActivation":
            blocks = value
    if blocks == "true" and prompt != "true":
        problems.append(
            'UpdateBlocksActivation="true" sans ShowPrompt="true" : la doc le refuse '
            "explicitement."
        )

    return problems


def non_ascii_report(text: str) -> list:
    """Liste les caractères non-ASCII du fichier, avec leur point de code.

    Ce n'est pas un contrôle : c'est la trace de ce que le critère d'acceptation du lot E doit
    éprouver. La doc de l'élément racine les interdit, un fichier public en service les emploie,
    et seule l'installation sur machine propre tranchera.
    """
    seen = {}
    for char in text:
        if ord(char) > 127:
            seen[char] = seen.get(char, 0) + 1
    return [f"{char} (U+{ord(char):04X}) × {count}" for char, count in sorted(seen.items())]


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--bundle", required=True, type=Path,
                        help="Bundle signé par l'association, source de l'identité.")
    parser.add_argument("--out", type=Path, help="Écrit le fichier à ce chemin.")
    parser.add_argument("--check", type=Path, help="Vérifie un fichier existant, n'écrit rien.")
    parser.add_argument("--print", action="store_true", dest="show",
                        help="Affiche le fichier sans l'écrire.")
    parser.add_argument("--hours", type=int, default=HOURS_BETWEEN_UPDATE_CHECKS,
                        help=f"HoursBetweenUpdateChecks (défaut : {HOURS_BETWEEN_UPDATE_CHECKS}).")
    args = parser.parse_args(argv)

    if not args.bundle.exists():
        print(f"Bundle introuvable : {args.bundle}")
        return 2

    identity = read_bundle_identity(args.bundle)
    print(f"Bundle    : {args.bundle}")
    print(f"Name      : {identity.name}")
    print(f"Publisher : {identity.publisher}")
    print(f"Version   : {identity.version}")

    if args.check is not None:
        if not args.check.exists():
            print(f"Fichier à vérifier introuvable : {args.check}")
            return 2
        raw = args.check.read_bytes()
        problems = validate(raw, identity, hours=args.hours)
        report = non_ascii_report(raw.decode("utf-8", errors="replace"))
        print(f"Non-ASCII : {', '.join(report) if report else 'aucun'}")
        if problems:
            print(f"\n{len(problems)} écart(s) :")
            for problem in problems:
                print(f"  - {problem}")
            return 1
        print("\nLe fichier dit la vérité sur ce bundle.")
        return 0

    text = build(identity, hours=args.hours)
    problems = validate(text.encode("utf-8"), identity, hours=args.hours)
    if problems:
        print("\nLe fichier produit ne passe pas sa propre vérification :")
        for problem in problems:
            print(f"  - {problem}")
        return 1

    report = non_ascii_report(text)
    print(f"Non-ASCII : {', '.join(report) if report else 'aucun'}")

    if args.show:
        print()
        print(text, end="")
    if args.out is not None:
        args.out.write_bytes(text.encode("utf-8"))
        print(f"\nÉcrit : {args.out}")
    if not args.show and args.out is None:
        print("\nRien écrit : passer --out ou --print.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
