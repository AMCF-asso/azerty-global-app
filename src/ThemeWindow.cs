// Habillage de fenêtre — refonte graphique v1.2.0, chantier CH0.
//
// Trois manques transverses que l'audit du 2026-08-28 a relevés, et qui ne se corrigent pas
// fenêtre par fenêtre sans les recopier treize fois :
//
//   - le chrome. Dix fenêtres appelaient EnableDarkTitleBar, qui posait l'attribut DWM à 1
//     quel que soit le thème de Windows : sur un poste en thème clair, toutes portaient une
//     barre de titre sombre au-dessus d'un corps clair. La fenêtre Leçons, elle, rendait sa
//     barre claire sous un OS sombre alors que son appel est le même que celui d'À propos —
//     six écarts de code ont été relevés entre les deux chemins de création, aucun n'explique
//     le rendu. ApplyChrome retire la classe entière de pannes plutôt que d'en chercher la
//     cause : il pose la couleur de barre explicitement plutôt que de dépendre du seul mode
//     immersif, et il force le recalcul du cadre, ce qu'aucun des deux chemins ne faisait.
//   - l'icône de fenêtre. Deux fenêtres sur dix posaient le logo du produit ; les huit autres
//     affichaient l'icône générique de Windows dans leur barre de titre et dans Alt+Tab.
//   - le DPI. Quatre fenêtres ne traitent aucun WM_DPICHANGED, et le dialogue de durée de
//     pause raisonne en pixels bruts.
//
// Sur Windows 10, les attributs de couleur n'existent pas : la barre reste celle du système,
// et c'est l'écart assumé par la décision n° 8 du 2026-08-28 — 569 des 1 604 installations du
// parc. Rien ici ne le contourne, et rien ne prétend l'avoir mesuré.

using System.Runtime.InteropServices;

namespace AZERTYGlobal;

/// <summary>
/// Chrome, icône, fond de classe et DPI, partagés par toutes les fenêtres. Comme les caches de
/// <see cref="Theme"/>, ce qui est rendu ici vit jusqu'à la fin du processus et ne se détruit
/// pas depuis l'appelant.
/// </summary>
static class ThemeWindow
{
    // ═══════════════════════════════════════════════════════════════
    // Chrome
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Windows 11, où DWMWA_CAPTION_COLOR existe. Lu une fois : la version du système ne change
    /// pas en cours d'exécution. Le manifeste de l'application déclare supportedOS Win10 et
    /// Win11, sans quoi Windows mentirait ici et rendrait 6.2 sur toute version récente.
    /// </summary>
    internal static bool SupportsCaptionColor { get; } = Environment.OSVersion.Version.Build >= 22000;

    /// <summary>
    /// Couleurs de chrome pour une palette. Fonction pure, séparée de la pose : c'est elle que
    /// les tests éprouvent, la pose demandant une fenêtre réelle.
    ///
    /// En Contraste élevé, les deux valent DWMWA_COLOR_DEFAULT — le chrome revient au système,
    /// comme le reste de la palette.
    /// </summary>
    internal static (uint Caption, uint Text) CaptionColors(Palette palette, bool highContrast)
    {
        if (highContrast)
            return (Win32.DWMWA_COLOR_DEFAULT, Win32.DWMWA_COLOR_DEFAULT);

        return (palette.Paper, palette.Ink);
    }

    /// <summary>Chrome au thème courant. C'est la forme que les fenêtres appellent.</summary>
    internal static void ApplyChrome(IntPtr hwnd) =>
        ApplyChrome(hwnd, Theme.Current, Theme.Variant, Theme.IsHighContrast);

