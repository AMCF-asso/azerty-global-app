using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Audit environnement Windows (docs/audit-environnement-windows.md), T5 — EW-15 : le contraste
/// élevé activé ou coupé pendant qu'une fenêtre est ouverte. Vraies fenêtres, isolées et
/// invisibles par <see cref="BancCapture.Isolation"/> ; le réglage de Windows est simulé par
/// <see cref="HighContrast.OverrideForTests"/>, jamais changé sur le poste.
/// </summary>
public sealed class EnvironnementWindowsContrasteTests
{
    private const uint WM_SETTINGCHANGE = 0x001A;
    private const uint WM_SYSCOLORCHANGE = 0x0015;

    public static TheoryData<string> FenêtresPapier => new()
    {
        "À propos", "Statistiques", "Couches", "Conflit de disposition", "Durée de pause",
    };

    private static IDisposable Ouvrir(string nom) => nom switch
    {
        "À propos" => new AboutWindow(),
        "Statistiques" => new UsageStatsWindow(),
        "Couches" => new MaintainableLayersWindow(),
        "Conflit de disposition" => new LayoutConflictWindow(false, () => { }, () => { }),
        "Durée de pause" => OuvrirPause(),
        _ => throw new ArgumentOutOfRangeException(nameof(nom)),
    };

    private static PauseDurationDialog OuvrirPause()
    {
        var dialog = new PauseDurationDialog();
        BancCapture.Call(dialog, "CreateWindow", IntPtr.Zero);
        return dialog;
    }

    /// <summary>
    /// EW-15 : la fenêtre ouverte en papier passe au contraste élevé sur WM_SETTINGCHANGE,
    /// palette et barre de titre, puis revient au papier quand le réglage est coupé.
    /// </summary>
    [Theory]
    [MemberData(nameof(FenêtresPapier))]
    public void EW15_ContrasteÉlevéBasculé_FenêtreOuverte_PaletteEtBarreSuivent(string nom)
    {
        using var isolation = new BancCapture.Isolation();
        bool contraste = false;
        HighContrast.OverrideForTests = () => contraste;
        var cadres = new List<(IntPtr Fenêtre, bool Contraste)>();
        NativeWindow.FrameAppliedForTests = (h, hc) => cadres.Add((h, hc));
        var fenêtre = Ouvrir(nom);
        try
        {
            IntPtr hwnd = BancCapture.Handle(fenêtre);
            Assert.NotEqual(IntPtr.Zero, hwnd);
            Assert.False(BancCapture.Field<PaperPalette>(fenêtre, "_pal").HighContrast);

            contraste = true;
            cadres.Clear();
            Win32.SendMessageW(hwnd, WM_SETTINGCHANGE, IntPtr.Zero, IntPtr.Zero);

            Assert.True(BancCapture.Field<PaperPalette>(fenêtre, "_pal").HighContrast);
            Assert.Contains((hwnd, true), cadres);

            contraste = false;
            cadres.Clear();
            Win32.SendMessageW(hwnd, WM_SYSCOLORCHANGE, IntPtr.Zero, IntPtr.Zero);

            Assert.Equal(PaperPalette.Default, BancCapture.Field<PaperPalette>(fenêtre, "_pal"));
            Assert.Contains((hwnd, false), cadres);
        }
        finally
        {
            BancCapture.Teardown(fenêtre);
        }
    }

    /// <summary>
    /// EW-15 : Paramètres basculait déjà sa palette, pas sa barre de titre ; la barre suit
    /// maintenant. Un WM_SETTINGCHANGE sans changement de palette ne repose pas le cadre.
    /// </summary>
    [Fact]
    public void EW15_Paramètres_ContrasteÉlevéBasculé_LaBarreDeTitreSuit()
    {
        using var isolation = new BancCapture.Isolation();
        bool contraste = false;
        HighContrast.OverrideForTests = () => contraste;
        var cadres = new List<(IntPtr Fenêtre, bool Contraste)>();
        NativeWindow.FrameAppliedForTests = (h, hc) => cadres.Add((h, hc));
        var fenêtre = new SettingsWindow();
        fenêtre.Show();
        try
        {
            IntPtr hwnd = BancCapture.Handle(fenêtre);
            cadres.Clear();
            Win32.SendMessageW(hwnd, WM_SETTINGCHANGE, IntPtr.Zero, IntPtr.Zero);
            Assert.Empty(cadres); // fond d'écran, langue… : rien à repeindre

            contraste = true;
            Win32.SendMessageW(hwnd, WM_SETTINGCHANGE, IntPtr.Zero, IntPtr.Zero);
            Assert.Contains((hwnd, true), cadres);
        }
        finally
        {
            BancCapture.Teardown(fenêtre);
        }
    }

