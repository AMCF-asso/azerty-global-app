// Socle de thème — refonte graphique v1.2.0, chantier CH0.
//
// Avant ce fichier, l'application ne portait aucun système de style : 167 constantes CLR_ pour
// 76 couleurs uniques réparties dans 12 fenêtres, sept corps clairs et cinq sombres, et aucune
// lecture du thème de Windows. Une retouche transverse coûtait treize chantiers, et deux
// fenêtres voisines n'avaient pas la même identité.
//
// Ce fichier est la seule source de couleur de l'application. Les treize jetons viennent de la
// charte du site — mêmes valeurs dans les deux thèmes, ratios WCAG déjà prouvés par les
// fondations — plus action-fond, calculé le 2026-08-28 par la même méthode. La charte fait foi
// en cas d'écart :
// Keyboard Layouts/projects/azerty-global/operations/refonte-app/2026-08-28-audit-refonte-ui.md
//
// Trois règles dures, chacune tenue par un test de ThemeTests :
//
//   1. Aucun littéral 0x00BBGGRR n'est écrit à la main. GDI attend un COLORREF, dont les octets
//      sont en ordre inverse de la notation hexadécimale usuelle — et c'est exactement ainsi
//      qu'est né l'orange #D47800 de l'application actuelle, qui n'est rien d'autre que le bleu
//      Windows #0078D4 à octets inversés, devenu accent de fait par accident. Rgb() est le seul
//      constructeur, et il prend ses trois octets dans l'ordre où on les lit.
//   2. Aucune couleur n'existe hors de ces deux tables. Une nuance absente se demande à Antoine,
//      elle ne s'invente pas. Les deux paires « On… » ne font pas exception : elles ne portent
//      aucune valeur propre, seulement le jeton de la même palette que la charte désigne comme
//      texte lisible sur le fond correspondant.
//   3. En mode Contraste élevé de Windows, aucun jeton custom ne s'applique : la palette entière
//      bascule sur les couleurs système. Un thème à contraste élevé est un réglage d'accessibilité
//      que l'utilisateur a choisi ; le repeindre aux couleurs du produit revient à le désactiver.
//
// L'application suit le thème de Windows et bascule à chaud. Le suivi tient en trois pièces :
// Refresh() relit l'état du système, Changed prévient les fenêtres, et les caches GDI ci-dessous
// n'ont rien à invalider puisqu'ils sont indexés par couleur et non par thème.

using System.Runtime.InteropServices;

namespace AZERTYGlobal;

/// <summary>
/// Variante de thème effectivement peinte. Elle suit le réglage « Mode Application » de Windows
/// (AppsUseLightTheme), qui est distinct du réglage de la barre des tâches — celui-là ne sert
/// qu'à l'icône de notification, voir <see cref="Theme.TaskbarVariant"/>.
/// </summary>
internal enum ThemeVariant
{
    /// <summary>Ivoire du site. Défaut de Windows, et repli quand l'état système est illisible.</summary>
    Light,

    /// <summary>Négatif chaud calculé pour le site le 2026-08-27.</summary>
    Dark,
}

/// <summary>
/// Rôles typographiques de l'application. Segoe UI pour l'interface, Consolas pour le technique :
/// ce sont les replis système officiels des fondations du site, et aucune police n'est embarquée
/// — la serif éditoriale reste au site.
///
/// Les tailles sont candidates jusqu'au chantier CH1 : elles se figent sur les deux fenêtres
/// témoins, à l'échelle réelle du poste, pas sur un tableau.
/// </summary>
internal enum FontRole
{
    /// <summary>Corps de texte. 15 pt, graisse 400.</summary>
    Body,

    /// <summary>Secondaire, légendes, sous-étiquettes. 13 pt, graisse 400.</summary>
    Secondary,

