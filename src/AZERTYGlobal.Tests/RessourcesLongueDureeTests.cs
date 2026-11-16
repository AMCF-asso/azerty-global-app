using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using AZERTYGlobal;
using Xunit;
using Xunit.Abstractions;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Audit des ressources longue durée (docs/audit-ressources.md, 16/11/2026). Une session de
/// plusieurs heures se simule ici en répétant ce qu'elle fait sans cesse : ouvrir, peindre,
/// relire la palette, changer de langue, fermer. Après un échauffement (caches par taille et
/// par police remplis), chaque cycle doit rendre ce qu'il a pris : objets GDI et USER du
/// processus (GetGuiResources), abonnés des événements statiques et fenêtres inscrites à la
/// navigation des dialogues ; les tests sans fenêtre comptent aussi les crochets et les
/// abonnés du moteur partagé.
///
/// Les compteurs couvrent tout le processus : l'assemblée tourne sans parallélisme
/// (TestCollectionBehavior.cs). On compare la pente entre deux paliers, jamais une valeur
/// absolue ; la tolérance absorbe un objet que Windows crée paresseusement une fois.
/// </summary>
public sealed class RessourcesLongueDureeTests
{
    private const int Echauffement = 3;
    /// <summary>Cycles mesurés ; <c>AZERTY_CYCLES_RESSOURCES</c> les allonge pour une session plus
    /// longue (exploration à la main, jamais dans verify).</summary>
    private static readonly int Cycles =
        int.TryParse(Environment.GetEnvironmentVariable("AZERTY_CYCLES_RESSOURCES"), out int n) && n > 0 ? n : 20;
    /// <summary>Objets tolérés entre deux relevés. GDI : aucune dérive mesurée sur 11
    /// fenêtres (16/11). USER : un décalage ponctuel, sans lien avec le nombre de cycles
    /// (Accueil : +0 sur 20 seul, +7 sur 20 dans verify, +8 sur 100, +9 sur 300 ; Pause +4 sur
    /// 20 comme sur 100), d'où un relevé à mi-parcours : la seconde moitié ne doit pas croître ;
    /// une fuite d'un objet par cycle y fait +10.</summary>
    private const int Tolerance = 5;

    private readonly ITestOutputHelper _out;

    public RessourcesLongueDureeTests(ITestOutputHelper output) => _out = output;

    // ── Compteurs ─────────────────────────────────────────────────────────────

    [DllImport("user32.dll")]
    private static extern uint GetGuiResources(IntPtr hProcess, uint uiFlags);
    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();
    [DllImport("user32.dll")]
    private static extern bool RedrawWindow(IntPtr hWnd, IntPtr lprcUpdate, IntPtr hrgnUpdate, uint flags);

    private const uint GR_GDIOBJECTS = 0;
    private const uint GR_USEROBJECTS = 1;

    internal readonly record struct Releve(int Gdi, int User, int Abonnes, int Dialogues)
    {
        public override string ToString() => $"GDI {Gdi}, USER {User}, abonnés {Abonnes}, dialogues {Dialogues}";
    }