    /// <summary>EW-15 : le clavier virtuel, gardé en vie, repose sa barre de titre.</summary>
    [Fact]
    public void EW15_ClavierVirtuel_ContrasteÉlevéBasculé_LaBarreDeTitreSuit()
    {
        using var isolation = new BancCapture.Isolation();
        bool contraste = false;
        HighContrast.OverrideForTests = () => contraste;
        var cadres = new List<(IntPtr Fenêtre, bool Contraste)>();
        NativeWindow.FrameAppliedForTests = (h, hc) => cadres.Add((h, hc));
        var clavier = new VirtualKeyboard(LayoutLoader.LoadFromResource());
        try
        {
            IntPtr hwnd = BancCapture.Handle(clavier);
            cadres.Clear();
            Win32.SendMessageW(hwnd, WM_SETTINGCHANGE, IntPtr.Zero, IntPtr.Zero);
            Assert.Empty(cadres);

            contraste = true;
            Win32.SendMessageW(hwnd, WM_SETTINGCHANGE, IntPtr.Zero, IntPtr.Zero);
            Assert.Equal(new[] { (hwnd, true) }, cadres);
        }
        finally
        {
            BancCapture.Teardown(clavier);
        }
    }

    /// <summary>
    /// EW-15 : les boutons papier (LightButton) et les pastilles (ChipButton) étaient posés à la
    /// création selon le réglage du moment, jamais retirés. Ils décident maintenant au dessin :
    /// le dessin du système en contraste élevé, le papier sinon.
    /// </summary>
    [Fact]
    public void EW15_BoutonsPapier_DécidentAuDessin()
    {
        using var isolation = new BancCapture.Isolation();
        bool contraste = false;
        HighContrast.OverrideForTests = () => contraste;
        Assert.True(LightButton.PaintsPaper);
        Assert.True(ChipButton.PaintsPaper);
        contraste = true;
        Assert.False(LightButton.PaintsPaper);
        Assert.False(ChipButton.PaintsPaper);
    }

    /// <summary>Le banc remet le réglage simulé et l'espion du cadre à zéro.</summary>
    [Fact]
    public void Isolation_RemetLeContrasteSimuléÀZéro()
    {
        using (new BancCapture.Isolation())
        {
            HighContrast.OverrideForTests = () => true;
            NativeWindow.FrameAppliedForTests = (_, _) => { };
        }
        Assert.Null(HighContrast.OverrideForTests);
        Assert.Null(NativeWindow.FrameAppliedForTests);
    }
}

/// <summary>
/// T6 — EW-17, EW-18, EW-19 : écran débranché, plus petit, ou fenêtre glissée vers un autre
/// écran. La zone de travail est simulée (<see cref="NativeWindow.OverrideWorkAreaForTests"/>) :
/// aucun écran du poste n'est touché.
/// </summary>
public sealed class EnvironnementWindowsEcranTests
{
    private const uint WM_DISPLAYCHANGE = 0x007E;
    private const uint WM_SETTINGCHANGE = 0x001A;
    private const int SPI_SETWORKAREA = 0x002F;

    private static readonly Win32.RECT PetitÉcran = R(0, 0, 1024, 768);

    private static Win32.RECT R(int left, int top, int right, int bottom) =>
        new() { left = left, top = top, right = right, bottom = bottom };

    private static Win32.RECT RectOf(IntPtr hwnd)
    {
        Assert.True(Win32.GetWindowRect(hwnd, out var rect));
        return rect;
    }

    private static void AssertDans(Win32.RECT zone, Win32.RECT r)
    {
        Assert.True(r.left >= zone.left && r.top >= zone.top && r.right <= zone.right && r.bottom <= zone.bottom,
            $"({r.left},{r.top})-({r.right},{r.bottom}) déborde de ({zone.left},{zone.top})-({zone.right},{zone.bottom})");
    }

    // ── Calcul pur ───────────────────────────────────────────────────