    /// <summary>Corps mis en avant. 15 pt, graisse 600 — la même taille que le corps, ce
    /// qui n'ajoute rien à l'échelle typographique arrêtée à CH1 : seule la graisse
    /// change. Les statistiques en ont besoin pour leurs lignes saillantes, que la charte
    /// rendait jusque-là en titre de section, deux crans trop gros.</summary>
    BodyStrong,

    /// <summary>Titre de section. 18 pt, graisse 600.</summary>
    SectionTitle,

    /// <summary>Titre de fenêtre dessiné dans le corps, pas la barre système. 24 pt, graisse 600.</summary>
    WindowTitle,

    /// <summary>
    /// Titre d'accueil, plus grand que celui d'une fenêtre ordinaire. 26 px, graisse 700.
    ///
    /// Septième rôle, ajouté le 2026-08-30 sur arbitrage d'Antoine, et le seul depuis que
    /// l'échelle a été figée à CH1. Motif : la fenêtre de bienvenue portait deux titres à 28 et
    /// 26 px pour 700 de graisse ; les faire tomber sur WindowTitle (24/600) leur retirait le
    /// poids qui distingue l'accueil du reste de l'application. Une taille de plus, pas une
    /// famille : c'est le même Segoe UI, et rien d'autre ne l'emploie.
    /// </summary>
    Display,

    /// <summary>Chiffre de statistique. 28 pt, graisse 600.</summary>
    StatNumber,

    /// <summary>Technique : versions, empreintes. Consolas 14 pt, graisse 400.</summary>
    Mono,
}

/// <summary>
/// Les treize jetons de la charte, plus les deux couples de lisibilité. Un enregistrement plutôt
/// qu'une classe : deux palettes se comparent alors par valeur, ce dont les tests se servent pour
/// prouver qu'une bascule de thème change bien quelque chose, et qu'un override de test restaure
/// exactement ce qu'il avait trouvé.
///
/// Toutes les valeurs sont des COLORREF prêts pour GDI, construits par <see cref="Theme.Rgb"/>.
/// </summary>
/// <param name="Paper">Fond de fenêtre. « papier » au thème clair, « fond » au sombre.</param>
/// <param name="Surface">Cartes, champs, touches de clavier.</param>
/// <param name="Ink">Texte principal, lettres des touches.</param>
/// <param name="TextSecondary">Texte secondaire, sous-étiquettes AltGr. Aussi la bordure de tout
/// contrôle interactif : la charte exige 3:1 minimum pour une bordure porteuse d'état, ce que
/// <paramref name="Border"/> n'atteint pas.</param>
/// <param name="Border">Filets et bordures décoratives, cartes et panneaux. À 1,4-1,7:1 elle
/// n'est jamais seule porteuse d'un état.</param>
/// <param name="Action">Liens, contrôles actifs, anneau de focus.</param>
/// <param name="Success">Précision, « activé ».</param>
/// <param name="Warning">Alertes douces, catégorie AltGr.</param>
/// <param name="Error">Touche en erreur, échecs.</param>
/// <param name="SuccessFill">Fond de carte de résultat réussi.</param>
/// <param name="WarningFill">Fond de message d'avertissement.</param>
/// <param name="ErrorFill">Fond de message d'erreur.</param>
/// <param name="ActionFill">Sélection de la recherche, item actif de la barre latérale des
/// leçons, toggle actif, états de touche. Calculé le 2026-08-28.</param>
/// <param name="OnAction">Texte posé sur <paramref name="Action"/> — le bouton primaire. Ce n'est
/// pas une couleur de plus : au thème clair c'est <paramref name="Surface"/> (6,96:1), au sombre
/// c'est <paramref name="Paper"/> (7,64:1). L'asymétrie est celle du site, elle n'est pas un
/// oubli.</param>
/// <param name="Disabled">Texte et bordure d'un contrôle inactif. Vaut
/// <paramref name="TextSecondary"/> dans les deux thèmes de la charte — ce n'est pas une
/// couleur de plus. Il existe pour le mode Contraste élevé : le schéma système y confond
/// Surface avec Paper et le texte secondaire avec l'encre, si bien qu'un contrôle désactivé
/// perdrait à la fois sa différence de fond et sa différence de texte. COLOR_GRAYTEXT est la
/// seule couleur qu'un tel schéma réserve à l'inactif.</param>
/// <param name="OnActionFill">Texte posé sur <paramref name="ActionFill"/>. Toujours
/// <paramref name="Ink"/> : 14,60:1 au clair, 14,45:1 au sombre. Il existe parce qu'en Contraste
/// élevé, ActionFill devient la couleur de sélection du système, dont le texte lisible est
/// COLOR_HIGHLIGHTTEXT et non COLOR_WINDOWTEXT — peindre Ink par-dessus rendrait la sélection
/// illisible sur certains schémas.</param>
internal sealed record Palette(
    uint Paper,
    uint Surface,
    uint Ink,
    uint TextSecondary,
    uint Border,
    uint Action,
    uint Success,
    uint Warning,
    uint Error,
    uint SuccessFill,
    uint WarningFill,
    uint ErrorFill,
    uint ActionFill,
    uint OnAction,
    uint OnActionFill,
    uint Disabled);

