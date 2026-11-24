"""Audit de régression 1.3.0, RP4 : aucun chemin de source du poste de build dans un binaire.

Usage : python scripts/check-source-paths.py <binaire> [<binaire> ...]
Exemple, après `dotnet publish src/AZERTYGlobal.csproj -c Release -r win-x64` :
    python scripts/check-source-paths.py "<dossier de publication>/AZERTY Global.exe"

La couture de troncature (src/TruncationProbe.cs) reçoit [CallerFilePath] ; le PathMap du
csproj remplace le dossier du projet par « /_/ ». Ce script le constate sur le binaire construit.

Un chemin de source du poste = une lettre de lecteur, « :\\ » (ou « :/ »), puis un chemin
qui finit par « .cs », en UTF-16 (chaînes C# : CallerFilePath) ou en UTF-8. Code de sortie 1
si un binaire en contient, 0 sinon. Les chemins trouvés sont comptés et les trois premiers
montrés.
"""
import re
import sys

# Lettre de lecteur seule et un seul séparateur : « https:// » n'est pas un chemin du poste.
UTF16 = re.compile(rb"(?<![A-Za-z]\x00)(?:[A-Za-z]\x00:\x00[\\/]\x00(?![\\/]\x00)(?:[^\x00-\x1f\"<>|*?]\x00){1,400}?\.\x00c\x00s\x00)(?![A-Za-z0-9]\x00)")
UTF8 = re.compile(rb"(?<![A-Za-z])[A-Za-z]:[\\/](?![\\/])[^\x00-\x1f\"<>|*?]{1,400}?\.cs(?![A-Za-z0-9])")


def chemins(data: bytes) -> list[str]:
    found = [m.group(0).decode("utf-16-le", "replace") for m in UTF16.finditer(data)]
    found += [m.group(0).decode("utf-8", "replace") for m in UTF8.finditer(data)]
    return found


def main(paths: list[str]) -> int:
    if not paths:
        print(__doc__)
        return 2
    bad = 0
    for path in paths:
        with open(path, "rb") as handle:
            data = handle.read()
        found = chemins(data)
        mapped = data.count("/_/".encode("utf-16-le"))
        print(f"{path} : {len(found)} chemin(s) de source du poste, {len(set(found))} distinct(s) ; "
              f"{mapped} occurrence(s) de « /_/ » (UTF-16)")
        for sample in sorted(set(found))[:3]:
            print("   " + sample)
        if found:
            bad = 1
    return bad


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
