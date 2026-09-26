// Palette claire des fenêtres à contrôles — audit du 25/09, lot 8 (F-14).
namespace AZERTYGlobal;

/// <summary>
/// Les couleurs que À propos, Statistiques, Conflit, Accueil, Paramètres, Couches et Durée de
/// pause déclaraient chacune (COLORREF, 0x00BBGGRR). Les fenêtres gardent leurs noms locaux,
/// qui renvoient ici : une valeur ne se déclare plus qu'une fois.
///
/// Ne sont pas ici les couleurs propres à une fenêtre, ni les écarts relevés par l'audit, qui
/// changeraient un pixel et attendent la décision d'Antoine : survol des liens de l'accueil
/// (0x00FF9830 au lieu de <see cref="Highlight"/>) et ses filets en littéraux (0x00D0D0D0 au
/// lieu de <see cref="Separator"/>).
/// </summary>
static class LightTheme
{
    /// <summary>Fond des fenêtres.</summary>
    internal const uint Background = 0x00DDDDDD;
    /// <summary>Fond des cartes (Paramètres, Accueil).</summary>
    internal const uint Panel = 0x00EEEEEE;
    internal const uint PanelBorder = 0x00D1D1D1;
    internal const uint Title = 0x00201C18;
    internal const uint Text = 0x00333333;
    /// <summary>Texte discret : sous-titres, mentions.</summary>
    internal const uint Muted = 0x00666666;
    /// <summary>Plus discret encore : numéro de version, rappel de confidentialité.</summary>
    internal const uint Faint = 0x00888888;
    /// <summary>Liens et accents.</summary>
    internal const uint Accent = 0x00D47800;
    /// <summary>Lien survolé ou focalisé, mise en avant.</summary>
    internal const uint Highlight = 0x000078D4;
    internal const uint Separator = 0x00D7D7D7;
}
