using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Lot 130 A (D1 d'Antoine, 29/10) — les fenêtres Conflit de disposition et Couches ne dépassent
/// plus l'écran : plafonnées à la zone de travail, texte qui revient à la ligne, contenu qui
/// défile. Ce qui est éprouvé : le calcul de taille (<see cref="WindowSizing.FitScrollable"/>) et
/// le défilement (<see cref="WindowScroll"/>). Le rendu se lit au banc de captures
/// (<c>AZERTY_CAPTURE_ECRAN=1024x728</c>).
///
/// Cadre mesuré sur Windows 11 : 16 px de large, 39 de haut ; barre de défilement : 17 px.
/// </summary>
public class FenetresDefilantesTests
{
    private const int FrameW = 16;
    private const int FrameH = 39;
    private const int Bar = 17;

    // Écran de la CI : 1024×768, moins la barre des tâches.
    private const int WorkW = 1024;
    private const int WorkH = 728;

    /// <summary>Contenu qui grandit quand la largeur diminue : texte qui revient à la ligne.</summary>
    private static int Wrapping(int width) => 400 * 1000 / Math.Max(1, width) * 2 + 300;

    [Fact]
    public void FenetreQuiTient_GardeSaTailleDeReference()
    {
        // 100 % sur 1920×1080 : rien ne change, ni la largeur, ni la hauteur, ni de barre.
        var fit = WindowSizing.FitScrollable(560, _ => 440, FrameW, FrameH, 1920, 1032, Bar);

        Assert.Equal(new WindowSizing.ScrollableFit(560, 560, 440, 440, false), fit);
    }

    [Fact]
    public void LeCasMesure_200PourcentSur1024x768_LaFenetreTientDansLEcran()
    {
        // Conflit à 200 % : référence 1120 px de zone cliente pour 1008 de disponible.
        var fit = WindowSizing.FitScrollable(1120, Wrapping, FrameW, FrameH, WorkW, WorkH, Bar);

        Assert.True(fit.ClientWidth + FrameW <= WorkW, $"largeur {fit.ClientWidth + FrameW}");
        Assert.True(fit.ClientHeight + FrameH <= WorkH, $"hauteur {fit.ClientHeight + FrameH}");
        Assert.True(fit.Scrolls);
        Assert.True(fit.ContentHeight > fit.ClientHeight);
    }

    [Fact]
    public void QuandIlDefile_LaBarreNeMangePasLaLargeurDeMiseEnPage()
    {
        // La barre s'ajoute à droite : le contenu est mesuré à la largeur qui reste, pas à
        // celle de la zone cliente entière.
        var fit = WindowSizing.FitScrollable(1120, Wrapping, FrameW, FrameH, WorkW, WorkH, Bar);

        Assert.Equal(fit.LayoutWidth + Bar, fit.ClientWidth);
        Assert.Equal(Wrapping(fit.LayoutWidth), fit.ContentHeight);
    }

    [Fact]
    public void Largeur_EstPlafonneeMemeSansDefilement()
    {
        // Un contenu court mais large : la fenêtre se resserre, le texte repasse à la ligne,
        // et aucune barre n'apparaît tant que la hauteur suffit.
        var fit = WindowSizing.FitScrollable(1120, _ => 300, FrameW, FrameH, WorkW, WorkH, Bar);

        Assert.Equal(WorkW - FrameW, fit.ClientWidth);
        Assert.Equal(fit.ClientWidth, fit.LayoutWidth);
        Assert.False(fit.Scrolls);
        Assert.Equal(300, fit.ClientHeight);
    }

    [Fact]
    public void MesureEchouee_ZoneDeTravailNulle_NePlafonnePas()
    {
        var fit = WindowSizing.FitScrollable(1120, _ => 600, FrameW, FrameH, 0, 0, Bar);

        Assert.Equal(new WindowSizing.ScrollableFit(1120, 1120, 600, 600, false), fit);
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    [InlineData(1.5)]
    [InlineData(1.75)]
    public void DeCentAcentSoixanteQuinzePourcent_RienNeDefile_SurUnEcranDe1920(double scale)
    {
        int wanted = (int)(560 * scale);
        int content = (int)(440 * scale);
        var fit = WindowSizing.FitScrollable(wanted, _ => content, FrameW, FrameH, 1920, 1032, Bar);

        Assert.False(fit.Scrolls);
        Assert.Equal(wanted, fit.ClientWidth);
        Assert.Equal(content, fit.ClientHeight);
    }

    [Fact]
    public void QuandIlDefile_SansChangerDeLargeur_LeContenuEstMesureUneSeuleFois()
    {
        // La barre tient à droite de la largeur voulue : la mise en page ne change pas, une
        // seconde mesure rendrait la même hauteur.
        var widths = new List<int>();
        var fit = WindowSizing.FitScrollable(600, w => { widths.Add(w); return 900; }, FrameW, FrameH, WorkW, WorkH, Bar);

        Assert.Equal(new[] { 600 }, widths);
        Assert.Equal(new WindowSizing.ScrollableFit(600, 600 + Bar, WorkH - FrameH, 900, true), fit);
    }

    [Theory]
    [InlineData(1120)] // plus large que l'écran : la barre rogne la mise en page
    [InlineData(1000)] // entre les deux : la barre ne tient pas à droite
    [InlineData(560)]
    public void LaDerniereMesure_EstCelleDeLaLargeurDeMiseEnPage(int wanted)
    {
        // FitWindow ne repose pas les contrôles après FitScrollable : ils sont à la dernière
        // largeur mesurée, qui doit donc être celle que la fenêtre retient.
        int last = 0;
        var fit = WindowSizing.FitScrollable(wanted, w => { last = w; return Wrapping(w); }, FrameW, FrameH, WorkW, WorkH, Bar);

        Assert.Equal(fit.LayoutWidth, last);
        Assert.Equal(Wrapping(last), fit.ContentHeight);
    }

    [Fact]
    public void CadreAuDpiDeLEcran_PlusGrandQueCeluiDeQuatreVingtSeizeDpi()
    {
        // 200 % : AdjustWindowRectEx mesure au DPI du système, AdjustWindowRectExForDpi à celui
        // de l'écran de la fenêtre, que la fenêtre porte réellement.
        const uint style = Win32.WS_OVERLAPPED | Win32.WS_CAPTION | Win32.WS_SYSMENU;
        var at96 = NativeWindow.OuterSize(100, 100, style, 0, 96);
        var at192 = NativeWindow.OuterSize(100, 100, style, 0, 192);

        Assert.True(at192.Width > at96.Width && at192.Height > at96.Height, $"{at96} puis {at192}");
        Assert.Equal(NativeWindow.OuterSize(100, 100, style), NativeWindow.OuterSize(100, 100, style, 0, 0));
    }

    [Fact]
    public void TryHandle_IgnoreLesAutresMessages()
    {
        var scroll = new WindowScroll();
        scroll.Reset(IntPtr.Zero, contentHeight: 900, viewHeight: 600);

        Assert.False(scroll.TryHandle(IntPtr.Zero, 0x000F /* WM_PAINT */, IntPtr.Zero, 24));
    }

    // ── Fenêtres réelles, à 200 % sur l'écran de la CI (1024×728 de zone de travail) ──

    private static Win32.RECT Ecran() => new() { left = 0, top = 0, right = WorkW, bottom = WorkH };

    private static void AssertDansLEcran(IntPtr hwnd, string nom)
    {
        Win32.GetWindowRect(hwnd, out var w);
        Assert.True(w.left >= 0 && w.top >= 0 && w.right <= WorkW && w.bottom <= WorkH,
            $"{nom} : ({w.left}, {w.top}) - ({w.right}, {w.bottom}) hors de {WorkW}x{WorkH}");
    }

    private static bool DansLaZoneCliente(IntPtr hwnd, IntPtr control)
    {
        Win32.GetClientRect(hwnd, out var client);
        Win32.GetWindowRect(control, out var r);
        var topLeft = new Win32.POINT { x = r.left, y = r.top };
        var bottomRight = new Win32.POINT { x = r.right, y = r.bottom };
        Win32.ScreenToClient(hwnd, ref topLeft);
        Win32.ScreenToClient(hwnd, ref bottomRight);
        return topLeft.x >= 0 && topLeft.y >= 0 && bottomRight.x <= client.right && bottomRight.y <= client.bottom;
    }

    [Fact]
    public void Couches_A200Pour100_TientDansLEcranEtFermerSeVoit()
    {
        using var isolation = new BancCapture.Isolation();
        using var ecran = NativeWindow.OverrideWorkAreaForTests(Ecran());
        var window = new MaintainableLayersWindow();
        try
        {
            window.Show();
            IntPtr hwnd = BancCapture.Handle(window);
            BancCapture.ApplyDpi(hwnd, 192);

            AssertDansLEcran(hwnd, "Couches");
            var scroll = BancCapture.Field<WindowScroll>(window, "_scroll");
            Assert.True(scroll.Needed, "à 200 % sur 1024×728, la fenêtre des couches défile");
            scroll.ScrollTo(hwnd, int.MaxValue);
            Assert.True(DansLaZoneCliente(hwnd, BancCapture.Field<IntPtr>(window, "_hClose")),
                "« Fermer » reste hors de la zone cliente, même défilé jusqu'au bout");
        }
        finally
        {
            BancCapture.Teardown(window);
        }
    }

    [Fact]
    public void Couches_Rouverte_RepartEnHautEtLeFocusSeVoit()
    {
        using var isolation = new BancCapture.Isolation();
        using var ecran = NativeWindow.OverrideWorkAreaForTests(Ecran());
        var window = new MaintainableLayersWindow();
        try
        {
            window.Show();
            IntPtr hwnd = BancCapture.Handle(window);
            BancCapture.ApplyDpi(hwnd, 192);
            var scroll = BancCapture.Field<WindowScroll>(window, "_scroll");
            scroll.ScrollTo(hwnd, int.MaxValue);
            Assert.True(scroll.Position > 0, "précondition : la fenêtre est défilée");

            window.Hide();
            window.Show();

            Assert.Equal(0, scroll.Position);
            Assert.True(DansLaZoneCliente(hwnd, BancCapture.Field<IntPtr>(window, "_hMaster")),
                "la case générale, premier contrôle, est sous le pli à la réouverture");
        }
        finally
        {
            BancCapture.Teardown(window);
        }
    }

    [Fact]
    public void Conflit_A200Pour100_TientDansLEcranEtLesDeuxChoixSeVoient()
    {
        using var isolation = new BancCapture.Isolation();
        using var ecran = NativeWindow.OverrideWorkAreaForTests(Ecran());
        foreach (bool atStartup in new[] { true, false })
        {
            var window = new LayoutConflictWindow(atStartup, () => { }, () => { });
            try
            {
                window.Show();
                IntPtr hwnd = BancCapture.Handle(window);
                BancCapture.ApplyDpi(hwnd, 192);

                AssertDansLEcran(hwnd, "Conflit");
                var scroll = BancCapture.Field<WindowScroll>(window, "_scroll");
                scroll.ScrollTo(hwnd, int.MaxValue);
                // Cette fenêtre n'a pas de bouton « Fermer » : ses deux choix sont ses sorties.
                Assert.True(DansLaZoneCliente(hwnd, BancCapture.Field<IntPtr>(window, "_hWndBtnQuit")),
                    $"« Quitter » hors de la zone cliente (démarrage : {atStartup})");

                window.Show();
                Assert.Equal(0, scroll.Position);
                Assert.True(DansLaZoneCliente(hwnd, BancCapture.Field<IntPtr>(window, "_hWndBtnKeep")),
                    $"« Garder », qui a le focus, hors de la zone cliente (démarrage : {atStartup})");
            }
            finally
            {
                BancCapture.Teardown(window);
            }
        }
    }

    [Fact]
    public void APropos_A200Pour100_TientDansLEcranEtFermerSeVoit()
    {
        using var isolation = new BancCapture.Isolation();
        using var ecran = NativeWindow.OverrideWorkAreaForTests(Ecran());
        var window = new AboutWindow();
        try
        {
            window.Show();
            IntPtr hwnd = BancCapture.Handle(window);
            BancCapture.ApplyDpi(hwnd, 192);

            AssertDansLEcran(hwnd, "À propos");
            Assert.True(DansLaZoneCliente(hwnd, BancCapture.Field<IntPtr>(window, "_hWndBtnClose")),
                "« Fermer » hors de la zone cliente");
        }
        finally
        {
            BancCapture.Teardown(window);
        }
    }

    [Fact]
    public void APropos_SurUnEcranPlusPetitQueSaReference_EstPlafonneeSansChangerDEchelle()
    {
        // Largeur et hauteur : la fenêtre naît au DPI de l'écran, que ni CorrectedScale ni
        // WM_DPICHANGED ne viennent corriger. 480×240 est plus petit que la référence même à 100 %.
        using var isolation = new BancCapture.Isolation();
        using var ecran = NativeWindow.OverrideWorkAreaForTests(new Win32.RECT { left = 0, top = 0, right = 480, bottom = 240 });
        var window = new AboutWindow();
        try
        {
            window.Show();
            IntPtr hwnd = BancCapture.Handle(window);

            Win32.GetWindowRect(hwnd, out var w);
            Assert.True(w.left >= 0 && w.top >= 0 && w.right <= 480 && w.bottom <= 240,
                $"({w.left}, {w.top}) - ({w.right}, {w.bottom}) hors de 480x240");
            Assert.True(DansLaZoneCliente(hwnd, BancCapture.Field<IntPtr>(window, "_hWndBtnClose")),
                "« Fermer » hors de la zone cliente");
        }
        finally
        {
            BancCapture.Teardown(window);
        }
    }

    [Fact]
    public void Defilement_PositionBorneAuContenu()
    {
        var scroll = new WindowScroll();
        scroll.Reset(IntPtr.Zero, contentHeight: 900, viewHeight: 600);

        Assert.True(scroll.Needed);
        Assert.Equal(300, scroll.Clamp(int.MaxValue));
        Assert.Equal(0, scroll.Clamp(-50));
        Assert.Equal(120, scroll.Clamp(120));
    }

    [Fact]
    public void Defilement_ContenuQuiTient_NEstPasNecessaire()
    {
        var scroll = new WindowScroll();
        scroll.Reset(IntPtr.Zero, contentHeight: 500, viewHeight: 600);

        Assert.False(scroll.Needed);
        Assert.Equal(0, scroll.Clamp(200));
        Assert.False(scroll.OnMouseWheel(IntPtr.Zero, IntPtr.Zero, 24));
        Assert.False(scroll.OnVScroll(IntPtr.Zero, IntPtr.Zero, 24));
    }

    // ── Lot H (a) : le clavier défile les fenêtres Conflit et Couches ──

    private const int VK_PRIOR = 0x21, VK_NEXT = 0x22, VK_END = 0x23, VK_HOME = 0x24, VK_DOWN = 0x28, VK_UP = 0x26;

    private static void Touche(IntPtr hwnd, int vk) =>
        Win32.SendMessageW(hwnd, Win32.WM_KEYDOWN, (IntPtr)vk, IntPtr.Zero);

    /// <summary>Ouvre la fenêtre à 200 % sur 1024×728 et rend son handle et son défilement.</summary>
    private static (IntPtr Hwnd, WindowScroll Scroll) Ouvrir(object window, Action show)
    {
        show();
        IntPtr hwnd = BancCapture.Handle(window);
        BancCapture.ApplyDpi(hwnd, 192);
        var scroll = BancCapture.Field<WindowScroll>(window, "_scroll");
        Assert.True(scroll.Needed, "précondition : le contenu déborde à 200 % sur 1024×728");
        Assert.Equal(0, scroll.Position);
        return (hwnd, scroll);
    }

    private static void AssertClavierDefile(IntPtr hwnd, WindowScroll scroll)
    {
        Touche(hwnd, VK_NEXT);
        int page = scroll.Position;
        Assert.True(page > 0, "Page bas laisse la position à 0");

        Touche(hwnd, VK_END);
        Assert.Equal(scroll.Clamp(int.MaxValue), scroll.Position);

        Touche(hwnd, VK_PRIOR);
        Assert.True(scroll.Position < scroll.Clamp(int.MaxValue), "Page haut ne remonte pas");

        Touche(hwnd, VK_HOME);
        Assert.Equal(0, scroll.Position);

        Touche(hwnd, VK_DOWN);
        Assert.True(scroll.Position > 0, "Flèche bas laisse la position à 0");
        Touche(hwnd, VK_UP);
        Assert.Equal(0, scroll.Position);
    }

    [Fact]
    public void Couches_ClavierSurLaFenetre_Defile()
    {
        using var isolation = new BancCapture.Isolation();
        using var ecran = NativeWindow.OverrideWorkAreaForTests(Ecran());
        var window = new MaintainableLayersWindow();
        try
        {
            var (hwnd, scroll) = Ouvrir(window, window.Show);
            AssertClavierDefile(hwnd, scroll);
        }
        finally
        {
            BancCapture.Teardown(window);
        }
    }

    [Fact]
    public void Conflit_ClavierSurLaFenetre_Defile()
    {
        using var isolation = new BancCapture.Isolation();
        using var ecran = NativeWindow.OverrideWorkAreaForTests(Ecran());
        var window = new LayoutConflictWindow(true, () => { }, () => { });
        try
        {
            var (hwnd, scroll) = Ouvrir(window, window.Show);
            AssertClavierDefile(hwnd, scroll);
        }
        finally
        {
            BancCapture.Teardown(window);
        }
    }

    [Fact]
    public void Conflit_ClavierSurUnBoutonFocalise_Defile_ViaLaNavigationDeDialogue()
    {
        // Le focus est toujours sur un contrôle : le WM_KEYDOWN va au bouton, jamais à la fenêtre.
        using var isolation = new BancCapture.Isolation();
        using var ecran = NativeWindow.OverrideWorkAreaForTests(Ecran());
        var window = new LayoutConflictWindow(true, () => { }, () => { });
        try
        {
            var (hwnd, scroll) = Ouvrir(window, window.Show);
            IntPtr bouton = BancCapture.Field<IntPtr>(window, "_hWndBtnKeep");
            Win32.SetFocus(bouton);
            var msg = new Win32.MSG { hwnd = bouton, message = Win32.WM_KEYDOWN, wParam = (IntPtr)VK_END };

            Assert.True(DialogNavigation.TryRoute(ref msg));
            Assert.Equal(scroll.Clamp(int.MaxValue), scroll.Position);
        }
        finally
        {
            BancCapture.Teardown(window);
        }
    }

    [Fact]
    public void Couches_ChampFocalise_GardeSesTouches()
    {
        // Home et Fin déplacent le curseur du champ du délai : la fenêtre ne défile pas.
        using var isolation = new BancCapture.Isolation();
        using var ecran = NativeWindow.OverrideWorkAreaForTests(Ecran());
        var window = new MaintainableLayersWindow();
        try
        {
            var (hwnd, scroll) = Ouvrir(window, window.Show);
            IntPtr champ = BancCapture.Field<IntPtr>(window, "_hDelay");
            Win32.SetFocus(champ);
            var msg = new Win32.MSG { hwnd = champ, message = Win32.WM_KEYDOWN, wParam = (IntPtr)VK_END };

            DialogNavigation.TryRoute(ref msg);
            Assert.Equal(0, scroll.Position);
        }
        finally
        {
            BancCapture.Teardown(window);
        }
    }

    [Theory]
    [InlineData(0x09)] // Tab
    [InlineData(0x1B)] // Échap
    [InlineData(0x0D)] // Entrée
    [InlineData(0x20)] // Espace
    [InlineData(0x41)] // A
    public void TryHandle_NeToucheNiTabNiEchapNiLesAutresTouches(int vk)
    {
        var scroll = new WindowScroll();
        scroll.Reset(IntPtr.Zero, contentHeight: 900, viewHeight: 600);

        Assert.False(scroll.TryHandle(IntPtr.Zero, Win32.WM_KEYDOWN, (IntPtr)vk, 24));
        Assert.Equal(0, scroll.Position);
    }

    [Fact]
    public void TryHandle_ContenuQuiTient_NeConsommePasLesTouches()
    {
        var scroll = new WindowScroll();
        scroll.Reset(IntPtr.Zero, contentHeight: 500, viewHeight: 600);

        Assert.False(scroll.TryHandle(IntPtr.Zero, Win32.WM_KEYDOWN, (IntPtr)VK_NEXT, 24));
    }

    // ── Lot H (b) : le cadre de l'Accueil se mesure au DPI de l'écran ──

    [Theory]
    [InlineData(144)]
    [InlineData(192)]
    public void Accueil_PlafondMesureLeCadreAuDpiDeLEcran(int dpi)
    {
        // Zone de travail juste assez haute pour le cadre du DPI système, pas pour celui de l'écran.
        const uint style = Win32.WS_OVERLAPPED | Win32.WS_CAPTION | Win32.WS_SYSMENU;
        float scale = dpi / 96f;
        int clientW = (int)(960 * scale * 0.75f);
        int clientH = (int)(720 * scale * 0.75f);
        var system = NativeWindow.OuterSize(clientW, clientH, style, Win32.WS_EX_TOPMOST);
        var screen = NativeWindow.OuterSize(clientW, clientH, style, Win32.WS_EX_TOPMOST, dpi);
        Assert.True(screen.Height > system.Height, $"précondition : cadre {system} (système) contre {screen} (écran {dpi})");

        int workH = system.Height + 1;
        float fitted = OnboardingWindow.FittedScaleFor(scale, 4000, workH);

        Assert.True(fitted < scale, $"échelle {fitted} : le cadre de l'écran ({screen.Height - clientH} px) n'est pas compté");
        int fittedClientH = (int)(720 * fitted * 0.75f);
        Assert.True(fittedClientH + (screen.Height - clientH) <= workH, $"la fenêtre plafonnée dépasse {workH} px");
    }

    [Fact]
    public void AjusterAuDpi_MesureLeCadreAuDpiDonne()
    {
        const uint style = Win32.WS_OVERLAPPED | Win32.WS_CAPTION | Win32.WS_SYSMENU;
        var work = new Win32.RECT { left = 0, top = 0, right = 2000, bottom = 1400 };
        IntPtr hwnd = Win32.CreateWindowExW(0, "STATIC", "t", style, 0, 0, 10, 10, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        try
        {
            NativeWindow.FitToDpi(hwnd, 330, 154, 192, style, 0, work);
            Win32.GetWindowRect(hwnd, out var w);
            var expected = NativeWindow.OuterSize(660, 308, style, 0, 192);
            Assert.Equal(expected, (w.right - w.left, w.bottom - w.top));
        }
        finally
        {
            Win32.DestroyWindow(hwnd);
        }
    }
}
