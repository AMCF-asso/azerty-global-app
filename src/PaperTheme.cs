// Palette « papier » de la refonte 1.3.0 (direction Tablature, maquettes du 07/10).
namespace AZERTYGlobal;

/// <summary>
/// Les couleurs des quatre écrans refondus en 1.3.0 — Leçons, recherche de caractères,
/// clavier virtuel, Paramètres (COLORREF, 0x00BBGGRR). Ce sont les jetons de la charte du
/// site, repris des maquettes validées par Antoine le 07/10 : un jeton par rôle.
/// </summary>
static class PaperTheme
{
    /// <summary>Fond des fenêtres (--papier).</summary>
    internal const uint Paper = 0x00F1F8FA; // #FAF8F1
    /// <summary>Fond des surfaces posées sur le papier : tablature, liste, touches (--surface).</summary>
    internal const uint Surface = 0x00FFFFFF; // #FFFFFF
    /// <summary>Texte principal, traits « tenir » déjà joués (--encre).</summary>
    internal const uint Ink = 0x0013181B; // #1B1813
    /// <summary>Texte secondaire : étiquettes, légendes, mentions (--texte-2).</summary>
    internal const uint TextSecondary = 0x004A555B; // #5B554A
    /// <summary>Filets, contours des surfaces et des touches (--bordure).</summary>
    internal const uint Border = 0x00C3D2D9; // #D9D2C3
    /// <summary>Action, colonne en cours, geste à faire, liens, focus (--action).</summary>
    internal const uint Action = 0x00F23E1A; // #1A3EF2
    /// <summary>Ce qui reste à venir : caractères et gestes pas encore joués (--pale).</summary>
    internal const uint Pale = 0x0074848C; // #8C8474
    /// <summary>Touches de fonction (Tab, Maj, Entrée…).</summary>
    internal const uint FunctionKey = 0x00E2EDF1; // #F1EDE2
    /// <summary>Texte posé sur l'action.</summary>
    internal const uint OnAction = 0x00FFFFFF; // #FFFFFF
    /// <summary>Teinte de la colonne en cours et de la ligne choisie : l'action à 7 % sur la
    /// surface blanche (rgba(26,62,242,.07)).</summary>
    internal const uint ActionTint = 0x00FEF1EF; // #EFF1FE
    /// <summary>La même teinte posée sur le papier.</summary>
    internal const uint ActionTintOnPaper = 0x00F1EBEA; // #EAEBF1
    /// <summary>Contour d'une touche effacée du clavier virtuel, posée à même le papier : elle
    /// ne tape rien sur la couche tenue.</summary>
    internal const uint KeyFadedBorder = 0x00D3E0E6; // #E6E0D3
    /// <summary>Faute de frappe, refus.</summary>
    internal const uint Error = 0x001A26A3; // #A3261A
    /// <summary>Réussite.</summary>
    internal const uint Success = 0x000F7B0F; // #0F7B0F
}