/// <summary>
/// Palette courante, caches GDI et suivi du thème de Windows.
///
/// Rien ici n'est protégé par un verrou, à dessein : l'application n'a qu'un fil, celui de la
/// boucle de messages ouverte par Program.Main sous [STAThread], et toutes les fenêtres y sont
/// créées et peintes. Un cache verrouillé donnerait l'illusion qu'un autre fil peut peindre.
/// </summary>
static class Theme
{
    // ═══════════════════════════════════════════════════════════════
    // Construction de couleur
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Seul constructeur de couleur de l'application. Prend les trois octets dans l'ordre où on
    /// les lit dans la charte — Rgb(0xFA, 0xF8, 0xF1) pour #FAF8F1 — et rend le COLORREF que GDI
    /// attend, soit 0x00BBGGRR.
    ///
    /// Écrire ce COLORREF à la main est interdit par la charte, et pour une raison mesurée : les
    /// onze occurrences de l'orange #D47800 de l'application actuelle sont le bleu #0078D4 dont
    /// quelqu'un a recopié les octets sans les inverser. Personne ne l'a vu pendant des mois
    /// parce qu'un octet inversé donne toujours une couleur valide.
    /// </summary>
    internal static uint Rgb(byte r, byte g, byte b) => (uint)(r | (g << 8) | (b << 16));

    // ═══════════════════════════════════════════════════════════════
    // Les deux tables de la charte
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Thème clair — ivoire du site.</summary>
    internal static Palette LightPalette { get; } = new(
        Paper: Rgb(0xFA, 0xF8, 0xF1),
        Surface: Rgb(0xFF, 0xFF, 0xFF),
        Ink: Rgb(0x1B, 0x18, 0x13),
        TextSecondary: Rgb(0x5B, 0x55, 0x4A),
        Border: Rgb(0xD9, 0xD2, 0xC3),
        Action: Rgb(0x1A, 0x3E, 0xF2),
        Success: Rgb(0x18, 0x63, 0x39),
        Warning: Rgb(0x8A, 0x52, 0x00),
        Error: Rgb(0xB0, 0x2A, 0x1E),
        SuccessFill: Rgb(0xE9, 0xF2, 0xEA),
        WarningFill: Rgb(0xF7, 0xED, 0xDC),
        ErrorFill: Rgb(0xF9, 0xE9, 0xE6),
        ActionFill: Rgb(0xE3, 0xE9, 0xFC),
        // Blanc sur bleu : 6,96:1. C'est Surface, pas une quatorzième couleur.
        OnAction: Rgb(0xFF, 0xFF, 0xFF),
        // Encre sur action-fond : 14,60:1.
        OnActionFill: Rgb(0x1B, 0x18, 0x13),
        // Texte-2, tel quel : 6,95:1 sur le papier.
        Disabled: Rgb(0x5B, 0x55, 0x4A));

