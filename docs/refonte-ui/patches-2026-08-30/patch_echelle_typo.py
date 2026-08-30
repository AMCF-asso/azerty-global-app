"""Échelle typographique globale — le second levier, symétrique de la densité.

Demande d'Antoine du 2026-08-30, après avoir vu les planches de densité : réduire la taille du
texte pour réduire celle des fenêtres. C'est le levier qui manque à la densité, et il agit sur
l'autre moitié du parc : la densité rétrécit ce qui est piloté par des constantes, l'échelle
typographique rétrécit ce qui est mesuré sur le texte.

⚠️ Elle ne fera rien sur Onboarding tant que sa hauteur restera `BASE_WIN_H = 763`, une constante.
Réduire son texte y laisse la fenêtre identique avec plus de vide dedans. C'est un fait de
structure, pas un réglage — Paramètres a reçu la mesure de son contenu à `3d158aa`, Onboarding ne
l'a pas.

Le facteur entre dans la **clé du cache de polices**. Sans cela, deux échelles rendues dans le
même processus se serviraient l'une dans les polices de l'autre, et le banc rendrait la seconde
matrice avec les polices de la première sans que rien ne le signale.

Le défaut reste 1,00, et `ThemeTests.EchelleTypographique_EstCelleDeLaCharte` continue donc de
vérifier la rampe telle qu'elle est écrite.
"""

import pathlib
import sys

sys.stdout.reconfigure(encoding="utf-8")

ROOT = pathlib.Path(__file__).resolve().parents[3]


def patch(rel, remplacements):
    path = ROOT / rel
    data = path.read_bytes()
    if data.count(b"\r\n"):
        sys.exit(f"{rel} n'est plus en LF pur, ce patch ne sait pas le traiter")
    text = data.decode("utf-8")
    for name, old, new in remplacements:
        if text.count(old) != 1:
            sys.exit(f"{rel} / {name} : {text.count(old)} occurrence(s), 1 attendue\n---\n{old}\n---")
        text = text.replace(old, new)
        print(f"  {rel:44s} {name}")
    path.write_bytes(text.encode("utf-8"))


patch("src/Theme.cs", [
    (
        "TypeScale dans la cle du cache",
        """    private static readonly Dictionary<(FontRole Role, int Dpi, bool Underlined), IntPtr> FontCache = new();""",
        """    private static readonly Dictionary<(FontRole Role, int Dpi, bool Underlined, float TypeScale), IntPtr> FontCache = new();""",
    ),
    (
        "TypeScale dans la cle et la hauteur",
        """        var key = (role, dpi, underlined);
        if (FontCache.TryGetValue(key, out var existing))
            return existing;

        var (size, weight, face) = Metrics(role);
        int height = -(int)Math.Round(size * dpi / 96.0, MidpointRounding.AwayFromZero);""",
        """        // L'échelle entre dans la clé : deux échelles rendues dans le même processus se
        // serviraient sinon l'une dans les polices de l'autre.
        var key = (role, dpi, underlined, TypeScale);
        if (FontCache.TryGetValue(key, out var existing))
            return existing;

        var (size, weight, face) = Metrics(role);
        int height = -(int)Math.Round(size * TypeScale * dpi / 96.0, MidpointRounding.AwayFromZero);""",
    ),
    (
        "TypeScale et son override",
        """    private const string SegoeUi = "Segoe UI";""",
        """    /// <summary>
    /// Échelle typographique de l'application : un facteur global sur la **taille du texte**,
    /// jamais sur la géométrie. Son pendant est <see cref="ThemeControls.Density"/>, qui fait
    /// l'inverse.
    ///
    /// Les deux leviers ne touchent pas les mêmes fenêtres. Celles qui posent leurs dimensions en
    /// constantes — Onboarding, Statistiques, À propos — ne bougent qu'avec la densité. Celle qui
    /// mesure son contenu — Paramètres depuis 3d158aa — ne bouge qu'avec l'échelle typographique.
    ///
    /// À 1, valeur par défaut, la rampe est celle que <c>Metrics</c> écrit, et le garde
    /// <c>ThemeTests.EchelleTypographique_EstCelleDeLaCharte</c> la vérifie telle quelle.
    /// </summary>
    internal static float TypeScale { get; private set; } = 1.0f;

    /// <summary>
    /// Force l'échelle typographique jusqu'au <c>Dispose</c>, qui restaure la précédente. Même
    /// crochet que <c>ThemeWindow.OverrideDpiForTests</c>, pour le même usage : le banc.
    /// </summary>
    internal static IDisposable OverrideTypeScaleForTests(float scale)
    {
        var scope = new TypeScaleScope(TypeScale);
        TypeScale = scale <= 0 ? 1.0f : scale;
        return scope;
    }

    private sealed class TypeScaleScope : IDisposable
    {
        private readonly float _previous;
        private bool _disposed;

        internal TypeScaleScope(float previous) => _previous = previous;

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            TypeScale = _previous;
        }
    }

    private const string SegoeUi = "Segoe UI";""",
    ),
])

patch("src/AZERTYGlobal.Tests/CaptureBench.cs", [
    (
        "lecture de l'echelle typo",
        """    private static string DensitySuffix =>
        Math.Abs(Density - 1.0f) < 0.001f ? "" : $"-d{(int)Math.Round(Density * 100)}";""",
        """    private static string DensitySuffix =>
        Math.Abs(Density - 1.0f) < 0.001f ? "" : $"-d{(int)Math.Round(Density * 100)}";

    /// <summary>Échelle typographique demandée, 1 par défaut. Voir <see cref="Theme.TypeScale"/>.</summary>
    private static float TypeScale
    {
        get
        {
            string? raw = Environment.GetEnvironmentVariable("AZERTY_CAPTURE_TYPE");
            if (string.IsNullOrWhiteSpace(raw))
                return 1.0f;
            return float.TryParse(raw, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float value) && value > 0
                ? value
                : 1.0f;
        }
    }

    /// <summary>Suffixe de nom de fichier de l'échelle typographique : vide à 1, <c>-t85</c> à 0,85.</summary>
    private static string TypeSuffix =>
        Math.Abs(TypeScale - 1.0f) < 0.001f ? "" : $"-t{(int)Math.Round(TypeScale * 100)}";""",
    ),
    (
        "portee de l'override",
        """                    using (ThemeControls.OverrideDensityForTests(Density))""",
        """                    using (ThemeControls.OverrideDensityForTests(Density))
                    using (Theme.OverrideTypeScaleForTests(TypeScale))""",
    ),
])

# Le suffixe typo s'ajoute au suffixe de densité sur les sept noms.
bench = ROOT / "src/AZERTYGlobal.Tests/CaptureBench.cs"
text = bench.read_bytes().decode("utf-8")
n = text.count("{DensitySuffix}.png")
if n != 7:
    sys.exit(f"{n} noms de fichier portent DensitySuffix, 7 attendus")
text = text.replace("{DensitySuffix}.png", "{DensitySuffix}{TypeSuffix}.png")
bench.write_bytes(text.encode("utf-8"))
print(f"  {'CaptureBench.cs':44s} 7 noms portent les deux suffixes")