    [Theory]
    [InlineData(100, 100, 600, 400, true)]       // dans l'écran
    [InlineData(-560, 100, -60, 400, false)]     // à gauche, hors écran
    [InlineData(-450, 100, 50, 400, true)]       // déborde, 50 px dedans : encore attrapable
    [InlineData(-470, 100, 30, 400, false)]      // 30 px dedans : non
    [InlineData(100, 100, 190, 400, false)]      // trop étroite pour être une vraie place
    public void EW17_PlaceAttrapable(int l, int t, int r, int b, bool attendu)
        => Assert.Equal(attendu, NativeWindow.IsRectVisible(R(l, t, r, b), PetitÉcran));

    [Fact]
    public void EW17_RectTropGrand_RéduitPuisRamené()
    {
        var tenu = NativeWindow.FitRectIn(R(-300, 100, 1500, 900), PetitÉcran);
        Assert.Equal(R(0, 0, 1024, 768), tenu);
    }

    [Fact]
    public void EW17_RectQuiDéborde_RamenéSansChangerDeTaille()
    {
        var tenu = NativeWindow.FitRectIn(R(900, 600, 1300, 900), PetitÉcran);
        Assert.Equal(R(624, 468, 1024, 768), tenu);
    }

    [Theory]
    [InlineData(WM_DISPLAYCHANGE, 0, true)]
    [InlineData(WM_SETTINGCHANGE, SPI_SETWORKAREA, true)]
    [InlineData(WM_SETTINGCHANGE, 0, false)] // couleurs, langue : pas un changement d'écran
    public void EW18_ChangementDÉcran_Reconnu(uint msg, int wParam, bool attendu)
        => Assert.Equal(attendu, NativeWindow.IsDisplayChange(msg, (IntPtr)wParam));

    // ── EW-17 : restauration plafonnée ───────────────────────────────

    /// <summary>Place mémorisée sur un écran de 1800 px, rouverte sur un écran de 1024 : le
    /// clavier est ramené et réduit, proportions gardées, au lieu de déborder.</summary>
    [Fact]
    public void EW17_ClavierVirtuel_PlaceMémoriséeTropGrande_TientDansLÉcran()
    {
        using var isolation = new BancCapture.Isolation();
        using var écran = NativeWindow.OverrideWorkAreaForTests(PetitÉcran);
        ConfigManager.SetWindowBounds(ConfigManager.VirtualKeyboardBoundsKey, R(-300, 100, 1500, 700));
        var clavier = new VirtualKeyboard(LayoutLoader.LoadFromResource());
        try
        {
            clavier.Show();
            var place = RectOf(BancCapture.Handle(clavier));
            AssertDans(PetitÉcran, place);
            // Proportions gardées : 1800 × 600 donne 3 pour 1, à un pixel près par côté.
            double ratio = (place.right - place.left) / (double)(place.bottom - place.top);
            Assert.InRange(ratio, 2.8, 3.2);
        }
        finally
        {
            BancCapture.Teardown(clavier);
        }
    }

    [Fact]
    public void EW17_Leçons_PlaceMémoriséeTropGrande_TientDansLÉcran()
    {
        using var isolation = new BancCapture.Isolation();
        using var écran = NativeWindow.OverrideWorkAreaForTests(PetitÉcran);
        var layout = LayoutLoader.LoadFromResource();
        var mapper = new KeyMapper(layout, new MockWin32Api());
        var hook = new KeyboardHook(mapper); // jamais installé
        ConfigManager.SetWindowBounds(ConfigManager.LessonsWindowBoundsKey, R(-300, 100, 1500, 1000));
        var leçons = new LessonsWindow(layout, mapper, hook);
        try
        {
            AssertDans(PetitÉcran, RectOf(BancCapture.Handle(leçons)));
        }
        finally
        {
            BancCapture.Teardown(leçons);
        }
    }

    // ── EW-18 : écran débranché, zone de travail changée ─────────────

