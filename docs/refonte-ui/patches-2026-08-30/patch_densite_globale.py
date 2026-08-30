"""Densité globale de l'application — un seul nombre pour les sept fenêtres.

Demande d'Antoine du 2026-08-30 : les fenêtres sont trop grandes à l'écran comparées aux autres
applications. Sa première idée était de réintroduire un facteur de rétrécissement sur Onboarding,
c'est-à-dire `ONBOARDING_UI_SCALE = 0.75` que `bea44be` venait de supprimer. Refusé pour la raison
qui l'avait fait supprimer : un facteur par fenêtre rend le texte de cette fenêtre différent de
celui du reste de l'application, sans que rien ne le dise. Un facteur **global** ne pose pas ce
problème — c'est une densité, elle vaut pour tout le monde, et elle se lit à un seul endroit.

Ce que la densité touche : la **géométrie** seule — marges, hauteurs, rembourrages, anatomie des
contrôles. Les polices viennent de `Theme.Font(role, dpi)`, qui ne passe pas par `Scale` : le
texte garde sa taille. La rampe typographique est un arbitrage séparé.

**Propriété tenue par construction : à `Density = 1`, chaque expression rend l'entier qu'elle
rendait avant ce patch.** Les six fenêtres qui tronquent (`(int)(val * _dpiScale)`) continuent de
tronquer, celle qui divise en entiers continue de diviser, et les deux qui passent par
`ThemeControls.Scale` continuent d'arrondir. C'est ce qui rend la comparaison de densités
lisible : à 1,00 les captures sont identiques à celles d'aujourd'hui, et tout écart vient de la
densité, jamais d'un changement d'arrondi introduit au passage.

⚠️ Ce patch pose le mécanisme, il ne choisit pas la valeur. Le défaut reste 1,00.
"""

import pathlib
import re
import sys

sys.stdout.reconfigure(encoding="utf-8")

ROOT = pathlib.Path(__file__).resolve().parents[3]
SRC = ROOT / "src"


def patch(rel, remplacements):
    path = SRC / rel
    data = path.read_bytes()
    crlf_before = data.count(b"\r\n")
    lf_before = data.count(b"\n") - crlf_before
    text = data.decode("utf-8")

    for name, old, new, expected in remplacements:
        lines = old.replace("\r\n", "\n").split("\n")
        rx = re.compile(r"\r?\n".join(re.escape(l) for l in lines))
        matches = list(rx.finditer(text))
        if len(matches) != expected:
            sys.exit(f"{rel} / {name} : {len(matches)} occurrence(s), {expected} attendue(s)")
        out, cursor = [], 0
        for m in matches:
            eol = "\r\n" if "\r\n" in m.group(0) else "\n"
            out.append(text[cursor:m.start()])
            out.append(new.replace("\r\n", "\n").replace("\n", eol))
            cursor = m.end()
        out.append(text[cursor:])
        text = "".join(out)

    data = text.encode("utf-8")
    crlf_after = data.count(b"\r\n")
    lf_after = data.count(b"\n") - crlf_after
    path.write_bytes(data)
    print(f"  {rel:38s} CRLF {crlf_before}→{crlf_after}, LF {lf_before}→{lf_after}")


# ── Le mécanisme, dans ThemeControls ────────────────────────────────────────
patch("ThemeControls.cs", [(
    "Density et Scale",
    """    internal static int Scale(int value, int dpi)
    {
        if (dpi <= 0)
            dpi = 96;

        int scaled = (int)Math.Round(value * dpi / 96.0, MidpointRounding.AwayFromZero);
        return value > 0 ? Math.Max(1, scaled) : scaled;
    }""",
    """    internal static int Scale(int value, int dpi)
    {
        if (dpi <= 0)
            dpi = 96;

        int scaled = (int)Math.Round(value * dpi * Density / 96.0, MidpointRounding.AwayFromZero);
        return value > 0 ? Math.Max(1, scaled) : scaled;
    }

    /// <summary>
    /// Densité de l'application : un facteur global sur la **géométrie**, jamais sur le texte.
    ///
    /// Les polices viennent de <c>Theme.Font(role, dpi)</c>, qui ne passe pas par cette classe :
    /// baisser la densité resserre les marges, les hauteurs et l'anatomie des contrôles sans
    /// toucher à la taille des caractères. C'est la différence avec <c>ONBOARDING_UI_SCALE</c>,
    /// le facteur 0,75 par fenêtre supprimé le 2026-08-30, qui multipliait aussi les polices et
    /// rendait l'accueil illisible par rapport au reste de l'application.
    ///
    /// À 1, valeur par défaut, chaque expression du dépôt rend l'entier qu'elle rendait avant que
    /// ce champ existe.
    /// </summary>
    internal static float Density { get; private set; } = 1.0f;

    /// <summary>
    /// Force la densité jusqu'au <c>Dispose</c>, qui restaure la précédente. Sert au banc de
    /// captures, comme <c>ThemeWindow.OverrideDpiForTests</c> : rendre la matrice à plusieurs
    /// densités sans toucher aux réglages du poste.
    /// </summary>
    internal static IDisposable OverrideDensityForTests(float density)
    {
        var scope = new DensityScope(Density);
        Density = density <= 0 ? 1.0f : density;
        return scope;
    }

    private sealed class DensityScope : IDisposable
    {
        private readonly float _previous;
        private bool _disposed;

        internal DensityScope(float previous) => _previous = previous;

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            Density = _previous;
        }
    }""",
    1,
)])

# ── Les six fenêtres qui tronquent sur un float ─────────────────────────────
TRONQUE = [
    ("LayoutConflictWindow.cs", "    private int S(int val) => (int)(val * _dpiScale);"),
    ("LearningModule.cs", "    private int S(int val) => (int)(val * _dpiScale);"),
    ("OnboardingWindow.cs", "    private int S(int val) => (int)(val * _dpiScale);"),
    ("SettingsWindow.cs", "    private int S(int val) => (int)(val * _dpiScale);"),
    ("ToggleNotification.cs", "    private int S(int v) => (int)(v * _dpiScale);"),
    ("UsageStatsWindow.cs", "    private int S(int val) => (int)(val * _dpiScale);"),
]
for rel, line in TRONQUE:
    patch(rel, [(
        "S() suit la densite",
        line,
        line.replace("* _dpiScale)", "* _dpiScale * ThemeControls.Density)"),
        1,
    )])

# ── Les deux formes restantes ───────────────────────────────────────────────
# La multiplication passe en flottant, la division reste entière : c'est ce qui garantit
# l'identité à Density = 1. Diviser en flottant puis tronquer ferait sortir 8 d'un 9 exact dès
# que la division rend 8,999999 en simple précision.
patch("MaintainableLayersWindow.Theme.cs", [(
    "S() suit la densite",
    "    private int S(int value) => value * _dpi / 96;",
    "    private int S(int value) => (int)(value * _dpi * ThemeControls.Density) / 96;",
    1,
)])

patch("LessonsWindow.cs", [(
    "S() suit la densite",
    "    private int S(int value) => (int)Math.Round(value * _dpiScale * _windowScale);",
    "    private int S(int value) => (int)Math.Round(value * _dpiScale * _windowScale * ThemeControls.Density);",
    1,
)])

print("\nAboutWindow et PauseDurationDialog passent déjà par ThemeControls.Scale : rien à patcher.")
