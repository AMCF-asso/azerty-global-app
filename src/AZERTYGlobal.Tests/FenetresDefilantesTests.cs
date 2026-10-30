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
}