    /// <summary>La fenêtre est restée sur l'écran débranché (x = 3000) : WM_DISPLAYCHANGE la
    /// ramène ; une zone de travail rétrécie (barre des tâches) aussi.</summary>
    [Theory]
    [InlineData(WM_DISPLAYCHANGE, 0)]
    [InlineData(WM_SETTINGCHANGE, SPI_SETWORKAREA)]
    public void EW18_ÀPropos_ÉcranDébranché_RevientDansLaZone(uint msg, int wParam)
    {
        using var isolation = new BancCapture.Isolation();
        var fenêtre = new AboutWindow();
        try
        {
            IntPtr hwnd = BancCapture.Handle(fenêtre);
            var avant = RectOf(hwnd);
            int w = avant.right - avant.left, h = avant.bottom - avant.top;
            Win32.MoveWindow(hwnd, 3000, 200, w, h, false);

            using var écran = NativeWindow.OverrideWorkAreaForTests(PetitÉcran);
            Win32.SendMessageW(hwnd, msg, (IntPtr)wParam, IntPtr.Zero);

            var après = RectOf(hwnd);
            AssertDans(PetitÉcran, après);
            Assert.Equal((w, h), (après.right - après.left, après.bottom - après.top));
        }
        finally
        {
            BancCapture.Teardown(fenêtre);
        }
    }

    /// <summary>Le clavier virtuel, gardé en vie, revient aussi, proportions gardées.</summary>
    [Fact]
    public void EW18_ClavierVirtuel_ÉcranDébranché_RevientDansLaZone()
    {
        using var isolation = new BancCapture.Isolation();
        var clavier = new VirtualKeyboard(LayoutLoader.LoadFromResource());
        try
        {
            IntPtr hwnd = BancCapture.Handle(clavier);
            var avant = RectOf(hwnd);
            Win32.MoveWindow(hwnd, 3000, 200, avant.right - avant.left, avant.bottom - avant.top, false);

            using var écran = NativeWindow.OverrideWorkAreaForTests(PetitÉcran);
            Win32.SendMessageW(hwnd, WM_DISPLAYCHANGE, IntPtr.Zero, IntPtr.Zero);

            AssertDans(PetitÉcran, RectOf(hwnd));
        }
        finally
        {
            BancCapture.Teardown(clavier);
        }
    }

    /// <summary>À propos, cachée puis réaffichée après le départ de son écran : elle s'ouvre
    /// dans la zone de travail, pas à l'ancienne place.</summary>
    [Fact]
    public void EW18_ÀPropos_RéaffichéeAprèsDépartDeLÉcran_DansLaZone()
    {
        using var isolation = new BancCapture.Isolation();
        var fenêtre = new AboutWindow();
        try
        {
            IntPtr hwnd = BancCapture.Handle(fenêtre);
            var avant = RectOf(hwnd);
            Win32.MoveWindow(hwnd, 3000, 200, avant.right - avant.left, avant.bottom - avant.top, false);

            using var écran = NativeWindow.OverrideWorkAreaForTests(PetitÉcran);
            fenêtre.Show();

            AssertDans(PetitÉcran, RectOf(hwnd));
        }
        finally
        {
            BancCapture.Teardown(fenêtre);
        }
    }

    // ── EW-19 : Leçons glissées vers un écran d'autre densité ───────

    /// <summary>WM_DPICHANGED avec la place suggérée par Windows (le coin reste) : les Leçons
    /// restent où on les pose, au lieu de sauter au centre de l'écran.</summary>
    [Fact]
    public void EW19_Leçons_ChangementDeDensité_GardeLaPlaceSuggérée()
    {
        using var isolation = new BancCapture.Isolation();
        using var écran = NativeWindow.OverrideWorkAreaForTests(R(0, 0, 3440, 1440));
        var layout = LayoutLoader.LoadFromResource();
        var mapper = new KeyMapper(layout, new MockWin32Api());
        var hook = new KeyboardHook(mapper);
        ConfigManager.ClearWindowBounds(ConfigManager.LessonsWindowBoundsKey);
        var leçons = new LessonsWindow(layout, mapper, hook);
        try
        {
            IntPtr hwnd = BancCapture.Handle(leçons);
            var avant = RectOf(hwnd);
            Win32.MoveWindow(hwnd, 120, 90, avant.right - avant.left, avant.bottom - avant.top, false);
            int dpi = Win32.GetDpiForWindow(hwnd);
            BancCapture.ApplyDpi(hwnd, dpi == 120 ? 144 : 120);

            var après = RectOf(hwnd);
            Assert.Equal((120, 90), (après.left, après.top));
        }
        finally
        {
            BancCapture.Teardown(leçons);
        }
    }
}
