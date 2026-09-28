// Palette sombre des fenêtres sans contrôles — lot visuel 1.3.0 (QCM du 28/09).
namespace AZERTYGlobal;

/// <summary>
/// Les couleurs des fenêtres sombres : Leçons, tutoriel, clavier virtuel, Recherche,
/// indicateur de couche et notification de bascule (COLORREF, 0x00BBGGRR). Les fenêtres
/// gardent leurs noms locaux, qui renvoient ici.
///
/// Première étape du lot : chaque valeur est celle d'avant, au pixel près. Plusieurs jetons
/// portent encore un même rôle (un fond par fenêtre, par exemple) ; ils se fondent à
/// l'étape suivante.
/// </summary>
static class DarkTheme
{
    // ── Fonds ────────────────────────────────────────────────────
    /// <summary>Fond des Leçons et du clavier virtuel.</summary>
    internal const uint Background = 0x00201C18;
    /// <summary>Fond du tutoriel et de sa zone clavier.</summary>
    internal const uint BackgroundTutorial = 0x001A1A1A;
    /// <summary>Fond de la Recherche.</summary>
    internal const uint BackgroundSearch = 0x00282828;
    /// <summary>Fond de la notification de bascule.</summary>
    internal const uint BackgroundToast = 0x00302D2A;
    /// <summary>Fond de l'indicateur de couche.</summary>
    internal const uint BackgroundIndicator = 0x00352A20;
    /// <summary>Voile posé sur le tutoriel en pause.</summary>
    internal const uint Scrim = 0x00000000;

    // ── Surfaces ─────────────────────────────────────────────────
    /// <summary>Panneaux des Leçons.</summary>
    internal const uint Surface = 0x00282018;
    /// <summary>Panneau surélevé des Leçons : ligne choisie, zones de saisie.</summary>
    internal const uint SurfaceRaised = 0x00302820;
    /// <summary>Champ de la Recherche.</summary>
    internal const uint SurfaceSearchField = 0x00323232;
    /// <summary>Résultat choisi de la Recherche.</summary>
    internal const uint Selected = 0x00483828;

    // ── Bordures ─────────────────────────────────────────────────
    internal const uint Border = 0x00484038;
    internal const uint BorderSearch = 0x00404040;

    // ── Boutons ──────────────────────────────────────────────────
    internal const uint Control = 0x00484038;
    internal const uint ControlHover = 0x00585048;
    /// <summary>Boutons « Quitter » et « Passer » du tutoriel.</summary>
    internal const uint ControlTutorial = 0x002A2A2A;
    internal const uint ControlTutorialBorder = 0x00555555;
    internal const uint ControlTutorialHover = 0x003838C0;
    internal const uint ControlTutorialHoverBorder = 0x005050E0;

    // ── Textes ───────────────────────────────────────────────────
    internal const uint Text = 0x00F0EDE8;
    internal const uint TextStrong = 0x00FFFFFF;
    internal const uint TextSearch = 0x00DDDDDD;
    internal const uint TextTutorialTitle = 0x00E0E0E0;
    internal const uint TextSecondary = 0x00A8A098;
    internal const uint TextSecondaryTutorial = 0x00CCCCCC;
    internal const uint TextStatus = 0x00AAAAAA;
    internal const uint TextTertiary = 0x00808080;
    internal const uint TextHint = 0x00777777;
    internal const uint TextPlaceholder = 0x00999999;
    internal const uint TextToastInactive = 0x009A9A9A;
    /// <summary>Points d'exercices à faire du tutoriel.</summary>
    internal const uint ProgressTodo = 0x00606060;

    // ── Accent et états ──────────────────────────────────────────
    internal const uint Accent = 0x00D47800;
    /// <summary>Onglet actif et repère de module des Leçons.</summary>
    internal const uint AccentLessons = 0x000078D4;
    /// <summary>Caractère courant des Leçons.</summary>
    internal const uint AccentCurrent = 0x00D4A060;
    /// <summary>Caractère trouvé de la Recherche.</summary>
    internal const uint AccentSearch = 0x00FF9040;
    internal const uint Success = 0x005EC522;
    internal const uint SuccessLessons = 0x004CB050;
    internal const uint SuccessSearch = 0x0060D060;
    internal const uint Error = 0x004444EF;
    internal const uint ErrorLessons = 0x003232DC;
    /// <summary>Mention bonus du tutoriel.</summary>
    internal const uint Warning = 0x000094E2;
    /// <summary>Indicateur de couche verrouillée.</summary>
    internal const uint IndicatorLocked = 0x0060D8FF;

    // ── Clavier des Leçons et du tutoriel (KeyboardRenderer) ─────
    internal const uint Key = 0x003A3A3A;
    internal const uint KeyContext = 0x002D2D2D;
    internal const uint KeyBorder = 0x00555555;
    internal const uint KeyPressed = 0x006A4A2A;
    internal const uint KeyDisabled = 0x002A2A2A;
    internal const uint KeyDisabledText = 0x00606060;
    internal const uint KeyModifierActive = 0x009A5A1A;
    internal const uint KeyContextText = 0x00E0E0E0;
    /// <summary>Nom de la touche sous le résultat d'une touche morte armée.</summary>
    internal const uint KeyCaption = 0x0080D0F0;
    /// <summary>Caractère de la couche tenue.</summary>
    internal const uint KeyGlyphActive = 0x00FFB366;
    /// <summary>Caractère d'une couche non tenue.</summary>
    internal const uint KeyGlyphDim = 0x00999999;
    internal const uint KeyDeadKey = 0x006666FF;
    /// <summary>Ligne « touche morte active » sous le clavier des Leçons.</summary>
    internal const uint KeyDeadKeyStatus = 0x000080FF;
    internal const uint KeyCapsBar = 0x0000A5FF;
    internal const uint KeyBadgeText = 0x00FFFFFF;

    // ── Clavier virtuel ──────────────────────────────────────────
    internal const uint VirtualKey = 0x00484038;
    internal const uint VirtualKeyContext = 0x00383028;
    internal const uint VirtualKeyBorder = 0x00302820;
    internal const uint VirtualKeyPressed = 0x00D4A060;
    internal const uint VirtualKeyPressedText = 0x00201C18;
    internal const uint VirtualKeyGlyph = 0x00F0EDE8;
    internal const uint VirtualKeyDeadKey = 0x000080FF;
    internal const uint VirtualKeyContextText = 0x00B0A898;

    // ── Surlignage d'une méthode de frappe ───────────────────────
    /// <summary>Touche à presser directement.</summary>
    internal const uint HighlightDirect = 0x0064C800;
    internal const uint HighlightDirectFill = 0x00284018;
    /// <summary>Armement d'une touche morte, étape 1 d'une séquence.</summary>
    internal const uint HighlightStep1 = 0x0000A5FF;
    internal const uint HighlightStep1Fill = 0x00283020;
    /// <summary>Touche finale d'une séquence, étape 2.</summary>
    internal const uint HighlightStep2 = 0x004CB050;
    internal const uint HighlightStep2Fill = 0x00203818;
    /// <summary>Touche morte à activer (clavier virtuel).</summary>
    internal const uint HighlightDeadKey = 0x003232DC;
    internal const uint HighlightDeadKeyFill = 0x00282040;

    // ── Méthodes de la Recherche ─────────────────────────────────
    /// <summary>Mention « Maj ».</summary>
    internal const uint MethodShift = 0x006EAAF0;
    /// <summary>Mention « AltGr ».</summary>
    internal const uint MethodAltGr = 0x00E8A848;
}