    /// <summary>Thème sombre — négatif chaud calculé pour le site le 2026-08-27.</summary>
    internal static Palette DarkPalette { get; } = new(
        Paper: Rgb(0x1B, 0x18, 0x13),
        Surface: Rgb(0x24, 0x1F, 0x17),
        Ink: Rgb(0xFA, 0xF8, 0xF1),
        TextSecondary: Rgb(0xB3, 0xA9, 0x96),
        Border: Rgb(0x45, 0x3D, 0x30),
        Action: Rgb(0x8F, 0xA6, 0xFF),
        Success: Rgb(0x6F, 0xBF, 0x8B),
        Warning: Rgb(0xD9, 0xA0, 0x45),
        Error: Rgb(0xE8, 0x80, 0x70),
        SuccessFill: Rgb(0x1E, 0x2A, 0x20),
        WarningFill: Rgb(0x2C, 0x22, 0x12),
        ErrorFill: Rgb(0x2D, 0x1A, 0x16),
        ActionFill: Rgb(0x1F, 0x24, 0x38),
        // Encre du thème clair sur le bleu clair : 7,64:1. C'est Paper de cette même palette.
        // Poser Ink ici — la crème — donnerait 1,4:1, soit un bouton primaire illisible.
        OnAction: Rgb(0x1B, 0x18, 0x13),
        // Encre sur action-fond : 14,45:1.
        OnActionFill: Rgb(0xFA, 0xF8, 0xF1),
        // Texte-2, tel quel : 7,61:1 sur le fond.
        Disabled: Rgb(0xB3, 0xA9, 0x96));

    /// <summary>Palette d'une variante. Fonction pure — c'est elle que les tests éprouvent.</summary>
    internal static Palette ForVariant(ThemeVariant variant) =>
        variant == ThemeVariant.Dark ? DarkPalette : LightPalette;

    /// <summary>
    /// Palette du mode Contraste élevé : aucun jeton custom, rien que des couleurs système. Le
    /// lecteur est injecté pour que les tests éprouvent la table de correspondance sans dépendre
    /// du schéma posé sur le poste, ni avoir à en activer un.
    ///
    /// Deux choix méritent d'être dits. TextSecondary rend COLOR_WINDOWTEXT et non COLOR_GRAYTEXT :
    /// en contraste élevé, le gris est la couleur du désactivé, et un texte secondaire peint avec
    /// se lirait comme un contrôle inerte. Les trois couleurs sémantiques rendent elles aussi
    /// COLOR_WINDOWTEXT : un schéma à contraste élevé n'a pas de vert de succès ni de rouge
    /// d'erreur, et la charte veut de toute façon que le texte porte l'information et que la
    /// couleur ne fasse que la renforcer — ici elle ne renforce plus rien, et c'est tout.
    /// </summary>
    internal static Palette HighContrastPalette(Func<int, uint> systemColor)
    {
        uint window = systemColor(Win32.COLOR_WINDOW);
        uint windowText = systemColor(Win32.COLOR_WINDOWTEXT);

        return new Palette(
            Paper: window,
            Surface: window,
            Ink: windowText,
            TextSecondary: windowText,
            Border: systemColor(Win32.COLOR_WINDOWFRAME),
            Action: systemColor(Win32.COLOR_HOTLIGHT),
            Success: windowText,
            Warning: windowText,
            Error: windowText,
            SuccessFill: window,
            WarningFill: window,
            ErrorFill: window,
            ActionFill: systemColor(Win32.COLOR_HIGHLIGHT),
            OnAction: systemColor(Win32.COLOR_HIGHLIGHTTEXT),
            OnActionFill: systemColor(Win32.COLOR_HIGHLIGHTTEXT),
            Disabled: systemColor(Win32.COLOR_GRAYTEXT));
    }

