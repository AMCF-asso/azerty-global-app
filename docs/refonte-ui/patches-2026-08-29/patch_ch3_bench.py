# -*- coding: utf-8 -*-
"""Le banc de captures rend Parametres et Onboarding, les deux fenetres de CH3.

Trois ecritures par fenetre : le DPI passe par `ThemeWindow.DpiOf` (sans quoi la matrice n'est
qu'un rendu repete, mesure du 2026-08-29 sur Conflit et Statistiques), un `Handle` interne est
expose au banc, et `CaptureBench` gagne ses deux cellules.

⚠️ `OnboardingWindow.cs` porte 1 456 CRLF et 246 LF, plus un BOM. La terminaison se deduit donc
de la region de chaque ancre, jamais du fichier : le script essaie la variante LF puis la
variante CRLF et refuse si le total des occurrences n'est pas exactement 1.
"""

import io
import os
import sys

SRC = os.path.normpath(
    os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "..", "src")
)

DPI_COMMENT = (
    "        try\n"
    "        {\n"
    "            // Le DPI passe par ThemeWindow : seul ce point honore l'override du banc de\n"
    "            // captures. Lu en direct, la fenêtre rend toujours à l'échelle du poste et sa\n"
    "            // matrice n'est qu'un rendu répété.\n"
    "            int realDpi = ThemeWindow.DpiOf(_hWnd);\n"
)

DPI_ANCHOR = (
    "        try\n"
    "        {\n"
    "            int realDpi = Win32.GetDpiForWindow(_hWnd);\n"
)

HANDLE = (
    "    /// <summary>Pour le banc de captures : la fenêtre est rendue, elle n'est pas pilotée.</summary>\n"
    "    internal IntPtr Handle => _hWnd;\n"
    "\n"
)

BENCH_METHODS = """    private static bool CaptureSettings(string outDir, string theme, int percent)
    {
        var window = new SettingsWindow();
        try
        {
            window.Show();
            return Capture(window.Handle, Path.Combine(outDir, $"parametres-{theme}-{percent}.png"));
        }
        finally
        {
            Teardown(window);
        }
    }

    private static bool CaptureOnboarding(string outDir, string theme, int percent)
    {
        var window = new OnboardingWindow();
        try
        {
            window.Show();
            return Capture(window.Handle, Path.Combine(outDir, $"onboarding-{theme}-{percent}.png"));
        }
        finally
        {
            Teardown(window);
        }
    }

"""

REPLACEMENTS = [
    ("SettingsWindow.cs", DPI_ANCHOR, DPI_COMMENT),
    (
        "SettingsWindow.cs",
        "    public void Show()\n    {\n        LoadShortcutStateFromConfig();\n",
        HANDLE + "    public void Show()\n    {\n        LoadShortcutStateFromConfig();\n",
    ),
    ("OnboardingWindow.cs", DPI_ANCHOR, DPI_COMMENT),
    (
        "OnboardingWindow.cs",
        "        }\n        catch { /* GetDpiForWindow non disponible (Windows 8.1-) */ }\n",
        "        }\n        catch { /* DpiOf absorbe déjà l'échec ; le filet reste par prudence */ }\n",
    ),
    (
        "OnboardingWindow.cs",
        "    public void Show()\n    {\n        _currentStep = 0;\n",
        HANDLE + "    public void Show()\n    {\n        _currentStep = 0;\n",
    ),
    (
        "AZERTYGlobal.Tests/CaptureBench.cs",
        "                                   () => CaptureConflict(outDir, theme, percent) })\n",
        "                                   () => CaptureConflict(outDir, theme, percent),\n"
        "                                   () => CaptureSettings(outDir, theme, percent),\n"
        "                                   () => CaptureOnboarding(outDir, theme, percent) })\n",
    ),
    (
        "AZERTYGlobal.Tests/CaptureBench.cs",
        "    private static bool CaptureLayers(string outDir, string theme, int percent)\n",
        BENCH_METHODS + "    private static bool CaptureLayers(string outDir, string theme, int percent)\n",
    ),
]


def apply(path, anchor, replacement):
    with open(path, "rb") as handle:
        data = handle.read()

    lf_anchor = anchor.encode("utf-8")
    crlf_anchor = anchor.replace("\n", "\r\n").encode("utf-8")

    lf_count = data.count(lf_anchor)
    crlf_count = data.count(crlf_anchor)
    if lf_count + crlf_count != 1:
        sys.exit(
            "%s : ancre trouvee %d fois en LF et %d fois en CRLF, attendu 1 au total"
            % (path, lf_count, crlf_count)
        )

    if lf_count:
        found, new = lf_anchor, replacement.encode("utf-8")
        ending = "LF"
    else:
        found = crlf_anchor
        new = replacement.replace("\n", "\r\n").encode("utf-8")
        ending = "CRLF"

    before_crlf = data.count(b"\r\n")
    before_lf = data.count(b"\n") - before_crlf
    patched = data.replace(found, new)
    after_crlf = patched.count(b"\r\n")
    after_lf = patched.count(b"\n") - after_crlf

    with open(path, "wb") as handle:
        handle.write(patched)

    print(
        "%-32s region %-4s | CRLF %d -> %d | LF %d -> %d | %+d octets"
        % (
            os.path.basename(path),
            ending,
            before_crlf,
            after_crlf,
            before_lf,
            after_lf,
            len(patched) - len(data),
        )
    )


def main():
    for name, anchor, replacement in REPLACEMENTS:
        apply(os.path.join(SRC, name.replace("/", os.sep)), anchor, replacement)


if __name__ == "__main__":
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8")
    main()
