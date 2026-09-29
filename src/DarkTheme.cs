// Palette sombre des fenêtres sans contrôles — lot visuel 1.3.0 (QCM du 28/09).
namespace AZERTYGlobal;

/// <summary>
/// Une seule palette pour les fenêtres sombres : Leçons, tutoriel, clavier virtuel,
/// Recherche, indicateur de couche et notification de bascule (COLORREF, 0x00BBGGRR). Les
/// fenêtres gardent leurs noms locaux, qui renvoient ici.
///
/// Valeurs de la base commune, celles de la variante A « Windows 11 » (lot visuel 1.3.0,
/// décisions d'Antoine du 28/09). Les
/// variantes ne changent que ces valeurs. Les couleurs du clavier gardent la teinte d'avant,
/// éclaircie au besoin pour tenir 4,5:1 sur la touche ; texte et accent tiennent 4,5:1 sur
/// chaque fond où ils s'écrivent.
/// </summary>
static class DarkTheme
{
    // ── Fonds ───────────────────────────────────────────────────────
    /// <summary>Fond des fenêtres sombres.</summary>
    internal const uint Background = 0x00202020; // #202020
    /// <summary>Panneaux, cartes, touches de contexte.</summary>
    internal const uint Surface = 0x002B2B2B; // #2B2B2B
    /// <summary>Panneau surélevé : ligne choisie, zone de saisie, infobulle.</summary>
    internal const uint SurfaceRaised = 0x00313131; // #313131
    /// <summary>Ligne choisie d'une liste (Recherche).</summary>
    internal const uint Selected = 0x004D432D; // #2D434D
    /// <summary>Voile posé sur le tutoriel en pause.</summary>
    internal const uint Scrim = 0x00000000; // #000000

    // ── Bordures ────────────────────────────────────────────────────
    /// <summary>Contours et séparateurs.</summary>
    internal const uint Border = 0x00454545; // #454545

    // ── Boutons ─────────────────────────────────────────────────────
    /// <summary>Bouton au repos.</summary>
    internal const uint ControlFill = 0x00313131; // #313131
    /// <summary>Bouton survolé.</summary>
    internal const uint ControlHover = 0x00373737; // #373737
    /// <summary>Bouton enfoncé.</summary>
    internal const uint ControlPressed = 0x002B2B2B; // #2B2B2B
    /// <summary>Contour d'un bouton.</summary>
    internal const uint ControlBorder = 0x00454545; // #454545

    // ── Textes ──────────────────────────────────────────────────────
    /// <summary>Texte principal.</summary>
    internal const uint Text = 0x00FFFFFF; // #FFFFFF
    /// <summary>Texte secondaire : consignes, statut, mentions.</summary>
    internal const uint TextSecondary = 0x00C5C5C5; // #C5C5C5
    /// <summary>Texte tertiaire : cible à venir, couche non tenue, séparateurs.</summary>
    internal const uint TextTertiary = 0x00ACACAC; // #ACACAC
    /// <summary>Texte d'un élément désactivé.</summary>
    internal const uint TextDisabled = 0x00707070; // #707070

    // ── Accent et états ─────────────────────────────────────────────
    /// <summary>Accent : onglet actif, bouton principal, repères, couche tenue.</summary>
    internal const uint Accent = 0x00FFCD60; // #60CDFF
    /// <summary>Bouton principal survolé.</summary>
    internal const uint AccentHover = 0x00E9BC5A; // #5ABCE9
    /// <summary>Texte d'accent sur fond sombre.</summary>
    internal const uint AccentText = 0x00FFEB99; // #99EBFF
    /// <summary>Texte posé sur l'accent et sur les pastilles.</summary>
    internal const uint OnAccent = 0x00000000; // #000000
    /// <summary>Réussite : frappe juste, bascule activée.</summary>
    internal const uint Success = 0x005FCB6C; // #6CCB5F
    /// <summary>Erreur : frappe fausse.</summary>
    internal const uint Error = 0x00A499FF; // #FF99A4
    /// <summary>Mise en garde : touche morte active, Verr. Maj., bonus, couche verrouillée.</summary>
    internal const uint Warning = 0x0000A5FF; // #FFA500

    // ── Clavier ─────────────────────────────────────────────────────
    /// <summary>Touche.</summary>
    internal const uint Key = 0x00373737; // #373737
    /// <summary>Touche de contexte (Tab, Maj, Ctrl…).</summary>
    internal const uint KeyContext = 0x002B2B2B; // #2B2B2B
    /// <summary>Touche enfoncée, modificateur tenu.</summary>
    internal const uint KeyPressed = 0x004F442D; // #2D444F
    /// <summary>Touche désactivée (Retour arrière du tutoriel).</summary>
    internal const uint KeyDisabled = 0x00262626; // #262626
    /// <summary>Touche morte : son caractère, et son contour à armer (clavier virtuel).</summary>
    internal const uint DeadKey = 0x00878BFF; // #FF8B87
    /// <summary>Fond d'une touche morte surlignée.</summary>
    internal const uint DeadKeyFill = 0x00494963; // #634949
    /// <summary>Mention « Maj » d'une méthode de frappe.</summary>
    internal const uint Shift = 0x006EAAF0; // #F0AA6E
    /// <summary>Mention « AltGr » d'une méthode de frappe.</summary>
    internal const uint AltGr = 0x00F4B454; // #54B4F4

    // ── Surlignage d'une méthode de frappe ──────────────────────────
    /// <summary>Touche à presser directement.</summary>
    internal const uint HighlightDirect = 0x0064C800; // #00C864
    /// <summary>Fond d'un modificateur tenu surligné « direct ».</summary>
    internal const uint HighlightDirectFill = 0x0041572B; // #2B5741
    /// <summary>Armement d'une touche morte, étape 1 d'une séquence.</summary>
    internal const uint HighlightStep1 = 0x0000A5FF; // #FFA500
    /// <summary>Fond d'un modificateur tenu à l'étape 1.</summary>
    internal const uint HighlightStep1Fill = 0x002B4F63; // #634F2B
    /// <summary>Touche finale d'une séquence, étape 2.</summary>
    internal const uint HighlightStep2 = 0x005CC161; // #61C15C
    /// <summary>Fond d'un modificateur tenu à l'étape 2.</summary>
    internal const uint HighlightStep2Fill = 0x003F5540; // #40553F
}
