"""CH3 passe 2, lot B (3/3) — le banc rend les trois étapes de la fenêtre de bienvenue.

Le banc n'ouvrait que l'étape 1. Or les quatre rôles typographiques les plus gros —
`_hFontPageTitle`, `_hFontStepSummary`, `_hFontBannerBold`, `_hFontSection` — ne servent qu'aux
étapes 2 et 3 : la descente sur la charte les changeait sans que rien ne les montre. Même défaut
que la barre d'onglets de Paramètres, corrigé le même jour et pour la même raison.

`_step3Reached` est posé comme le fait la navigation : c'est lui qui fait afficher l'étape 3 son
contenu complet.

OnboardingWindow.cs est mixte ; CaptureBench.cs est en LF pur.
"""

import pathlib
import sys

sys.stdout.reconfigure(encoding="utf-8")

ROOT = pathlib.Path(__file__).resolve().parents[3]
ONBOARDING = ROOT / "src" / "OnboardingWindow.cs"
BENCH = ROOT / "src" / "AZERTYGlobal.Tests" / "CaptureBench.cs"

CRLF = b"\r\n"
LF = b"\n"


def patch(path, name, old_lines, new_lines, pure_lf=False):
    """Un fichier LF pur voit forcément son compte de LF changer quand le bloc grossit : c'est
    l'absence de CRLF qui s'y vérifie. Sur un fichier mixte, c'est l'inverse — aucun saut isolé
    ne doit bouger, aucune ligne n'étant supprimée ici."""
    data = path.read_bytes()
    crlf_before = data.count(CRLF)
    lf_before = data.count(LF) - crlf_before
    if pure_lf and crlf_before:
        sys.exit(f"{path.name} n'est plus en LF pur")

    for eol, label in ((CRLF, "CRLF"), (LF, "LF  ")):
        marker = eol.join(line.encode("utf-8") for line in new_lines)
        if data.count(marker) == 1:
            print(f"  {path.name:24s} {name:28s} déjà appliqué")
            return

        old = eol.join(line.encode("utf-8") for line in old_lines)
        found = data.count(old)
        if found == 1:
            data = data.replace(old, marker)
            if pure_lf:
                if data.count(CRLF):
                    sys.exit(f"{name} : des CRLF sont apparus dans un fichier LF pur")
            else:
                if data.count(LF) - data.count(CRLF) != lf_before:
                    sys.exit(f"{name} : le compte de sauts isolés a bougé")
            path.write_bytes(data)
            print(f"  {path.name:24s} {name:28s} {label}")
            return
        if found > 1:
            sys.exit(f"{name} : {found} occurrences en {label}, attendu 1")
    sys.exit(f"{name} : introuvable en CRLF comme en LF")


patch(
    ONBOARDING,
    "ShowStepForCapture",
    [
        "    private void RecreateFonts()",
    ],
    [
        "    /// <summary>",
        "    /// Pour le banc de captures : la fenêtre montre l'étape demandée. Le banc ne clique pas,",
        "    /// et sans ce point d'entrée son contrôle visuel ne verrait jamais que l'étape 1 — alors",
        "    /// que les quatre plus gros rôles typographiques ne servent qu'aux étapes 2 et 3.",
        "    /// </summary>",
        "    internal void ShowStepForCapture(int step)",
        "    {",
        "        _currentStep = Math.Clamp(step, 0, StepCountForCapture - 1);",
        "        if (_currentStep == 2)",
        "            _step3Reached = true;",
        "        UpdateStepVisibility();",
        "        Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);",
        "    }",
        "",
        "    /// <summary>Nombre d'étapes, pour le banc.</summary>",
        "    internal const int StepCountForCapture = 3;",
        "",
        "    private void RecreateFonts()",
    ],
)

patch(
    BENCH,
    "CaptureOnboarding en 3 étapes",
    [
        "            window.Show();",
        '            return Capture(window.Handle, Path.Combine(outDir, $"onboarding-{theme}-{percent}.png"));',
    ],
    [
        "            window.Show();",
        "            // Les trois étapes, un fichier chacune. Ne capturer que la première laissait les",
        "            // quatre plus gros rôles typographiques hors du contrôle visuel.",
        "            bool all = true;",
        "            for (int step = 0; step < OnboardingWindow.StepCountForCapture; step++)",
        "            {",
        "                window.ShowStepForCapture(step);",
        "                all &= Capture(window.Handle,",
        '                    Path.Combine(outDir, $"onboarding-etape{step + 1}-{theme}-{percent}.png"));',
        "            }",
        "            return all;",
    ],
    pure_lf=True,
)
