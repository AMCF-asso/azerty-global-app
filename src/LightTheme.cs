// Palette claire des fenêtres à contrôles — audit du 25/09, lot 8 (F-14) ; lot visuel 1.3.0.
namespace AZERTYGlobal;

/// <summary>
/// Les couleurs d'À propos, Statistiques, Conflit, Accueil, Paramètres, Couches et Durée de
/// pause (COLORREF, 0x00BBGGRR) : un jeton par rôle. Les fenêtres gardent leurs noms locaux,
/// qui renvoient ici : une valeur ne se déclare qu'une fois.
///
/// Valeurs de la variante B « Ardoise » (lot visuel 1.3.0,
/// décisions d'Antoine du 28/09). Les
/// variantes ne changent que ces valeurs ; le rôle de chaque jeton ne bouge pas.
/// </summary>
static class LightTheme
{
    /// <summary>Fond des fenêtres.</summary>
    internal const uint Background = 0x00F7F4F2; // #F2F4F7
    /// <summary>Fond des cartes et panneaux (Paramètres, Accueil).</summary>
    internal const uint Card = 0x00FCFBFA; // #FAFBFC
    /// <summary>Contour des cartes, filets et séparateurs.</summary>
    internal const uint Border = 0x00E6E1DD; // #DDE1E6
    /// <summary>Contour d'un contrôle (champ, piste de progression) : 3:1 sur les fonds.</summary>
    internal const uint ControlBorder = 0x008A8A8A; // #8A8A8A
    /// <summary>Fond d'un champ (touches de raccourci des Paramètres).</summary>
    internal const uint Field = 0x00FFFFFF; // #FFFFFF
    /// <summary>Titres et corps de texte.</summary>
    internal const uint Text = 0x00211D1A; // #1A1D21
    /// <summary>Texte discret : version, mentions, notes.</summary>
    internal const uint TextSecondary = 0x006B615A; // #5A616B
    /// <summary>Liens, titres de section, mises en avant, barre de progression, focus.</summary>
    internal const uint Accent = 0x00B85F00; // #005FB8
    /// <summary>Lien survolé ou focalisé.</summary>
    internal const uint AccentHover = 0x00923E00; // #003E92
    /// <summary>Texte posé sur l'accent.</summary>
    internal const uint OnAccent = 0x00FFFFFF; // #FFFFFF
    /// <summary>Message de réussite.</summary>
    internal const uint Success = 0x000F7B0F; // #0F7B0F
    /// <summary>Message d'erreur, saisie refusée.</summary>
    internal const uint Error = 0x001C2BC4; // #C42B1C
}