    // ═══════════════════════════════════════════════════════════════
    // État courant
    // ═══════════════════════════════════════════════════════════════

    private static ThemeVariant? _variant;
    private static bool? _highContrast;
    private static OverrideState? _override;

    /// <summary>
    /// Variante peinte en ce moment. Lue une fois puis mise en cache : c'est
    /// <see cref="Refresh"/> qui la remet en question, pas chaque appel — une fenêtre lit ses
    /// couleurs des dizaines de fois par WM_PAINT.
    /// </summary>
    internal static ThemeVariant Variant =>
        _override?.Variant ?? (_variant ??= ReadVariant(AppsUseLightThemeValueName));

    /// <summary>Mode Contraste élevé de Windows, lu et mis en cache comme la variante.</summary>
    internal static bool IsHighContrast =>
        _override?.HighContrast ?? (_highContrast ??= Win32.IsHighContrastActive());

    /// <summary>
    /// Palette à peindre. C'est le seul point d'entrée des fenêtres : la bascule Contraste élevé
    /// se décide ici, une fois, et aucune fenêtre n'a à la connaître.
    /// </summary>
    internal static Palette Current =>
        IsHighContrast ? HighContrastPalette(Win32.GetSysColor) : ForVariant(Variant);

    /// <summary>
    /// Variante de la barre des tâches, réglage distinct de celui des applications
    /// (SystemUsesLightTheme). Ne sert qu'à l'icône de notification, qui vit sur cette barre et
    /// non dans une fenêtre du produit : le chantier CH6 s'en sert, aucune fenêtre ne doit.
    /// Non mise en cache, l'icône n'étant redessinée qu'aux changements de thème.
    /// </summary>
    internal static ThemeVariant TaskbarVariant => ReadVariant(SystemUsesLightThemeValueName);

    // ═══════════════════════════════════════════════════════════════
    // Suivi du thème de Windows
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Prévient que la palette courante a changé. Une fenêtre s'y abonne pour se redessiner, et
    /// s'en désabonne dans son Dispose — un abonnement laissé derrière garde en vie une fenêtre
    /// détruite et lui envoie des repeints sur un handle mort.
    /// </summary>
    internal static event Action? Changed;

    /// <summary>
    /// Relit l'état du système et rend true si la palette a changé. Appelée sur WM_SETTINGCHANGE
    /// et WM_THEMECHANGED ; ne fait rien d'autre que constater, la re-peinture appartient aux
    /// abonnés de <see cref="Changed"/>.
    ///
    /// Sous un override de test, la lecture système est délibérément court-circuitée : un test
    /// qui force le thème sombre ne doit pas voir sa palette repasser au clair parce que le poste
    /// qui exécute la suite est en clair.
    /// </summary>
    internal static bool Refresh()
    {
        if (_override != null)
            return false;

        var previous = Current;

        _variant = ReadVariant(AppsUseLightThemeValueName);
        _highContrast = Win32.IsHighContrastActive();

        if (Current == previous)
            return false;

        Changed?.Invoke();
        return true;
    }

    /// <summary>
    /// Filtre de WM_SETTINGCHANGE. Windows diffuse ce message pour des dizaines de réglages sans
    /// rapport ; seuls ceux dont lParam vaut « ImmersiveColorSet » concernent le thème, et le
    /// mode Contraste élevé arrive lui par wParam = SPI_SETHIGHCONTRAST. Relire le registre à
    /// chaque WM_SETTINGCHANGE marcherait, mais ferait une lecture de registre à chaque
    /// changement de résolution, de police système ou de fuseau horaire.
    ///
    /// La fenêtre message-only du tray reçoit ce message même quand aucune fenêtre visible n'est
    /// ouverte : c'est elle qui porte l'appel, et c'est ce qui rend la bascule à chaud possible
    /// sans qu'une fenêtre soit à l'écran.
    /// </summary>
    internal static bool IsThemeSettingChange(IntPtr wParam, IntPtr lParam)
    {
        if ((uint)wParam == Win32.SPI_SETHIGHCONTRAST)
            return true;

        if (lParam == IntPtr.Zero)
            return false;

        string? changed = Marshal.PtrToStringUni(lParam);
        return string.Equals(changed, "ImmersiveColorSet", StringComparison.Ordinal);
    }

