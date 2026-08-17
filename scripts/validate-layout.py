"""Valide la disposition embarquée contre son schéma, puis contre elle-même.

Deux contrôles que rien ne faisait :

1. **Le schéma** — `schemas/azerty-layout.schema.json`, fermé à chaque niveau structurel.
   Une clé racine mal orthographiée est rejetée au lieu d'être ignorée en silence : sans
   cela, un `deadkeys` fautif laisse `TryGetProperty("dead_keys")` échouer et l'application
   démarre avec zéro touche morte, sans la moindre erreur.

2. **Ce que JSON Schema ne sait pas exprimer** — recompter, et vérifier des références
   croisées. Les cinq compteurs de `statistics` sont recalculés depuis `rows` et
   `dead_keys` au lieu d'être crus sur parole ; les jetons `dk_*` posés sur les couches
   sont recoupés avec les touches mortes déclarées ; l'unicité des scancodes et des
   positions est vérifiée, un doublon de scancode s'écrasant en silence dans le
   dictionnaire construit par le parseur.

    python scripts/validate-layout.py

Codes de sortie :

    0  la disposition est conforme
    1  au moins une violation — schéma, compteur ou référence
    2  contrôle impossible : fichier absent, ou dépendance jsonschema manquante
"""
import json
import sys
from pathlib import Path

for stream in (sys.stdout, sys.stderr):
    reconfigure = getattr(stream, "reconfigure", None)
    if reconfigure is not None:
        try:
            reconfigure(encoding="utf-8", errors="backslashreplace")
        except (OSError, ValueError):
            pass

ROOT = Path(__file__).resolve().parent.parent
LAYOUT = ROOT / "src" / "AZERTY Global 2026.json"
SCHEMA = ROOT / "schemas" / "azerty-layout.schema.json"

LAYERS = (
    "base", "shift", "alt_gr", "shift_alt_gr",
    "caps", "caps_shift", "caps_alt_gr", "caps_shift_alt_gr",
)
DEAD_KEY_PREFIX = "dk_"


def pointer(path) -> str:
    return "/" + "/".join(str(part) for part in path) if path else "(racine)"


def check_schema(layout, schema) -> list[str]:
    from jsonschema import Draft202012Validator

    validator = Draft202012Validator(schema)
    return [
        f"{pointer(error.absolute_path)} : {error.message}"
        for error in sorted(validator.iter_errors(layout), key=lambda e: list(e.absolute_path))
    ]


def check_counters(layout) -> list[str]:
    """Recalcule les cinq compteurs déclarés. Les jetons dk_* ne sont pas des caractères :
    ils arment une touche morte, et les compteurs du fichier les excluent."""
    keys = [key for row in layout["rows"] for key in row["keys"]]
    dead_keys = layout["dead_keys"]

    values = [
        value
        for key in keys
        for layer in LAYERS
        if isinstance(value := key.get(layer), str) and value
    ]
    direct = {value for value in values if not value.startswith(DEAD_KEY_PREFIX)}
    produced = {
        value
        for dead_key in dead_keys.values()
        for value in dead_key["table"].values()
        if isinstance(value, str) and value
    }

    computed = {
        "physical_keys": len(keys),
        "dead_keys_count": len(dead_keys),
        "dead_key_combinations": sum(len(dk["table"]) for dk in dead_keys.values()),
        "direct_characters": len(direct),
        "total_unique_characters": len(direct | produced),
    }
    declared = layout["statistics"]
    return [
        f"statistics.{name} déclare {declared[name]}, la donnée en contient {value}"
        for name, value in computed.items()
        if declared.get(name) != value
    ]


def check_references(layout) -> list[str]:
    keys = [key for row in layout["rows"] for key in row["keys"]]
    declared = set(layout["dead_keys"])
    placed = {
        value
        for key in keys
        for layer in LAYERS
        if isinstance(value := key.get(layer), str) and value.startswith(DEAD_KEY_PREFIX)
    }

    problems = [
        f"la couche {position} arme {token}, qui n'est pas déclarée dans dead_keys"
        for token in sorted(placed - declared)
        for position in [next(
            key["position"] for key in keys
            if token in (key.get(layer) for layer in LAYERS)
        )]
    ]
    problems += [
        f"la touche morte {token} est déclarée mais n'est posée sur aucune couche"
        for token in sorted(declared - placed)
    ]

    for field in ("scancode", "position"):
        seen: dict[str, str] = {}
        for key in keys:
            value = key[field]
            if value in seen:
                problems.append(
                    f"{field} {value} apparaît deux fois, en {seen[value]} et en {key['position']}"
                )
            seen[value] = key["position"]
    return problems


def main() -> int:
    for path in (LAYOUT, SCHEMA):
        if not path.is_file():
            print(f"Introuvable : {path}")
            return 2
    try:
        import jsonschema  # noqa: F401
    except ImportError:
        print("Le module jsonschema est absent. Installer : python -m pip install jsonschema")
        return 2

    layout = json.loads(LAYOUT.read_text(encoding="utf-8"))
    schema = json.loads(SCHEMA.read_text(encoding="utf-8"))

    sections = [
        ("Schéma", check_schema(layout, schema)),
    ]
    # Les deux contrôles suivants supposent une structure déjà valide : les lancer sur un
    # fichier que le schéma rejette produirait des KeyError illisibles à la place des
    # vraies violations.
    if not sections[0][1]:
        sections.append(("Compteurs", check_counters(layout)))
        sections.append(("Références", check_references(layout)))

    total = 0
    for name, problems in sections:
        total += len(problems)
        if problems:
            print(f"\n### {name} — {len(problems)} violation(s)")
            for problem in problems:
                print(f"  {problem}")
        else:
            print(f"{name} : conforme")

    if total:
        print(f"\n{total} violation(s). La disposition n'est pas conforme.")
        return 1

    print(f"\n{LAYOUT.name} est conforme au schéma, à ses compteurs et à ses références.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
