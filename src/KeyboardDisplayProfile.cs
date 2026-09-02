namespace AZERTYGlobal;

/// <summary>
/// Fond d'une touche au repos. Trois candidats rendus côte à côte par le banc
/// <c>KeyboardContextBench</c> pour l'arrêt visuel de CH4a (décision S4-4 du 2026-09-02) :
/// deux disparaissent dès qu'Antoine a tranché.
/// </summary>
internal enum KeyRestFill
{
    /// <summary>Le jeton <c>surface</c> tel quel — 1,15:1 sur le fond de fenêtre en sombre.</summary>
    Surface,

    /// <summary>Un jeton « touche » propre, calibré à 1,53:1 sur le fond en sombre (comme l'app 1.x).</summary>
    KeyFace,

    /// <summary>Le fond de fenêtre, la touche n'est délimitée que par son contour, renforcé.</summary>
    PaperBordered,
}

/// <summary>
/// Profil d'affichage du clavier : ce que le moteur <see cref="KeyboardRenderer"/> fait varier
/// sans changer sa signature ni ses cinq polices. Le produit n'en connaît qu'un,
/// <see cref="Default"/>, qui rend exactement ce que le moteur rendait avant CH4a ; le banc en
/// force d'autres par <see cref="OverrideForTests"/>, le temps d'une capture. Quand Antoine
/// aura tranché S4-2, S4-3 et S4-4, la valeur retenue de chaque axe devient le défaut et les
/// autres branches meurent.
/// </summary>
/// <param name="FontFollowsActiveLayer">
/// Vrai : le caractère qui sera tapé prend la grande police, quelle que soit sa position sur la
/// touche (levier mesuré au plan §18 : masse relative 0,41 → 3,51 quand AltGr est tenu).
/// Faux : la police suit la position, comme avant.
/// </param>
/// <param name="Highlight">
/// Table de surlignage appliquée quand l'état porte un <see cref="KeyboardRenderState.HighlightRole"/>.
/// Nul : peinture « enfoncée » d'avant CH4a, sans pastille — c'est le comportement du produit tant
/// que S4-3 n'est pas tranchée.
/// </param>
/// <param name="RestFill">Fond des touches au repos.</param>
internal sealed record KeyboardDisplayProfile(
    bool FontFollowsActiveLayer,
    HighlightScheme? Highlight,
    KeyRestFill RestFill)
{
    /// <summary>Le rendu du produit, identique à celui d'avant CH4a.</summary>
    internal static readonly KeyboardDisplayProfile Default = new(false, null, KeyRestFill.Surface);

    [ThreadStatic]
    private static KeyboardDisplayProfile? _override;

    /// <summary>Profil en vigueur : celui que le banc force, sinon <see cref="Default"/>.</summary>
    internal static KeyboardDisplayProfile Current => _override ?? Default;

    /// <summary>
    /// Force un profil jusqu'au Dispose, qui restaure le précédent. Même contrat que
    /// <see cref="Theme.OverrideForTests"/> : la restauration est portée par le scope, parce
    /// qu'un statique non remis à zéro traverse toute la suite dans un seul processus.
    /// </summary>
    internal static IDisposable OverrideForTests(KeyboardDisplayProfile profile)
    {
        var scope = new OverrideScope(_override);
        _override = profile;
        return scope;
    }

    /// <summary>Fond d'une touche ordinaire au repos, selon le candidat demandé.</summary>
    internal static uint RestFillColor(KeyRestFill fill, Palette p) => fill switch
    {
        KeyRestFill.KeyFace => KeyFace(p),
        KeyRestFill.PaperBordered => p.Paper,
        _ => p.Surface,
    };

    /// <summary>
    /// Contour d'une touche au repos. Le candidat « papier + bordure » ne tient que par son trait :
    /// il prend <c>texte-2</c> (≥ 3:1), ce que la règle des fondations exige d'un contrôle
    /// interactif — et une touche l'est. Les deux autres gardent <c>bordure</c>.
    /// </summary>
    internal static uint RestBorderColor(KeyRestFill fill, Palette p) =>
        fill == KeyRestFill.PaperBordered ? p.TextSecondary : p.Border;

    /// <summary>
    /// ⛔ Candidat S4-4, la seule nuance hors <see cref="Palette"/> du dossier clavier. En thème
    /// clair la touche reste <c>surface</c> (blanc sur papier, 1,06:1 — le trait la délimite) ;
    /// en sombre, #3E382F : 1,53:1 sur le fond, la valeur exacte que l'app 1.x posait
    /// (#3A3A3A sur #1A1A1A), dans la teinte chaude de la charte ; encre 10,9:1, texte-2 5,0:1,
    /// action 5,0:1. S'il est retenu à l'arrêt visuel, il entre dans <see cref="Palette"/> et
    /// au plan §6 ; sinon cette méthode meurt avec l'enum.
    /// </summary>
    private static uint KeyFace(Palette p) =>
        Theme.Variant == ThemeVariant.Dark && !Theme.IsHighContrast
            ? Theme.Rgb(0x3E, 0x38, 0x2F)
            : p.Surface;

    private sealed class OverrideScope : IDisposable
    {
        private readonly KeyboardDisplayProfile? _previous;
        private bool _disposed;

        internal OverrideScope(KeyboardDisplayProfile? previous) => _previous = previous;

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _override = _previous;
        }
    }
}
