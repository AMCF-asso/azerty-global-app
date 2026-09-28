// Palette claire des fenêtres à contrôles — audit du 25/09, lot 8 (F-14).
namespace AZERTYGlobal;

/// <summary>
/// Les couleurs que À propos, Statistiques, Conflit, Accueil, Paramètres, Couches et Durée de
/// pause déclaraient chacune (COLORREF, 0x00BBGGRR). Les fenêtres gardent leurs noms locaux,
/// qui renvoient ici : une valeur ne se déclare plus qu'une fois.
///
/// Lot visuel 1.3.0, première étape : les couleurs que les fenêtres claires déclaraient
/// encore en littéraux sont ici aussi, valeurs inchangées. Plusieurs jetons portent encore
/// un même rôle (trois filets, deux survols de lien) ; ils se fondent à l'étape suivante.
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
    /// <summary>Filet de l'accueil.</summary>
    internal const uint SeparatorOnboarding = 0x00D0D0D0;
    /// <summary>Filet entre deux lignes d'une carte de l'accueil.</summary>
    internal const uint RowSeparator = 0x00E3E3E3;
    /// <summary>Lien survolé de l'accueil.</summary>
    internal const uint LinkHoverOnboarding = 0x00FF9830;
    /// <summary>Texte posé sur l'accent (pastilles de l'accueil).</summary>
    internal const uint OnAccent = 0x00FFFFFF;
    /// <summary>Étapes à venir de la barre de progression de l'accueil.</summary>
    internal const uint ProgressTrack = 0x00C8C8C8;
    /// <summary>Fond d'un champ (touches de raccourci des Paramètres).</summary>
    internal const uint Field = 0x00FAFAFA;
    internal const uint FieldBorder = 0x00CBCBCB;
    internal const uint FieldBorderError = 0x00A8A8FF;
    /// <summary>Message de réussite.</summary>
    internal const uint Success = 0x00228B22;
    /// <summary>Message d'erreur, saisie refusée.</summary>
    internal const uint Error = 0x000000CC;
}
