"""Témoin de mutation du lot F : les gardes du modèle d'administration rougissent-ils ?

    python docs/audit-v1.2.0/witness-lot-f.py

`scripts/tests/test_admx_agreement.py` compare le modèle ADMX, ses deux fichiers de libellés et
le `.reg` d'exemple au code qui lit réellement les cinq clés. Vingt et un tests au vert ne
prouvent rien : un contrôle qui n'a jamais échoué n'est pas un contrôle. Ce script casse les
fichiers réels d'une façon précise, relance la suite, et dit ce qui a été vu.

Chaque fichier est sauvegardé en mémoire sous forme d'octets et réécrit tel quel à la fin, y
compris si une mutation lève. Le `.reg` est en UTF-16 LE avec BOM : le manipuler en texte le
casserait, d'où le passage par les octets partout.

Sortie attendue sur un dépôt sain : dix mutations, dix rouges.
"""
import subprocess
import sys
from pathlib import Path

for stream in (sys.stdout, sys.stderr):
    reconfigure = getattr(stream, "reconfigure", None)
    if reconfigure is not None:
        try:
            reconfigure(encoding="utf-8", errors="backslashreplace")
        except (OSError, ValueError):
            pass

ROOT = Path(__file__).resolve().parent.parent.parent
ADMX = ROOT / "entreprise" / "AZERTYGlobal.admx"
ADML_FR = ROOT / "entreprise" / "fr-FR" / "AZERTYGlobal.adml"
ADML_EN = ROOT / "entreprise" / "en-US" / "AZERTYGlobal.adml"
REG = ROOT / "entreprise" / "politiques-exemple.reg"

TOUCHED = (ADMX, ADML_FR, ADML_EN, REG)


def run_suite():
    """Rend (rouge, liste de toutes les lignes FAIL et ERROR).

    Toutes, pas la première : la mutation « libellé orphelin » faisait tomber deux tests, et
    n'afficher que le premier a masqué le seul informatif — un plantage de comparaison passait
    devant le garde réellement concerné. Mesuré le 2026-08-20.
    """
    result = subprocess.run(
        [sys.executable, "-m", "unittest", "discover", "-s", "scripts/tests"],
        cwd=ROOT, capture_output=True, text=True, encoding="utf-8", errors="replace",
    )
    if result.returncode == 0:
        return False, []
    lines = [
        line.strip() for line in (result.stderr or "").splitlines()
        if line.startswith(("FAIL:", "ERROR:"))
    ]
    return True, lines or ["échec sans ligne FAIL identifiable"]


def replace_bytes(path, old, new):
    """Remplace une séquence d'octets, en exigeant qu'elle soit unique."""
    raw = path.read_bytes()
    count = raw.count(old)
    if count != 1:
        raise SystemExit(
            f"{path.name} : {count} occurrence(s) de {old[:40]!r}, mutation impossible."
        )
    path.write_bytes(raw.replace(old, new))


def utf16(text):
    return text.encode("utf-16-le")


def mutate_valueName():
    replace_bytes(ADMX, b'valueName="UsageStatsEnabled"', b'valueName="UsageStatsAllowed"')


def mutate_key_path():
    replace_bytes(
        ADMX,
        b'key="SOFTWARE\\Policies\\AZERTYGlobal"\n            valueName="NotificationsEnabled"',
        b'key="SOFTWARE\\Policies\\AzertyGlobal2"\n            valueName="NotificationsEnabled"',
    )


def mutate_onboarding_inversion():
    """La « correction » qu'un jour quelqu'un croira bonne : rendre la stratégie directe."""
    replace_bytes(
        ADMX,
        b'valueName="ShowOnboarding">\n      <parentCategory ref="AZERTYGlobal" />\n'
        b'      <supportedOn ref="SUPPORTED_AZERTY_GLOBAL_1_2_0" />\n'
        b'      <enabledValue><decimal value="0" /></enabledValue>\n'
        b'      <disabledValue><decimal value="1" /></disabledValue>',
        b'valueName="ShowOnboarding">\n      <parentCategory ref="AZERTYGlobal" />\n'
        b'      <supportedOn ref="SUPPORTED_AZERTY_GLOBAL_1_2_0" />\n'
        b'      <enabledValue><decimal value="1" /></enabledValue>\n'
        b'      <disabledValue><decimal value="0" /></disabledValue>',
    )


def mutate_policy_class():
    replace_bytes(
        ADMX,
        b'<policy name="Language" class="Machine"',
        b'<policy name="Language" class="User"',
    )


def mutate_language_items():
    replace_bytes(ADMX, b"<string>en</string>", b"<string>de</string>")


def mutate_missing_string():
    raw = ADML_FR.read_bytes()
    start = raw.index(b'<string id="ExternalLinksEnabled">')
    end = raw.index(b"</string>", start) + len(b"</string>\n")
    ADML_FR.write_bytes(raw[:start] + raw[end:])


def mutate_orphan_string():
    replace_bytes(
        ADML_FR,
        b'<string id="AZERTYGlobal">AZERTY Global</string>',
        b'<string id="AZERTYGlobal">AZERTY Global</string>\n'
        b'      <string id="Obsolete">Texte que le modele n\'appelle plus</string>',
    )


def mutate_french_copied_from_english():
    """Un .adml recopié d'une langue sur l'autre garde tous les identifiants."""
    ADML_FR.write_bytes(ADML_EN.read_bytes())


def mutate_reg_to_utf8():
    raw = REG.read_bytes()
    REG.write_bytes(raw[2:].decode("utf-16-le").encode("utf-8"))


def mutate_reg_language_type():
    replace_bytes(REG, utf16('"Language"="fr"'), utf16('"Language"=dword:00000001'))


MUTATIONS = (
    ("valueName du modèle renommé", mutate_valueName),
    ("racine de registre différente du code", mutate_key_path),
    ("inversion de la fenêtre de bienvenue « corrigée »", mutate_onboarding_inversion),
    ("stratégie passée en class=User", mutate_policy_class),
    ("langue « de » au lieu de « en » dans le sélecteur", mutate_language_items),
    ("libellé français supprimé", mutate_missing_string),
    ("libellé orphelin ajouté", mutate_orphan_string),
    ("français recopié depuis l'anglais", mutate_french_copied_from_english),
    (".reg réécrit en UTF-8", mutate_reg_to_utf8),
    ("Language posée en REG_DWORD", mutate_reg_language_type),
)


def main():
    backups = {path: path.read_bytes() for path in TOUCHED}

    red, lines = run_suite()
    if red:
        raise SystemExit(
            "La suite est déjà rouge avant toute mutation :\n  "
            + "\n  ".join(lines)
            + "\nRien n'a été touché."
        )
    print(f"Départ : suite au vert.\n{len(MUTATIONS)} mutations.\n")

    seen = 0
    try:
        for label, mutate in MUTATIONS:
            mutate()
            red, lines = run_suite()
            for path in TOUCHED:
                path.write_bytes(backups[path])
            if red:
                seen += 1
                print(f"  ROUGE  {label}")
                for line in lines:
                    print(f"         {line}")
            else:
                print(f"  VERT   {label}  <-- rien ne l'a vue")
    finally:
        for path, raw in backups.items():
            path.write_bytes(raw)

    print(f"\n{seen} mutation(s) sur {len(MUTATIONS)} vues.")
    for path in TOUCHED:
        assert path.read_bytes() == backups[path], f"{path} n'a pas été restauré"
    print("Tous les fichiers restaurés à l'octet.")
    return 0 if seen == len(MUTATIONS) else 1


if __name__ == "__main__":
    raise SystemExit(main())