    /// <summary>
    /// Pose le chrome et force DWM à recalculer le cadre. Se rappelle à volonté : à la création
    /// de la fenêtre, et de nouveau à chaque bascule de thème.
    ///
    /// Le SetWindowPos final n'est pas décoratif. Un attribut DWM posé avant le premier
    /// affichage n'est pris en compte qu'au prochain recalcul du cadre, et un MoveWindow
    /// intermédiaire — restauration de position, recentrage, correction de DPI — suffit à le
    /// perdre en silence. SWP_FRAMECHANGED demande ce recalcul explicitement, sans bouger,
    /// redimensionner, réordonner ni activer la fenêtre.
    /// </summary>
    internal static void ApplyChrome(IntPtr hwnd, Palette palette, ThemeVariant variant,
        bool highContrast)
    {
        if (hwnd == IntPtr.Zero)
            return;

        try
        {
            int immersiveDark = variant == ThemeVariant.Dark ? 1 : 0;
            Win32.DwmSetWindowAttribute(hwnd, Win32.DWMWA_USE_IMMERSIVE_DARK_MODE,
                ref immersiveDark, sizeof(int));

            if (SupportsCaptionColor)
            {
                var (caption, text) = CaptionColors(palette, highContrast);
                int captionValue = unchecked((int)caption);
                int textValue = unchecked((int)text);
                Win32.DwmSetWindowAttribute(hwnd, Win32.DWMWA_CAPTION_COLOR,
                    ref captionValue, sizeof(int));
                Win32.DwmSetWindowAttribute(hwnd, Win32.DWMWA_TEXT_COLOR,
                    ref textValue, sizeof(int));
            }
        }
        catch
        {
            // Windows 10 1809 et anterieur, ou attribut non supporte : la barre reste celle du
            // systeme. C'est l'ecart assume par la decision n° 8, pas une erreur a signaler.
        }

        Win32.SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0,
            Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE
            | Win32.SWP_FRAMECHANGED);
    }

    /// <summary>
    /// Relit l'attribut de mode sombre tel que DWM le porte réellement. Ne sert à rien en
    /// production : c'est l'instrument de mesure du chantier CH8, qui doit dire si un poste
    /// Windows 10 accepte l'attribut ou l'ignore. Rend null quand l'appel échoue.
    /// </summary>
    internal static bool? ReadImmersiveDarkMode(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            return null;

        try
        {
            int value;
            int hr = Win32.DwmGetWindowAttribute(hwnd, Win32.DWMWA_USE_IMMERSIVE_DARK_MODE,
                out value, sizeof(int));
            return hr == 0 ? value != 0 : null;
        }
        catch
        {
            return null;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Fond de classe
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Repose la brosse de fond de la classe de la fenêtre, puis redessine cadre et enfants.
    ///
    /// Une classe de fenêtre garde le hbrBackground qu'on lui a donné à l'enregistrement, et
    /// c'est avec lui que Windows efface le fond avant chaque WM_PAINT. Sans cette re-pose, une
    /// bascule de thème laisse un éclair de l'ancienne couleur à chaque repeint, indéfiniment.
    ///
    /// La brosse vient du cache de <see cref="Theme"/> : elle survit à la fenêtre, ce qui est
    /// exactement ce que demande une brosse de classe, et il ne faut jamais la détruire.
    /// </summary>
    internal static void ApplyClassBackground(IntPtr hwnd, uint color)
    {
        if (hwnd == IntPtr.Zero)
            return;

        Win32.SetClassLongPtrW(hwnd, Win32.GCLP_HBRBACKGROUND, Theme.Brush(color));
        Win32.RedrawWindow(hwnd, IntPtr.Zero, IntPtr.Zero,
            Win32.RDW_INVALIDATE | Win32.RDW_ERASE | Win32.RDW_FRAME | Win32.RDW_ALLCHILDREN
            | Win32.RDW_UPDATENOW);
    }

    // ═══════════════════════════════════════════════════════════════
    // Icône de fenêtre
    // ═══════════════════════════════════════════════════════════════

    private static IntPtr _logo = IntPtr.Zero;
    private static bool _logoTried;
    private static readonly Dictionary<int, IntPtr> IconCache = new();

    /// <summary>
    /// Pose le logo du produit comme icône de la fenêtre, dans les deux tailles que Windows
    /// demande : la grande pour Alt+Tab et la barre des tâches, la petite pour la barre de
    /// titre. Les deux se lisent aux métriques système au moment de la pose, jamais en dur —
    /// elles suivent l'échelle d'affichage, et une icône rendue à la mauvaise taille est
    /// rééchantillonnée par Windows, donc floue.
    ///
    /// Sans appel, la fenêtre porte l'icône générique de Windows : c'était le cas de huit des
    /// dix fenêtres à chrome avant ce chantier.
    /// </summary>
    internal static void ApplyProductIcon(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            return;

        IntPtr big = ProductIcon(Win32.GetSystemMetrics(Win32.SM_CXICON));
        IntPtr small = ProductIcon(Win32.GetSystemMetrics(Win32.SM_CXSMICON));

        if (big != IntPtr.Zero)
            Win32.SendMessageW(hwnd, Win32.WM_SETICON, (IntPtr)Win32.ICON_BIG, big);
        if (small != IntPtr.Zero)
            Win32.SendMessageW(hwnd, Win32.WM_SETICON, (IntPtr)Win32.ICON_SMALL, small);
    }

    /// <summary>
    /// Icône du produit à une taille donnée, rendue une fois puis gardée. Rend IntPtr.Zero si
    /// la ressource ou GDI+ manque à l'appel — une fenêtre sans icône reste une fenêtre.
    /// </summary>
    private static IntPtr ProductIcon(int size)
    {
        if (size <= 0)
            return IntPtr.Zero;

        if (IconCache.TryGetValue(size, out var existing))
            return existing;

        IntPtr icon = RenderIcon(size);
        IconCache[size] = icon;
        return icon;
    }

    private static IntPtr RenderIcon(int size)
    {
        if (!_logoTried)
        {
            _logoTried = true;
            _logo = GdiImageLoader.LoadFromEmbeddedResource(typeof(ThemeWindow),
                ProductIdentity.LogoResourceName);
        }

        if (_logo == IntPtr.Zero)
            return IntPtr.Zero;

        IntPtr hBitmap = IntPtr.Zero;
        IntPtr hMask = IntPtr.Zero;
        try
        {
            // 0x0026200A = PixelFormat32bppARGB.
            if (Win32.GdipCreateBitmapFromScan0(size, size, 0, 0x0026200A, IntPtr.Zero,
                    out IntPtr bitmap) != 0 || bitmap == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            Win32.GdipGetImageGraphicsContext(bitmap, out IntPtr graphics);
            Win32.GdipSetSmoothingMode(graphics, 4);
            Win32.GdipSetInterpolationMode(graphics, 7);
            Win32.GdipDrawImageRectI(graphics, _logo, 0, 0, size, size);
            Win32.GdipDeleteGraphics(graphics);

            Win32.GdipCreateHBITMAPFromBitmap(bitmap, out hBitmap, 0x00000000);
            Win32.GdipDisposeImage(bitmap);
            if (hBitmap == IntPtr.Zero)
                return IntPtr.Zero;

            hMask = Win32.CreateBitmap(size, size, 1, 1, new byte[MaskByteCount(size)]);
            if (hMask == IntPtr.Zero)
                return IntPtr.Zero;

            var info = new Win32.ICONINFO { fIcon = true, hbmMask = hMask, hbmColor = hBitmap };
            return Win32.CreateIconIndirect(ref info);
        }
        catch
        {
            return IntPtr.Zero;
        }
        finally
        {
            if (hMask != IntPtr.Zero)
                Win32.DeleteObject(hMask);
            if (hBitmap != IntPtr.Zero)
                Win32.DeleteObject(hBitmap);
        }
    }

    /// <summary>
    /// Taille du masque monochrome d'une icône carrée. Les lignes d'un bitmap 1 bit par pixel
    /// sont alignées sur un mot de deux octets, ce qui n'est pas la même chose que
    /// largeur × hauteur / 8 : à 40 px — la grande icône d'un poste à 125 % — la formule naïve
    /// rend 200 octets là où GDI en lit 240, et lit donc au-delà du tableau.
    /// </summary>
    internal static int MaskByteCount(int size) => ((size + 15) / 16) * 2 * size;

    // ═══════════════════════════════════════════════════════════════
    // DPI
    // ═══════════════════════════════════════════════════════════════

    private static int? _dpiOverride;

    /// <summary>
    /// Force l'échelle rendue par <see cref="DpiOf"/> jusqu'au Dispose, qui restaure la
    /// précédente. Sert au banc de captures : rendre la matrice 100/125/150 % sans toucher aux
    /// réglages d'affichage du poste, qui déconnecteraient la session.
    ///
    /// Toutes les fenêtres passent par DpiOf pour adopter l'échelle de l'écran où elles
    /// naissent, donc ce seul crochet les couvre toutes.
    /// </summary>
    internal static IDisposable OverrideDpiForTests(int dpi)
    {
        var scope = new DpiScope(_dpiOverride);
        _dpiOverride = dpi;
        return scope;
    }

    private sealed class DpiScope : IDisposable
    {
        private readonly int? _previous;
        private bool _disposed;

        internal DpiScope(int? previous) => _previous = previous;

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _dpiOverride = _previous;
        }
    }

    /// <summary>Échelle d'une fenêtre, 96 par défaut quand elle est illisible.</summary>
    internal static int DpiOf(IntPtr hwnd)
    {
        if (_dpiOverride is int forced)
            return forced;

        if (hwnd == IntPtr.Zero)
            return 96;

        try
        {
            int dpi = Win32.GetDpiForWindow(hwnd);
            return dpi > 0 ? dpi : 96;
        }
        catch
        {
            return 96;
        }
    }

    /// <summary>
    /// Lit le nouveau DPI d'un WM_DPICHANGED et applique le rectangle que Windows suggère. Rend
    /// l'échelle, à charge pour l'appelant de refaire ses polices et sa mise en page.
    ///
    /// Le rectangle suggéré n'est pas facultatif : c'est lui qui garde la fenêtre à la même
    /// taille apparente lorsqu'elle passe sur un écran d'une autre échelle. Une fenêtre qui
    /// l'ignore double ou divise de taille en traversant la frontière entre deux écrans.
    /// </summary>
    internal static int ApplyDpiChange(IntPtr hwnd, IntPtr wParam, IntPtr lParam)
    {
        int dpi = (wParam.ToInt32() >> 16) & 0xFFFF;
        if (dpi <= 0)
            dpi = DpiOf(hwnd);

        if (hwnd != IntPtr.Zero && lParam != IntPtr.Zero)
        {
            var suggested = Marshal.PtrToStructure<Win32.RECT>(lParam);
            Win32.MoveWindow(hwnd, suggested.left, suggested.top,
                suggested.right - suggested.left, suggested.bottom - suggested.top, true);
        }

        return dpi;
    }
}
