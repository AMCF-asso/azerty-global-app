"""CH3 passe 2, lot B (2/2) — les quatre appels aux méthodes de police disparues.

Suite de patch_onboarding_typo.py. Trois sites : la construction, `RecreateFonts` et `Dispose`.
`RecreateFonts` perd sa raison d'être — le cache de Theme est indexé par rôle et par DPI, donc
une bascule d'échelle rend déjà les bonnes polices sans rien recréer — mais elle garde son nom
et son `ApplyFontsToControls`, qui reste nécessaire : les contrôles Win32 détiennent le handle
de police qu'on leur a envoyé, pas une référence au cache.

⚠️ OnboardingWindow.cs est mixte. Chaque bloc est essayé en CRLF puis en LF ; le compte des
sauts isolés doit être identique avant et après, aucune ligne n'étant supprimée ici.
"""

import pathlib
import sys

sys.stdout.reconfigure(encoding="utf-8")

ROOT = pathlib.Path(__file__).resolve().parents[3]
ONBOARDING = ROOT / "src" / "OnboardingWindow.cs"

CRLF = b"\r\n"
LF = b"\n"

data = ONBOARDING.read_bytes()
crlf_before = data.count(CRLF)
lf_before = data.count(LF) - crlf_before


def block(name, old_lines, new_lines):
    global data
    for eol, label in ((CRLF, "CRLF"), (LF, "LF  ")):
        old = eol.join(line.encode("utf-8") for line in old_lines)
        found = data.count(old)
        if found == 1:
            data = data.replace(old, eol.join(line.encode("utf-8") for line in new_lines))
            print(f"  {name:32s} {label}  {len(old_lines)} → {len(new_lines)} lignes")
            return
        if found > 1:
            sys.exit(f"{name} : {found} occurrences en {label}, attendu 1")
    sys.exit(f"{name} : introuvable en CRLF comme en LF — bloc mixte ?")


block(
    "construction",
    [
        "        CreateFonts();",
        "        CreateMainWindow();",
    ],
    ["        CreateMainWindow();"],
)

block(
    "RecreateFonts",
    [
        "    private void RecreateFonts()",
        "    {",
        "        DestroyFonts();",
        "        CreateFonts();",
        "        ApplyFontsToControls();",
        "    }",
    ],
    [
        "    /// <summary>",
        "    /// Rejoue l'envoi des polices aux contrôles après un changement d'échelle. Elle ne",
        "    /// recrée plus rien depuis le 2026-08-30 : le cache de Theme est indexé par rôle et par",
        "    /// DPI, donc les propriétés rendent déjà la bonne police. Mais un contrôle Win32 détient",
        "    /// le handle qu'on lui a envoyé par WM_SETFONT, pas une référence au cache — sans ce",
        "    /// renvoi, il garderait la police de l'ancienne échelle.",
        "    /// </summary>",
        "    private void RecreateFonts()",
        "    {",
        "        ApplyFontsToControls();",
        "    }",
    ],
)

block(
    "Dispose",
    [
        "        if (_gdipToken != IntPtr.Zero) { Win32.GdiplusShutdown(_gdipToken); _gdipToken = IntPtr.Zero; }",
        "        DestroyFonts();",
    ],
    ["        if (_gdipToken != IntPtr.Zero) { Win32.GdiplusShutdown(_gdipToken); _gdipToken = IntPtr.Zero; }"],
)

crlf_after = data.count(CRLF)
lf_after = data.count(LF) - crlf_after
if lf_after != lf_before:
    sys.exit(f"LF isolés passés de {lf_before} à {lf_after} : aucune ligne à saut isolé "
             "n'était censée disparaître ici")

ONBOARDING.write_bytes(data)
print(f"  OnboardingWindow.cs  CRLF {crlf_before} → {crlf_after}, LF isolés {lf_after}")