    // ═══════════════════════════════════════════════════════════════
    // Caches GDI
    // ═══════════════════════════════════════════════════════════════

    private static readonly Dictionary<uint, IntPtr> BrushCache = new();
    private static readonly Dictionary<(uint Color, int Width), IntPtr> PenCache = new();
    private static readonly Dictionary<(FontRole Role, int Dpi, bool Underlined), IntPtr> FontCache = new();

    /// <summary>
    /// Brosse pleine d'une couleur de la palette, partagée et vivante jusqu'à la fin du processus.
    ///
    /// ⛔ Ne jamais passer le résultat à DeleteObject. Le cache est indexé par couleur et non par
    /// thème, donc il n'a rien à invalider à la bascule ; mais il fait aussi que la même brosse
    /// est rendue à plusieurs fenêtres, et qu'un DeleteObject dans le Dispose de l'une laisserait
    /// les autres peindre avec un handle mort. C'est le changement de contrat par rapport au code
    /// actuel, où chaque fenêtre crée et détruit sa brosse de fond.
    ///
    /// Le cache est borné par construction : au plus les quinze jetons des deux palettes, plus
    /// ceux du schéma à contraste élevé.
    /// </summary>
    internal static IntPtr Brush(uint color)
    {
        if (BrushCache.TryGetValue(color, out var existing))
            return existing;

        var brush = Win32.CreateSolidBrush(color);
        BrushCache[color] = brush;
        return brush;
    }

    /// <summary>
    /// Stylo plein, même contrat que <see cref="Brush"/> : partagé, jamais détruit par l'appelant.
    /// La largeur entre dans la clé, un anneau de focus faisant 2 px là où un filet en fait 1.
    /// </summary>
    internal static IntPtr Pen(uint color, int width = 1)
    {
        var key = (color, width);
        if (PenCache.TryGetValue(key, out var existing))
            return existing;

        var pen = Win32.CreatePen(Win32.PS_SOLID, width, color);
        PenCache[key] = pen;
        return pen;
    }

    /// <summary>
    /// Police d'un rôle à un DPI donné, partagée et jamais détruite par l'appelant.
    ///
    /// Le DPI entre dans la clé plutôt que d'être appliqué par l'appelant : l'application est
    /// PerMonitorV2, deux fenêtres peuvent vivre au même instant sur deux écrans d'échelles
    /// différentes, et une police mise à l'échelle une fois pour toutes au démarrage rend un
    /// texte flou sur le second écran. Le cache est borné par le nombre d'échelles réellement
    /// rencontrées, soit une ou deux sur un poste ordinaire.
    /// </summary>
    /// <param name="underlined">Souligne le tracé. Ce n'est pas un rôle de plus : c'est le
    /// même rôle, décoré. Un lien rendu par un contrôle STATIC est peint par le système, donc
    /// on ne peut pas lui dessiner de filet comme le fait ThemeControls.DrawLink — le survol
    /// d'un tel lien passe par cette police-là.</param>
    internal static IntPtr Font(FontRole role, int dpi, bool underlined = false)
    {
        if (dpi <= 0)
            dpi = 96;

        var key = (role, dpi, underlined);
        if (FontCache.TryGetValue(key, out var existing))
            return existing;

        var (size, weight, face) = Metrics(role);
        int height = -(int)Math.Round(size * dpi / 96.0, MidpointRounding.AwayFromZero);

        // Qualité 5 = CLEARTYPE_QUALITY, comme partout ailleurs dans l'application.
        var font = Win32.CreateFontW(height, 0, 0, 0, weight, 0, underlined ? 1u : 0u, 0,
            0, 0, 0, 5, 0, face);
        FontCache[key] = font;
        return font;
    }