    internal static Releve Mesurer()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        BancCapture.Pump(5);
        IntPtr self = GetCurrentProcess();
        return new Releve(
            (int)GetGuiResources(self, GR_GDIOBJECTS),
            (int)GetGuiResources(self, GR_USEROBJECTS),
            AbonnesStatiques(),
            Inscrits(typeof(DialogNavigation), "_dialogs"));
    }

    /// <summary>Abonnés des événements statiques qui vivent toute la session.</summary>
    internal static int AbonnesStatiques()
    {
        int total = 0;
        foreach (var (type, name) in new[]
                 {
                     (typeof(ConfigManager), "AppLanguageChanged"),
                     (typeof(ConfigManager), "WindowBoundsCleared"),
                     (typeof(ConfigManager), "TrainingEnabledChanged"),
                     (typeof(ToastActivation), "Activated"),
                 })
            total += Abonnes(type, null, name);
        return total;
    }

    internal static int Abonnes(Type type, object? instance, string eventName)
    {
        var flags = BindingFlags.NonPublic | (instance == null ? BindingFlags.Static : BindingFlags.Instance);
        var field = type.GetField(eventName, flags)
                    ?? throw new MissingFieldException(type.Name, eventName);
        return (field.GetValue(instance) as Delegate)?.GetInvocationList().Length ?? 0;
    }

    private static int Inscrits(Type type, string field)
    {
        var value = type.GetField(field, BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        // HashSet<T> n'implémente pas ICollection non générique : Count par réflexion.
        return (int)value.GetType().GetProperty("Count")!.GetValue(value)!;
    }

    /// <summary>
    /// Échauffe, relève, répète <see cref="Cycles"/> fois avec un relevé à mi-parcours, relève,
    /// et rend l'écart par compteur. GDI, abonnés et dialogues : sur tous les cycles. USER : sur
    /// la seconde moitié seulement, Windows créant à la demande quelques objets une fois par
    /// processus (voir <see cref="Tolerance"/>) ; l'écart total reste écrit dans la sortie.
    /// </summary>
    private void AssertCycleSansFuite(string nom, Action cycle)
    {
        for (int i = 0; i < Echauffement; i++) cycle();
        var avant = Mesurer();
        int moitie = Cycles / 2;
        for (int i = 0; i < moitie; i++) cycle();
        var milieu = Mesurer();
        for (int i = moitie; i < Cycles; i++) cycle();
        var apres = Mesurer();
        _out.WriteLine($"{nom} : avant {avant} ; après {moitie} cycles {milieu} ; après {Cycles} cycles {apres}");

        var fuites = new List<string>();
        if (apres.Gdi - avant.Gdi > Tolerance) fuites.Add($"GDI +{apres.Gdi - avant.Gdi}");
        if (apres.User - milieu.User > Tolerance)
            fuites.Add($"USER +{apres.User - milieu.User} sur les {Cycles - moitie} derniers cycles (+{apres.User - avant.User} en tout)");
        if (apres.Abonnes != avant.Abonnes) fuites.Add($"abonnés {avant.Abonnes} → {apres.Abonnes}");
        if (apres.Dialogues != avant.Dialogues) fuites.Add($"dialogues inscrits {avant.Dialogues} → {apres.Dialogues}");
        Assert.True(fuites.Count == 0,
            $"{nom} : {Cycles} cycles laissent {string.Join(", ", fuites)} (avant {avant} ; après {apres})");
    }

    // ── Fenêtres : ouvrir, peindre, fermer ────────────────────────────────────

    private static void Peindre(IntPtr hwnd)
    {
        Win32.InvalidateRect(hwnd, IntPtr.Zero, true);
        RedrawWindow(hwnd, IntPtr.Zero, IntPtr.Zero, 0x0001 | 0x0004 | 0x0080 | 0x0100); // INVALIDATE | ERASE | ALLCHILDREN | UPDATENOW
        BancCapture.Pump(2);
    }

    private static void OuvrirFermer<T>(Func<T> creer, Action<T> montrer) where T : IDisposable
    {
        var fenetre = creer();
        try
        {
            montrer(fenetre);
            BancCapture.Pump(3);
            Peindre(BancCapture.Handle(fenetre));
        }
        finally { BancCapture.Teardown(fenetre); }
    }

    public static IEnumerable<object[]> Fenetres() => new[]
    {
        new object[] { "Réglages" },
        new object[] { "À propos" },
        new object[] { "Accueil" },
        new object[] { "Mes statistiques" },
        new object[] { "Pause" },
        new object[] { "Couches maintenues" },
        new object[] { "Conflit de disposition" },
        new object[] { "Clavier virtuel" },
        new object[] { "Recherche de caractère" },
        new object[] { "Notification de bascule" },
        new object[] { "Leçons" },
    };

    private static Action CycleDe(string fenetre)
    {
        var layout = LayoutLoader.LoadFromResource();
        return fenetre switch
        {
            "Réglages" => () => OuvrirFermer(() => new SettingsWindow(), w => w.Show()),
            "À propos" => () => OuvrirFermer(() => new AboutWindow(), w => w.Show()),
            "Accueil" => () => OuvrirFermer(() => new OnboardingWindow(), w => w.Show()),
            "Mes statistiques" => () => OuvrirFermer(() => new UsageStatsWindow(), w => w.Show()),
            "Pause" => () => OuvrirFermer(() => new PauseDurationDialog(), w => BancCapture.Call(w, "CreateWindow", IntPtr.Zero)),
            "Couches maintenues" => () => OuvrirFermer(() => new MaintainableLayersWindow(), w => w.ShowOrBringToFront()),
            "Conflit de disposition" => () => OuvrirFermer(() => new LayoutConflictWindow(false, () => { }, () => { }), w => w.Show()),
            "Clavier virtuel" => () => OuvrirFermer(() => new VirtualKeyboard(layout), w => w.Show()),
            "Recherche de caractère" => () => OuvrirFermer(
                () => new CharacterSearch(new TextInsertionService(_ => default, _ => false, () => false)), w => w.Show()),
            "Notification de bascule" => () => OuvrirFermer(() => new ToggleNotification(), w => w.Show(true)),
            "Leçons" => () =>
            {
                var mapper = new KeyMapper(layout, new MockWin32Api());
                var hook = new KeyboardHook(mapper); // jamais installé
                OuvrirFermer(() => new LessonsWindow(layout, mapper, hook), w => w.Show());
            },
            _ => throw new ArgumentOutOfRangeException(nameof(fenetre)),
        };
    }

    [Theory]
    [MemberData(nameof(Fenetres))]
    public void Ouvrir_peindre_fermer_une_fenetre_rend_tout_ce_qu_elle_a_pris(string fenetre)
    {
        using var isolation = new BancCapture.Isolation();
        AssertCycleSansFuite(fenetre, CycleDe(fenetre));
    }

    // ── Fenêtre restée ouverte : échelle, couleurs et langue qui changent ─────

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessageW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out Win32.RECT rect);

    private const uint WM_SYSCOLORCHANGE = 0x0015;
    private const uint WM_SETTINGCHANGE = 0x001A;

    /// <summary>Ce que Windows envoie à une fenêtre ouverte au fil d'une longue session :
    /// passage d'un écran à l'autre (WM_DPICHANGED, 96 puis 144 ppp), thème ou contraste
    /// changé (WM_SYSCOLORCHANGE, WM_SETTINGCHANGE), langue de l'interface changée.</summary>
    private static void SecouerUneFenetreOuverte(IntPtr hwnd, ref int tour)
    {
        int dpi = tour++ % 2 == 0 ? 144 : 96;
        GetWindowRect(hwnd, out var rect);
        IntPtr suggestion = Marshal.AllocHGlobal(Marshal.SizeOf<Win32.RECT>());
        try
        {
            Marshal.StructureToPtr(rect, suggestion, false);
            SendMessageW(hwnd, Win32.WM_DPICHANGED, (IntPtr)((dpi << 16) | dpi), suggestion);
        }
        finally { Marshal.FreeHGlobal(suggestion); }
        SendMessageW(hwnd, WM_SYSCOLORCHANGE, IntPtr.Zero, IntPtr.Zero);
        SendMessageW(hwnd, WM_SETTINGCHANGE, IntPtr.Zero, IntPtr.Zero);
        ConfigManager.SetAppLanguage(tour % 2 == 0 ? "en" : "fr");
        BancCapture.Pump(3);
        Peindre(hwnd);
    }

    private static IDisposable Ouvrir(string fenetre, Layout layout)
    {
        IDisposable w;
        switch (fenetre)
        {
            case "Réglages": { var x = new SettingsWindow(); x.Show(); w = x; break; }
            case "À propos": { var x = new AboutWindow(); x.Show(); w = x; break; }
            case "Accueil": { var x = new OnboardingWindow(); x.Show(); w = x; break; }
            case "Mes statistiques": { var x = new UsageStatsWindow(); x.Show(); w = x; break; }
            case "Pause": { var x = new PauseDurationDialog(); BancCapture.Call(x, "CreateWindow", IntPtr.Zero); w = x; break; }
            case "Couches maintenues": { var x = new MaintainableLayersWindow(); x.ShowOrBringToFront(); w = x; break; }
            case "Conflit de disposition": { var x = new LayoutConflictWindow(false, () => { }, () => { }); x.Show(); w = x; break; }
            case "Clavier virtuel": { var x = new VirtualKeyboard(layout); x.Show(); w = x; break; }
            case "Recherche de caractère":
            {
                var x = new CharacterSearch(new TextInsertionService(_ => default, _ => false, () => false));
                x.Show(); w = x; break;
            }
            case "Notification de bascule": { var x = new ToggleNotification(); x.Show(true); w = x; break; }
            case "Leçons":
            {
                var mapper = new KeyMapper(layout, new MockWin32Api());
                var x = new LessonsWindow(layout, mapper, new KeyboardHook(mapper)); // crochet jamais installé
                x.Show(); w = x; break;
            }
            default: throw new ArgumentOutOfRangeException(nameof(fenetre));
        }
        BancCapture.Pump(3);
        return w;
    }

    // ── Même objet rouvert : le tray garde une seule instance par fenêtre ─────

    private const uint WM_CLOSE = 0x0010;

    /// <summary>Les fenêtres que TrayApplication garde en un seul exemplaire et rouvre
    /// (champs _settings, _about, _usageStats, _lessons, _onboarding, _virtualKeyboard,
    /// _characterSearch, _maintainableLayersWindow, _toggleNotification). Pause et Conflit
    /// de disposition sont recréées à chaque fois : le premier banc les couvre.</summary>
    public static IEnumerable<object[]> FenetresRouvertes() => new[]
    {
        new object[] { "Réglages" },
        new object[] { "À propos" },
        new object[] { "Accueil" },
        new object[] { "Mes statistiques" },
        new object[] { "Couches maintenues" },
        new object[] { "Clavier virtuel" },
        new object[] { "Recherche de caractère" },
        new object[] { "Notification de bascule" },
        new object[] { "Leçons" },
    };

    private static void Remontrer(IDisposable fen)
    {
        switch (fen)
        {
            case SettingsWindow x: x.ShowOrBringToFront(); break;
            case MaintainableLayersWindow x: x.ShowOrBringToFront(); break;
            case ToggleNotification x: x.Show(true); break;
            case CharacterSearch x: x.Show(); break;
            default: BancCapture.Call(fen, "Show"); break;
        }
    }

    [Theory]
    [MemberData(nameof(FenetresRouvertes))]
    public void Fermer_puis_rouvrir_la_meme_fenetre_ne_garde_rien_du_tour_precedent(string fenetre)
    {
        using var isolation = new BancCapture.Isolation();
        var fen = Ouvrir(fenetre, LayoutLoader.LoadFromResource());
        try
        {
            AssertCycleSansFuite(fenetre + " (rouverte)", () =>
            {
                IntPtr hwnd = BancCapture.Handle(fen);
                if (hwnd != IntPtr.Zero && Win32.IsWindow(hwnd))
                {
                    SendMessageW(hwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                    BancCapture.Pump(3);
                }
                Remontrer(fen);
                BancCapture.Pump(3);
                hwnd = BancCapture.Handle(fen);
                if (hwnd != IntPtr.Zero && Win32.IsWindow(hwnd)) Peindre(hwnd);
            });
        }
        finally { BancCapture.Teardown(fen); }
    }

    [Theory]
    [MemberData(nameof(Fenetres))]
    public void Une_fenetre_ouverte_des_heures_ne_grossit_pas_quand_l_ecran_les_couleurs_ou_la_langue_changent(string fenetre)
    {
        using var isolation = new BancCapture.Isolation();
        string langue = ConfigManager.AppLanguage;
        var fen = Ouvrir(fenetre, LayoutLoader.LoadFromResource());
        try
        {
            IntPtr hwnd = BancCapture.Handle(fen);
            int tour = 0;
            AssertCycleSansFuite(fenetre + " (ouverte)", () => SecouerUneFenetreOuverte(hwnd, ref tour));
        }
        finally
        {
            BancCapture.Teardown(fen);
            ConfigManager.SetAppLanguage(langue);
        }
    }

    // ── Leçons recréées sur le moteur partagé ─────────────────────────────────

    /// <summary>
    /// TrayApplication garde un seul KeyMapper et un seul KeyboardHook pour toute la session,
    /// et recrée les Leçons sur eux quand la langue a changé (EnsureLessonsWindow : Dispose
    /// puis new). Chaque fenêtre s'abonne à trois événements de ces objets : le premier banc
    /// donnait un moteur neuf à chaque cycle et ne pouvait pas voir un abonné oublié.
    /// </summary>
    [Fact]
    public void Recreer_les_Lecons_sur_le_meme_moteur_ne_laisse_aucun_abonne_sur_le_moteur()
    {
        using var isolation = new BancCapture.Isolation();
        var layout = LayoutLoader.LoadFromResource();
        var mapper = new KeyMapper(layout, new MockWin32Api());
        var hook = new KeyboardHook(mapper); // jamais installé
        int AbonnesDuMoteur() =>
            Abonnes(typeof(KeyMapper), mapper, "StateChanged")
            + Abonnes(typeof(KeyMapper), mapper, "KeyDecided")
            + Abonnes(typeof(KeyboardHook), hook, "RawKeyDownDetailed");

        int moteurAvant = AbonnesDuMoteur();
        AssertCycleSansFuite("Leçons recréées sur le même moteur",
            () => OuvrirFermer(() => new LessonsWindow(layout, mapper, hook), w => w.Show()));
        int moteurApres = AbonnesDuMoteur();
        _out.WriteLine($"abonnés du moteur : {moteurAvant} avant, {moteurApres} après {Cycles + Echauffement} fenêtres");
        Assert.Equal(moteurAvant, moteurApres);
    }

    // ── Sans fenêtre : suivi du premier plan, icônes de la zone de notification ─

    /// <summary>ForegroundMonitor pose trois crochets WinEvent à la construction et les retire
    /// au Dispose. Le tray n'en crée qu'un, mais un oubli coûterait un crochet système à
    /// chaque recréation : on compte posés et retirés sur le mock.</summary>
    [Fact]
    public void Creer_et_disposer_le_suivi_du_premier_plan_retire_chaque_crochet_pose()
    {
        var api = new MockWin32Api();
        for (int i = 0; i < Cycles; i++)
            new global::TypingEngine.Windows.ForegroundMonitor(api, IntPtr.Zero).Dispose();
        _out.WriteLine($"{Cycles} suivis : {api.WinEventHooksSet} crochets posés, {api.WinEventHooksRemoved} retirés");
        Assert.Equal(3 * Cycles, api.WinEventHooksSet);
        Assert.Equal(api.WinEventHooksSet, api.WinEventHooksRemoved);
    }

    /// <summary>L'icône du tray est refaite à chaque bascule, Verr. Maj. ou pause
    /// (TrayApplication.UpdateIcon, qui détruit l'ancienne) : la fabrique ne doit rien
    /// garder d'autre que l'icône qu'elle rend.</summary>
    [Theory]
    [InlineData("TrayIcon.Create")]
    [InlineData("TrayApplication.CreateTextIcon")]
    public void Refaire_l_icone_du_tray_ne_garde_que_l_icone_rendue(string fabrique)
    {
        int tour = 0;
        AssertCycleSansFuite($"icône du tray ({fabrique})", () =>
        {
            bool actif = tour % 2 == 0, majuscules = tour % 3 == 0;
            tour++;
            IntPtr icone = fabrique == "TrayIcon.Create"
                ? TrayIcon.Create(actif, majuscules, suspended: !actif)
                : TrayApplication.CreateTextIcon("AG", actif, majuscules);
            Assert.NotEqual(IntPtr.Zero, icone);
            Win32.DestroyIcon(icone);
            BancCapture.Pump(2); // la boucle de messages du tray tourne entre deux icônes
        });
    }

    /// <summary>Ce que TrayIcon.FromPixels fait autour de l'icône à chaque appel, seul :
    /// démarrer GDI+ puis l'arrêter (GdiplusStartup / GdiplusShutdown).
    ///
    /// Mesure du 16/11 : sans pomper les messages entre deux cycles, USER +52 sur 100 cycles,
    /// encore là après 2 s d'attente ; en pompant comme la boucle du tray, +2 ; avec un GDI+
    /// gardé ouvert ailleurs, 0. GDI+ libère ses objets USER par la file de messages du fil
    /// qui l'arrête : le tray la vide, le constat est réfuté. Une boucle serrée sans pompage
    /// (fil de fond, outil en ligne de commande) fuirait.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Demarrer_puis_arreter_GDIplus_ne_garde_rien(bool gdiplusDejaOuvert)
    {
        var input = new Win32.GdiplusStartupInput { GdiplusVersion = 1 };
        IntPtr exterieur = IntPtr.Zero;
        if (gdiplusDejaOuvert) // ce que ferait un GDI+ démarré une fois pour tout le processus
            Assert.Equal(0, Win32.GdiplusStartup(out exterieur, ref input, IntPtr.Zero));
        try
        {
            AssertCycleSansFuite($"GdiplusStartup puis GdiplusShutdown (GDI+ déjà ouvert : {gdiplusDejaOuvert})", () =>
            {
                Assert.Equal(0, Win32.GdiplusStartup(out IntPtr token, ref input, IntPtr.Zero));
                Win32.GdiplusShutdown(token);
                BancCapture.Pump(2); // la boucle de messages du tray tourne entre deux icônes
            });
        }
        finally
        {
            if (exterieur != IntPtr.Zero) Win32.GdiplusShutdown(exterieur);
        }
    }

    // ── Leçons redimensionnées : le contenu suit la taille de la fenêtre ──────

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);

    private const uint SWP_NOMOVE = 0x0002, SWP_NOZORDER = 0x0004, SWP_NOACTIVATE = 0x0010;

    /// <summary>
    /// Les Leçons mettent leur contenu à l'échelle de la fenêtre (_windowScale) : chaque
    /// taille donne des hauteurs de police neuves, demandées au cache partagé AppFonts.Get
    /// (FontSpec), qui garde chaque HFONT jusqu'à la fin du processus. Le banc balaie
    /// <see cref="Cycles"/> tailles distinctes, puis les mêmes une seconde fois : la seconde
    /// passe ne doit rien créer (cache rempli), la première pas plus que la tolérance.
    /// </summary>
    [Fact]
    public void Redimensionner_les_Lecons_ne_garde_pas_des_polices_pour_chaque_taille_traversee()
    {
        using var isolation = new BancCapture.Isolation();
        var layout = LayoutLoader.LoadFromResource();
        var mapper = new KeyMapper(layout, new MockWin32Api());
        var fen = new LessonsWindow(layout, mapper, new KeyboardHook(mapper)); // crochet jamais installé
        try
        {
            fen.Show();
            BancCapture.Pump(3);
            IntPtr hwnd = BancCapture.Handle(fen);
            GetWindowRect(hwnd, out var rect);
            int w0 = rect.right - rect.left, h0 = rect.bottom - rect.top;
            var tailles = new List<(int W, int H)>();
            for (int i = 0; i < Cycles; i++)
            {
                float k = 0.85f + 0.4f * i / Math.Max(1, Cycles - 1); // de 0,85 à 1,25 fois la taille d'origine
                tailles.Add(((int)(w0 * k), (int)(h0 * k)));
            }
            var echelles = new SortedSet<float>();
            void Balayer()
            {
                foreach (var (w, h) in tailles)
                {
                    SetWindowPos(hwnd, IntPtr.Zero, 0, 0, w, h, SWP_NOMOVE | SWP_NOZORDER | SWP_NOACTIVATE);
                    BancCapture.Pump(2);
                    Peindre(hwnd);
                    echelles.Add(BancCapture.Field<float>(fen, "_windowScale"));
                }
                SetWindowPos(hwnd, IntPtr.Zero, 0, 0, w0, h0, SWP_NOMOVE | SWP_NOZORDER | SWP_NOACTIVATE);
                Peindre(hwnd);
            }

            for (int i = 0; i < Echauffement; i++) Peindre(hwnd); // taille d'origine seulement
            var avant = Mesurer();
            int policesAvant = Inscrits(typeof(AppFonts), "Cache");
            Balayer();
            var premiere = Mesurer();
            int policesPremiere = Inscrits(typeof(AppFonts), "Cache");
            Balayer();
            var seconde = Mesurer();
            _out.WriteLine($"Leçons redimensionnées ({Cycles} tailles, échelles {echelles.Count} distinctes de " +
                           $"{(echelles.Count > 0 ? echelles.Min : 0)} à {(echelles.Count > 0 ? echelles.Max : 0)}) : " +
                           $"avant {avant}, cache AppFonts {policesAvant} ; après une passe {premiere}, cache {policesPremiere} ; " +
                           $"après la seconde {seconde}");

            Assert.True(seconde.Gdi - premiere.Gdi <= Tolerance,
                $"seconde passe sur les mêmes tailles : GDI +{seconde.Gdi - premiere.Gdi} (le cache ne se remplit jamais)");
            Assert.True(premiere.Gdi - avant.Gdi <= Tolerance,
                $"{Cycles} tailles traversées laissent GDI +{premiere.Gdi - avant.Gdi} objets jusqu'à la fin du processus " +
                $"(cache AppFonts.Get : {policesAvant} → {policesPremiere} polices, une par hauteur ; avant {avant} ; après {premiere})");
        }
        finally { BancCapture.Teardown(fen); }
    }

    /// <summary>
    /// Le clavier virtuel dessine tout à l'échelle de sa fenêtre (PaintScale, continue, sans
    /// palier) : touches, bandeau et légendes demandent au cache partagé AppFonts.Get une
    /// hauteur neuve à chaque taille. Même protocole que le test des Leçons : <see cref="Cycles"/>
    /// tailles de 0,85 à 1,25 fois la taille d'origine, deux passes.
    /// </summary>
    [Fact]
    public void Redimensionner_le_clavier_virtuel_ne_garde_pas_des_polices_pour_chaque_taille_traversee()
    {
        using var isolation = new BancCapture.Isolation();
        var layout = LayoutLoader.LoadFromResource();
        var fen = new VirtualKeyboard(layout);
        try
        {
            fen.Show();
            BancCapture.Pump(3);
            IntPtr hwnd = BancCapture.Handle(fen);
            GetWindowRect(hwnd, out var rect);
            int w0 = rect.right - rect.left, h0 = rect.bottom - rect.top;
            var tailles = new List<(int W, int H)>();
            for (int i = 0; i < Cycles; i++)
            {
                float k = 0.85f + 0.4f * i / Math.Max(1, Cycles - 1);
                tailles.Add(((int)(w0 * k), (int)(h0 * k)));
            }
            void Balayer()
            {
                foreach (var (w, h) in tailles)
                {
                    SetWindowPos(hwnd, IntPtr.Zero, 0, 0, w, h, SWP_NOMOVE | SWP_NOZORDER | SWP_NOACTIVATE);
                    BancCapture.Pump(2);
                    Peindre(hwnd);
                }
                SetWindowPos(hwnd, IntPtr.Zero, 0, 0, w0, h0, SWP_NOMOVE | SWP_NOZORDER | SWP_NOACTIVATE);
                Peindre(hwnd);
            }

            for (int i = 0; i < Echauffement; i++) Peindre(hwnd);
            var avant = Mesurer();
            int policesAvant = Inscrits(typeof(AppFonts), "Cache");
            Balayer();
            var premiere = Mesurer();
            int policesPremiere = Inscrits(typeof(AppFonts), "Cache");
            Balayer();
            var seconde = Mesurer();
            _out.WriteLine($"Clavier virtuel redimensionné ({Cycles} tailles de {tailles[0]} à {tailles[^1]}) : " +
                           $"avant {avant}, cache AppFonts {policesAvant} ; après une passe {premiere}, cache {policesPremiere} ; " +
                           $"après la seconde {seconde}");

            Assert.True(seconde.Gdi - premiere.Gdi <= Tolerance,
                $"seconde passe sur les mêmes tailles : GDI +{seconde.Gdi - premiere.Gdi} (le cache ne se remplit jamais)");
            Assert.True(premiere.Gdi - avant.Gdi <= Tolerance,
                $"{Cycles} tailles traversées laissent GDI +{premiere.Gdi - avant.Gdi} objets jusqu'à la fin du processus " +
                $"(cache AppFonts.Get : {policesAvant} → {policesPremiere} polices ; avant {avant} ; après {premiere})");
        }
        finally { BancCapture.Teardown(fen); }
    }
}