    /// <summary>
    /// Échelle typographique de la charte, exprimée à 96 DPI. Candidate jusqu'au chantier CH1,
    /// qui la fige sur les fenêtres témoins.
    /// </summary>
    internal static (int Size, int Weight, string Face) Metrics(FontRole role) => role switch
    {
        FontRole.Body => (15, 400, SegoeUi),
        FontRole.Secondary => (13, 400, SegoeUi),
        FontRole.BodyStrong => (15, 600, SegoeUi),
        FontRole.SectionTitle => (18, 600, SegoeUi),
        FontRole.WindowTitle => (24, 600, SegoeUi),
        FontRole.Display => (26, 700, SegoeUi),
        FontRole.StatNumber => (28, 600, SegoeUi),
        FontRole.Mono => (14, 400, Consolas),
        _ => (15, 400, SegoeUi),
    };

    private const string SegoeUi = "Segoe UI";
    private const string Consolas = "Consolas";

    // ═══════════════════════════════════════════════════════════════
    // Lecture du thème dans le registre
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Lit une des deux valeurs de thème sous HKCU. Absente ou illisible, Windows se comporte
    /// comme si elle valait 1 : le thème clair est le défaut du système, et c'est aussi le repli
    /// le moins risqué — une fenêtre claire sur un poste sombre se voit, une fenêtre sombre sur
    /// un poste en contraste élevé mal détecté ne se lit plus.
    ///
    /// La lecture passe par RegGetValueW et non par Microsoft.Win32.Registry, pour la raison
    /// déjà retenue par PolicyManager : toute l'application parle à Windows en P/Invoke, la
    /// fonction lit une valeur sans ouvrir ni fermer de clé, et le binaire AOT n'embarque pas
    /// une dépendance de plus pour deux lectures.
    /// </summary>
    // Les deux valeurs vivent sous HKCU, dans la même clé, et ne diffèrent que par leur nom.
    private const string PersonalizeKeyPath =
        @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    /// <summary>Thème des applications. C'est celui que l'application suit.</summary>
    private const string AppsUseLightThemeValueName = "AppsUseLightTheme";

    /// <summary>Thème de la barre des tâches et du menu Démarrer. Icône de notification seulement.</summary>
    private const string SystemUsesLightThemeValueName = "SystemUsesLightTheme";

    private static ThemeVariant ReadVariant(string valueName)
    {
        if (Win32.TryReadCurrentUserDword(PersonalizeKeyPath, valueName, out int value))
            return value == 0 ? ThemeVariant.Dark : ThemeVariant.Light;

        return ThemeVariant.Light;
    }

    // ═══════════════════════════════════════════════════════════════
    // Hook de test
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Force la variante et le mode Contraste élevé jusqu'au Dispose, qui restaure l'état
    /// précédent — y compris quand le corps du using lève.
    ///
    /// La restauration est portée par le scope et non par l'appelant, pour la raison mesurée sur
    /// ConfigManager._lastErrorUtc le 2026-08-18 : un statique sans remise à zéro traverse toute
    /// la suite dans un seul processus, et rend un test vert en isolation mais rouge dans la
    /// suite entière.
    /// </summary>
    internal static IDisposable OverrideForTests(ThemeVariant variant, bool highContrast = false)
    {
        var scope = new OverrideScope(_override);
        _override = new OverrideState(variant, highContrast);
        return scope;
    }

    private sealed record OverrideState(ThemeVariant Variant, bool HighContrast);

    private sealed class OverrideScope : IDisposable
    {
        private readonly OverrideState? _previous;
        private bool _disposed;

        internal OverrideScope(OverrideState? previous) => _previous = previous;

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _override = _previous;
        }
    }
}
