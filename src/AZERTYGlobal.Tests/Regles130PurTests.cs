using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Un test par règle du lot « pur » de docs/machines-a-etats.md (app 1.3.0) : file des Leçons,
/// instance unique, écritures différées, statistiques, progression, avis, liens. Le nom de chaque
/// méthode commence par l'id de la règle. Verdict : vert si le produit tient la règle ; rouge si
/// elle n'est pas tenue (le test échoue alors sur sa dernière assertion, les témoins avant).
/// Les règles structurelles (« seul écrivain », « seul appelant ») passent par un balayage du
/// texte source, faute de couture : voir <see cref="SourcePur"/>.
/// </summary>
internal static class SourcePur
{
    /// <summary>Dossier src du dépôt, trouvé en remontant depuis le dossier de sortie des tests.</summary>
    internal static string DossierSrc()
    {
        for (var dossier = new DirectoryInfo(AppContext.BaseDirectory); dossier != null; dossier = dossier.Parent)
        {
            string chemin = Path.Combine(dossier.FullName, "src", "TrayApplication.cs");
            if (File.Exists(chemin)) return Path.Combine(dossier.FullName, "src");
        }
        throw new DirectoryNotFoundException("src introuvable depuis les tests.");
    }

    /// <summary>Texte d'un fichier de l'app, sans les commentaires de ligne ni de bloc.</summary>
    internal static string Code(string fichier) => SansCommentaires(File.ReadAllText(Path.Combine(DossierSrc(), fichier)));

    internal static string SansCommentaires(string texte)
    {
        texte = Regex.Replace(texte, @"/\*.*?\*/", "", RegexOptions.Singleline);
        return Regex.Replace(texte, @"(?m)(^|\s)//.*$", "$1");
    }

    internal static string Normalise(string texte) => Regex.Replace(texte, @"\s+", " ").Trim();

    internal static int Occurrences(string texte, string motif)
    {
        int n = 0;
        for (int i = texte.IndexOf(motif, StringComparison.Ordinal); i >= 0;
             i = texte.IndexOf(motif, i + motif.Length, StringComparison.Ordinal))
            n++;
        return n;
    }

    /// <summary>Appels « a.b.C( » ou « C( » d'un texte de code, sans les mots-clés.</summary>
    internal static HashSet<string> Appels(string code)
    {
        var motsCles = new HashSet<string> { "if", "else", "catch", "while", "for", "foreach", "switch", "return", "using", "lock" };
        return Regex.Matches(code, @"([A-Za-z_][\w]*(?:\??\.[A-Za-z_]\w*)*)\s*\(")
            .Select(m => m.Groups[1].Value.Replace("?.", "."))
            .Where(nom => !motsCles.Contains(nom))
            .ToHashSet(StringComparer.Ordinal);
    }
}

/// <summary>File du texte attendu (M1.7) et indicateur de couche (M1.8).</summary>
public sealed class Regles130PurLeconsTests
{
    private const uint SC_A = 0x10;
    private const uint VK_LCONTROL = 0xA2, SC_LCONTROL = 0x1D;

    // M1.7 I1 : l'ordre des frappes physiques est celui des caractères rendus (FIFO), y compris
    // pour une entrée de plusieurs caractères ; la file vide rend le caractère reçu.
    [Fact]
    public void M1_7_I1_FileFifo_RendLesAttendusDansLOrdreDesFrappes()
    {
        var file = new PhysicalTextInputBuffer(1000, () => 0);
        file.Enqueue("a");
        file.Enqueue("b");
        file.Enqueue("cd");
        Assert.True(file.HasPending);   // témoin : la file contient bien trois entrées

        string rendus = new string(new[]
        {
            file.Resolve('1'), file.Resolve('2'), file.Resolve('3'), file.Resolve('4'), file.Resolve('5'),
        });
        Assert.Equal("abcd5", rendus);
    }

    // M1.7 I2 : un caractère de contrôle reçu (Ctrl+lettre, Retour arrière, Entrée, Tab) n'est
    // jamais remplacé par le texte attendu, et ne consomme pas une entrée qui attend du texte.
    [Fact]
    public void M1_7_I2_CaractereDeControle_JamaisRemplaceParDuTexte()
    {
        var file = new PhysicalTextInputBuffer(1000, () => 0);
        file.Enqueue("a");
        // Témoin : un caractère ordinaire reçu est bien remplacé par l'attendu.
        Assert.Equal('a', file.Resolve('x'));
        Assert.False(file.HasPending);

        file.Enqueue("é");
        Assert.Equal('\u0001', file.Resolve('\u0001'));   // Ctrl+A
        Assert.Equal('\b', file.Resolve('\b'));
        Assert.Equal('\r', file.Resolve('\r'));
        Assert.Equal('\t', file.Resolve('\t'));
        Assert.True(file.HasPending);                       // l'entrée « é » n'a pas été consommée
        Assert.Equal('é', file.Resolve('x'));
    }

    // M1.7 I3 : aucun texte n'est attendu pour un raccourci (Ctrl, Alt ou Win tenu, hors Alt Gr).
    [Fact]
    public void M1_7_I3_Raccourci_AucunTexteAttendu()
    {
        Assert.True(PhysicalTextInputBuffer.IsShortcutChord(ctrl: true, alt: false, win: false, altGr: false));
        Assert.True(PhysicalTextInputBuffer.IsShortcutChord(ctrl: false, alt: true, win: false, altGr: false));
        Assert.True(PhysicalTextInputBuffer.IsShortcutChord(ctrl: false, alt: false, win: true, altGr: false));
        Assert.False(PhysicalTextInputBuffer.IsShortcutChord(ctrl: true, alt: true, win: false, altGr: true));   // Alt Gr écrit

        var mock = new MockWin32Api();
        var layout = LayoutLoader.LoadFromResource();
        var mapper = new KeyMapper(layout, mock);

        // Témoin : sans modificateur, la frappe de la touche A attend bien un texte.
        var sansCtrl = new PhysicalTextInputBuffer(1000, () => 0);
        sansCtrl.CaptureKey(layout, SC_A, mapper);
        Assert.True(sansCtrl.HasPending);

        // Ctrl tenu : Ctrl+A est un raccourci, la file reste vide.
        mock.AsyncKeyStateScript[(int)VK_LCONTROL] = unchecked((short)0x8000);
        mapper.TrackModifiers(VK_LCONTROL, SC_LCONTROL, 0, true);
        Assert.True(mapper.CtrlDown);
        var raccourci = new PhysicalTextInputBuffer(1000, () => 0);
        raccourci.CaptureKey(layout, SC_A, mapper);
        Assert.False(raccourci.HasPending);
    }

    // M1.8 I3 : le pinceau de fond appartient à la classe de fenêtre (Windows le détruit au
    // désenregistrement) ; l'indicateur ne le détruit jamais lui-même. Balayage du texte source :
    // aucun appel de suppression d'objet GDI dans LayerIndicatorWindow.cs.
    [Fact]
    public void M1_8_I3_Indicateur_NeDetruitJamaisLePinceauDeLaClasse()
    {
        using var iso = new BancCapture.Isolation();
        ConfigManager.SetMaintainableLayersEnabled(true);
        using (var indicateur = new LayerIndicatorWindow())
        {
            // Témoin : l'indicateur a bien une fenêtre et le pinceau de classe posé par le socle.
            Assert.NotEqual(IntPtr.Zero, BancCapture.Field<IntPtr>(indicateur, "_hWnd"));
            Assert.NotEqual(IntPtr.Zero, BancCapture.Field<IntPtr>(indicateur, "_hBrush"));
        }

        string code = SourcePur.Code("LayerIndicatorWindow.cs");
        Assert.Contains("ApplyClassBackground(", code);   // témoin : le pinceau vient du socle
        Assert.DoesNotContain("DeleteObject", code);
    }
}

/// <summary>Instance unique (M4.1), écritures de config.json (M4.2), politiques et canal (M4.5), toast COM (M4.6).</summary>
public sealed class Regles130PurInstanceEtEcrituresTests
{
    private static bool LireNotifications(string chemin)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(chemin));
        return doc.RootElement.GetProperty("notificationsEnabled").GetBoolean();
    }

    private static string CheminConfig() => Path.Combine(ConfigManager.LogDirectory, "config.json");

    // M4.1 I3 : seul le gagnant écrit (rotation du journal, enregistrement de relance) : ces deux
    // appels viennent après les sorties de la seconde instance et de l'autre session. Avant elles,
    // Main ne touche à ConfigManager que pour lire la langue, journaliser et lire IsPackaged.
    [Fact]
    public void M4_1_I3_SeulLeGagnantEcrit_RotationEtRelanceApresLesSorties()
    {
        string code = SourcePur.Code("Program.cs");
        int main = code.IndexOf("static void Main()", StringComparison.Ordinal);
        Assert.True(main >= 0, "Main introuvable");
        code = code[main..];

        int acquisition = code.IndexOf("AcquireSingleInstance(", StringComparison.Ordinal);
        int autreSession = code.IndexOf("outcome == SingleInstanceOutcome.OtherSession", StringComparison.Ordinal);
        int seconde = code.IndexOf("outcome == SingleInstanceOutcome.SecondInstance", StringComparison.Ordinal);
        int sortieSeconde = seconde < 0 ? -1 : code.IndexOf("return;", seconde, StringComparison.Ordinal);
        Assert.True(acquisition >= 0 && autreSession > acquisition && seconde > autreSession && sortieSeconde > seconde,
            "ordre acquisition, autre session, seconde instance, sortie introuvable dans Main");

        // Avant la sortie de la seconde instance : aucune écriture de ConfigManager.
        var avant = Regex.Matches(code[..sortieSeconde], @"ConfigManager\.(\w+)").Select(m => m.Groups[1].Value).ToHashSet();
        Assert.Subset(new HashSet<string> { "AppLanguageWithoutLoading", "LogCompatEvent", "IsPackaged" }, avant);
        Assert.DoesNotContain("UsageStats.", code[..sortieSeconde]);

        Assert.Equal(1, SourcePur.Occurrences(code, "RotateLogIfNeeded("));
        Assert.Equal(1, SourcePur.Occurrences(code, "RestartRegistration.Register("));
        Assert.True(code.IndexOf("RotateLogIfNeeded(", StringComparison.Ordinal) > sortieSeconde);
        Assert.True(code.IndexOf("RestartRegistration.Register(", StringComparison.Ordinal) > sortieSeconde);
    }

    // M4.1 I5 : la seconde instance ne montre rien en cas d'activation système : la boîte de
    // message n'existe que hors package, et le signal de relance seulement hors activation système.
    // Balayage du texte source (aucune couture dans Main).
    [Fact]
    public void M4_1_I5_SecondeInstance_MuetteEnActivationSysteme()
    {
        string code = SourcePur.Code("Program.cs");
        int seconde = code.IndexOf("outcome == SingleInstanceOutcome.SecondInstance", StringComparison.Ordinal);
        int sortie = seconde < 0 ? -1 : code.IndexOf("return;", seconde, StringComparison.Ordinal);
        Assert.True(seconde >= 0 && sortie > seconde, "bloc de la seconde instance introuvable");
        string bloc = SourcePur.Normalise(code[seconde..sortie]);

        Assert.Equal(1, SourcePur.Occurrences(bloc, "MessageBoxW("));         // témoins : une boîte, un signal
        Assert.Equal(1, SourcePur.Occurrences(bloc, "TrySignalLiveInstance("));
        Assert.Matches(@"if \(!ConfigManager\.IsPackaged\) \{ Win32\.MessageBoxW\(.*?\); \} "
            + @"else if \(!RelaunchSignal\.IsSystemActivation\(args\)\) \{ bool signaled = RelaunchSignal\.TrySignalLiveInstance\(\);", bloc);
    }

    // M4.2 I3 : les écritures partent dans l'ordre des instantanés. Une écriture suspendue au
    // milieu du disque ne laisse pas passer l'écriture d'un instantané plus récent : jamais deux
    // écritures disque en même temps, et le fichier final porte l'instantané le plus récent.
    [Fact]
    public void M4_2_I3_DeuxFlushConcurrents_EcrituresDansLOrdreDesInstantanes()
    {
        using var iso = new BancCapture.Isolation();
        int delaiAvant = ConfigManager.SaveDelayMilliseconds;
        var hookAvant = ConfigManager.DiskWritePhaseForTests;
        ConfigManager.SaveDelayMilliseconds = -1;
        try
        {
            string chemin = CheminConfig();
            ConfigManager.SetNotifications(true);
            ConfigManager.Flush();   // état ancien sur le disque : vrai

            var valeursEcrites = new List<bool>();
            int actifs = 0, maxActifs = 0, appels = 0;
            using var dansLeHook = new ManualResetEventSlim();
            using var reprendre = new ManualResetEventSlim();
            ConfigManager.DiskWritePhaseForTests = () =>
            {
                int n = Interlocked.Increment(ref actifs);
                lock (valeursEcrites)
                {
                    maxActifs = Math.Max(maxActifs, n);
                    try { valeursEcrites.Add(LireNotifications(ConfigManager.BuildTempPath(chemin, Environment.ProcessId))); }
                    catch (IOException) { }
                }
                if (Interlocked.Increment(ref appels) == 1) { dansLeHook.Set(); reprendre.Wait(10_000); }
                else Thread.Sleep(150);   // laisse une écriture concurrente se chevaucher
                Interlocked.Decrement(ref actifs);
            };

            ConfigManager.SetNotifications(false);   // instantané 1
            var a = new Thread(ConfigManager.Flush) { IsBackground = true };
            a.Start();
            Assert.True(dansLeHook.Wait(5_000), "la première écriture n'a pas atteint le disque");

            ConfigManager.SetNotifications(true);    // instantané 2, plus récent
            var b = new Thread(ConfigManager.Flush) { IsBackground = true };
            b.Start();
            Thread.Sleep(300);
            reprendre.Set();
            Assert.True(a.Join(10_000));
            Assert.True(b.Join(10_000));

            Assert.Equal(2, appels);                                  // témoin : deux écritures ont eu lieu
            Assert.Equal(new[] { false, true }, valeursEcrites);      // dans l'ordre des instantanés
            Assert.Equal(1, maxActifs);
            Assert.True(LireNotifications(chemin));                   // le plus récent est sur le disque
        }
        finally
        {
            ConfigManager.DiskWritePhaseForTests = hookAvant;
            ConfigManager.SaveDelayMilliseconds = delaiAvant;
        }
    }

    // M4.2 I7 : jamais de fichier tronqué : au moment du remplacement, l'ancien config.json est
    // intact et le fichier temporaire complet ; après, le nouveau contenu est en place et le
    // temporaire a disparu. La vraie coupure de courant n'est pas testable.
    [Fact]
    public void M4_2_I7_EcritureAtomique_AncienFichierIntactEtTemporaireCompletAvantRemplacement()
    {
        using var iso = new BancCapture.Isolation();
        int delaiAvant = ConfigManager.SaveDelayMilliseconds;
        var hookAvant = ConfigManager.DiskWritePhaseForTests;
        ConfigManager.SaveDelayMilliseconds = -1;
        try
        {
            string chemin = CheminConfig();
            string temporaire = ConfigManager.BuildTempPath(chemin, Environment.ProcessId);
            ConfigManager.SetNotifications(true);
            ConfigManager.Flush();   // ancien contenu : vrai
            ConfigManager.SetNotifications(false);

            bool? ancien = null, nouveau = null;
            bool temporaireExiste = false;
            ConfigManager.DiskWritePhaseForTests = () =>
            {
                ancien = LireNotifications(chemin);
                temporaireExiste = File.Exists(temporaire);
                nouveau = LireNotifications(temporaire);
            };
            ConfigManager.Flush();

            Assert.True(temporaireExiste);         // témoin : le crochet a bien joué avant le remplacement
            Assert.Equal(true, ancien);            // l'ancien fichier est intact et se relit
            Assert.Equal(false, nouveau);          // le temporaire est complet
            Assert.False(File.Exists(temporaire));
            Assert.False(LireNotifications(chemin));
        }
        finally
        {
            ConfigManager.DiskWritePhaseForTests = hookAvant;
            ConfigManager.SaveDelayMilliseconds = delaiAvant;
        }
    }

    // M4.5 I3 : un registre illisible vaut « aucune politique » : la lecture qui lève rend
    // PolicySet.None, sans exception, et le résultat est gardé (une seule lecture).
    [Fact]
    public void M4_5_I3_PanneDeRegistre_ValeAucunePolitique()
    {
        using var iso = new BancCapture.Isolation();
        int appels = 0;
        using (PolicyManager.OverrideForTests((_, _) =>
               {
                   appels++;
                   throw new InvalidOperationException("registre illisible (simulé)");
               }))
        {
            var politique = PolicyManager.Current;
            Assert.True(appels > 0);                  // témoin : le lecteur a bien été appelé
            int apresPremiere = appels;
            Assert.Same(PolicySet.None, PolicyManager.Current);
            Assert.Equal(apresPremiere, appels);      // gardé : pas de relecture
            Assert.Same(PolicySet.None, politique);
        }
        ConfigManager.FlushLog();
    }

    // M4.5 I4 : le canal est immuable. Seul champ modifiable d'AppChannel : _override, le crochet
    // de test ; aucune propriété modifiable ; et aucun code de production ne l'appelle.
    [Fact]
    public void M4_5_I4_Canal_ImmuableHorsCrochetDeTest()
    {
        const BindingFlags tous = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public
            | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        var modifiables = typeof(AppChannel).GetFields(tous)
            .Where(f => !f.IsInitOnly && !f.IsLiteral).Select(f => f.Name).ToArray();
        Assert.Equal(new[] { "_override" }, modifiables);   // témoin : le crochet de test est bien le seul
        Assert.DoesNotContain(typeof(AppChannel).GetProperties(tous), p => p.GetSetMethod(true) != null);

        // Aucun fichier de production n'appelle le crochet.
        string src = SourcePur.DossierSrc();
        var appelants = Directory.GetFiles(src, "*.cs")
            .Where(f => Path.GetFileName(f) != "AppChannel.cs")
            .Where(f => SourcePur.SansCommentaires(File.ReadAllText(f)).Contains("AppChannel.OverrideForTests"))
            .Select(Path.GetFileName).ToArray();
        Assert.Empty(appelants);
    }

    // M4.6 I1 : l'enregistrement du serveur d'activation est idempotent : déjà enregistré,
    // Register rend vrai sans créer de seconde fabrique ni de seconde inscription.
    [Fact]
    public void M4_6_I1_Register_Idempotent()
    {
        using var iso = new BancCapture.Isolation();
        var cookie = typeof(ToastActivation).GetField("_registrationCookie", BindingFlags.NonPublic | BindingFlags.Static)!;
        var fabrique = typeof(ToastActivation).GetField("_factoryUnknown", BindingFlags.NonPublic | BindingFlags.Static)!;
        Assert.Equal(0u, (uint)cookie.GetValue(null)!);
        Assert.Equal(IntPtr.Zero, (IntPtr)fabrique.GetValue(null)!);

        cookie.SetValue(null, 0xABCDu);   // état « enregistré » simulé
        try
        {
            Assert.True(ToastActivation.Register());
            Assert.True(ToastActivation.Register());
            Assert.Equal(0xABCDu, (uint)cookie.GetValue(null)!);
            Assert.Equal(IntPtr.Zero, (IntPtr)fabrique.GetValue(null)!);
        }
        finally
        {
            if ((uint)cookie.GetValue(null)! != 0xABCDu) ToastActivation.Unregister();   // une inscription réelle serait révoquée
            else cookie.SetValue(null, 0u);
        }
    }

    // M4.6 I3 : l'abonné du toast ne touche pas l'interface depuis le fil COM : son corps ne fait
    // que des tests de texte et un PostMessageW par la couture. Balayage du texte source.
    [Fact]
    public void M4_6_I3_AbonneDuToast_NeFaitQuePosterUnMessage()
    {
        string code = SourcePur.Code("TrayApplication.cs");
        int debut = code.IndexOf("ToastActivation.Activated +=", StringComparison.Ordinal);
        Assert.True(debut >= 0, "abonné introuvable");
        int fin = code.IndexOf("};", debut, StringComparison.Ordinal);
        Assert.True(fin > debut);
        string corps = code[debut..fin];

        var appels = SourcePur.Appels(corps);
        appels.Remove("ToastActivation.Activated");
        Assert.Contains("Shell.PostMessageW", appels);   // témoin : il poste bien
        Assert.Subset(new HashSet<string> { "Shell.PostMessageW", "args.Contains" }, appels);
    }

    // M4.6 I4 : une activation reçue après Unregister ou DestroyWindow est sans effet : Activate
    // capture toute exception de l'abonné et la journalise ; PostMessageW vers une fenêtre détruite
    // rend faux, sans exception.
    [Fact]
    public void M4_6_I4_ActivationTardive_SansEffet()
    {
        using var iso = new BancCapture.Isolation();
        var activateur = new ToastActivation.NotificationActivator();
        Action<string> leve = _ => throw new InvalidOperationException("abonné qui lève (simulé)");
        ToastActivation.Activated += leve;
        try
        {
            Assert.Null(Record.Exception(() => activateur.Activate("aumid", "action=review", IntPtr.Zero, 0)));
        }
        finally { ToastActivation.Activated -= leve; }
        ConfigManager.FlushLog();
        string journal = File.ReadAllText(Path.Combine(ConfigManager.LogDirectory, "error.log"));
        Assert.Contains("ToastActivation.Activate", journal);

        IntPtr fenetre = Win32.CreateWindowExW(0, "STATIC", "", 0, 0, 0, 0, 0, IntPtr.Zero, IntPtr.Zero,
            Win32.GetModuleHandleW(null), IntPtr.Zero);
        Assert.NotEqual(IntPtr.Zero, fenetre);
        Assert.True(Win32.PostMessageW(fenetre, 0, IntPtr.Zero, IntPtr.Zero));   // témoin : fenêtre vivante
        Win32.DestroyWindow(fenetre);
        Assert.False(Win32.PostMessageW(fenetre, 0x8004, (IntPtr)1, IntPtr.Zero));
    }
}

/// <summary>Statistiques d'usage (M5.1, M5.4), fenêtre Statistiques (M5.6), rappel d'entraînement (M5.12).</summary>
public sealed class Regles130PurStatsTests
{
    private const uint WM_TRAYICON = 0x8001;           // TrayApplication.WM_TRAYICON (WM_APP + 1)
    private const uint NIN_BALLOONTIMEOUT = 0x0404;
    private const uint TimerCopieStats = 4210;          // UsageStatsWindow.TIMER_COPY_FEEDBACK

    private static (TrayApplication Tray, FauxTrayShell Faux, Win32.WNDPROC WndProc) Tray()
    {
        var tray = (TrayApplication)RuntimeHelpers.GetUninitializedObject(typeof(TrayApplication));
        var faux = new FauxTrayShell();
        tray.Shell = faux;
        BancCapture.SetField(tray, "_hWnd", (IntPtr)0x1234);
        BancCapture.SetField(tray, "_tooltip", new TrayApplication.TooltipPublisher());
        BancCapture.SetField(tray, "_wmTaskbarCreated", Win32.RegisterWindowMessageW("TaskbarCreated"));
        var wndProc = (Win32.WNDPROC)Delegate.CreateDelegate(typeof(Win32.WNDPROC), tray,
            typeof(TrayApplication).GetMethod("WndProcCallback", BindingFlags.Instance | BindingFlags.NonPublic)!);
        return (tray, faux, wndProc);
    }

    // M5.1 I1 : un seul écrivain du fichier de statistiques à la fois. Deux Flush concurrents, le
    // premier suspendu au milieu du disque : jamais deux écritures en même temps.
    [Fact]
    public void M5_1_I1_DeuxFlushConcurrents_UnSeulEcrivainADeLaFois()
    {
        using var iso = new BancCapture.Isolation();
        using var canal = AppChannel.OverrideForTests(DistributionChannel.Unpackaged);
        var hookAvant = UsageStats.WritePhaseForTests;
        try
        {
            int actifs = 0, maxActifs = 0, appels = 0;
            var verrou = new object();
            using var dansLeHook = new ManualResetEventSlim();
            using var reprendre = new ManualResetEventSlim();
            UsageStats.WritePhaseForTests = () =>
            {
                int n = Interlocked.Increment(ref actifs);
                lock (verrou) maxActifs = Math.Max(maxActifs, n);
                if (Interlocked.Increment(ref appels) == 1) { dansLeHook.Set(); reprendre.Wait(10_000); }
                else Thread.Sleep(150);
                Interlocked.Decrement(ref actifs);
            };

            UsageStats.RecordEmittedText("É");
            var a = new Thread(UsageStats.Flush) { IsBackground = true };
            a.Start();
            Assert.True(dansLeHook.Wait(5_000), "la première écriture n'a pas atteint le disque");

            UsageStats.RecordEmittedText("É");   // le cache est de nouveau à écrire
            var b = new Thread(UsageStats.Flush) { IsBackground = true };
            b.Start();
            Thread.Sleep(300);
            reprendre.Set();
            Assert.True(a.Join(10_000));
            Assert.True(b.Join(10_000));

            Assert.Equal(2, appels);   // témoin : deux écritures ont bien eu lieu
            Assert.Equal(1, maxActifs);
        }
        finally { UsageStats.WritePhaseForTests = hookAvant; }
    }

    // M5.1 I4 : le hook n'effectue jamais d'E/S de statistiques. Garanti parce que Preload charge
    // le fichier au démarrage, avant la pose du hook : (1) Preload marque le fichier lu (la frappe
    // suivante ne le relit pas) ; (2) TrayApplication appelle Preload avant la première pose du hook.
    [Fact]
    public void M5_1_I4_Preload_ChargeAvantLeHook_LaFrappeNeRelitPasLeFichier()
    {
        using var iso = new BancCapture.Isolation();
        using var canal = AppChannel.OverrideForTests(DistributionChannel.Unpackaged);
        string chemin = Path.Combine(ConfigManager.LogDirectory, "usage-stats.json");

        // Un fichier portant cinq caractères enrichis, mis de côté.
        for (int i = 0; i < 5; i++) UsageStats.RecordEmittedText("É");
        UsageStats.Flush();
        byte[] avecCinq = File.ReadAllBytes(chemin);
        Assert.Equal(5, UsageStats.AccentedUppercaseCount);   // témoin : le compteur vaut bien 5

        // Fichier absent au Preload ; il réapparaît ensuite, comme une lecture paresseuse l'aurait vu.
        File.Delete(chemin);
        UsageStats.OverrideStatsPathForTests(chemin);
        UsageStats.Preload();
        File.WriteAllBytes(chemin, avecCinq);
        UsageStats.RecordEmittedText("É");
        Assert.Equal(1, UsageStats.AccentedUppercaseCount);   // pas de relecture du fichier par la frappe

        string code = SourcePur.Code("TrayApplication.cs");
        int preload = code.IndexOf("UsageStats.Preload();", StringComparison.Ordinal);
        var pose = Regex.Match(code, @"_hook!?\.Install\(\)");
        Assert.True(preload >= 0 && pose.Success);
        Assert.True(preload < pose.Index, "Preload doit précéder la première pose du hook");
    }

    // M5.4 I1 : la profondeur d'exclusion n'est jamais négative : deux fermetures en trop ne
    // doivent pas la faire descendre sous zéro, sinon la prochaine ouverture n'exclurait rien.
    [Fact]
    public void M5_4_I1_FermeturesEnTrop_LaProfondeurNeDevientJamaisNegative()
    {
        using var iso = new BancCapture.Isolation();
        using var canal = AppChannel.OverrideForTests(DistributionChannel.Unpackaged);
        while (UsageStats.IsTypingExcluded) UsageStats.EndExcludedTyping();   // part de zéro

        UsageStats.EndExcludedTyping();
        UsageStats.EndExcludedTyping();   // deux fermetures en trop
        UsageStats.BeginExcludedTyping();
        long avant = UsageStats.TotalSpecialCharsCount;
        UsageStats.RecordEmittedText("É");
        Assert.Equal(avant, UsageStats.TotalSpecialCharsCount);   // la frappe est bien exclue
        Assert.True(UsageStats.IsTypingExcluded);

        UsageStats.EndExcludedTyping();
        Assert.False(UsageStats.IsTypingExcluded);
    }

    // M5.4 I3 : l'exclusion ne couvre que la frappe du tutoriel : BeginExcludedTyping n'a qu'un
    // appelant de production, BeginTutorial ; sans lui, la frappe ordinaire est comptée.
    [Fact]
    public void M5_4_I3_Exclusion_SeulBeginTutorialOuvreLaPlage()
    {
        using var iso = new BancCapture.Isolation();
        using var canal = AppChannel.OverrideForTests(DistributionChannel.Unpackaged);
        while (UsageStats.IsTypingExcluded) UsageStats.EndExcludedTyping();
        long avant = UsageStats.TotalSpecialCharsCount;
        UsageStats.RecordEmittedText("É");
        Assert.True(UsageStats.TotalSpecialCharsCount > avant);   // témoin : hors plage, la frappe est comptée

        string src = SourcePur.DossierSrc();
        var appelants = Directory.GetFiles(src, "*.cs")
            .SelectMany(f => Regex.Matches(SourcePur.SansCommentaires(File.ReadAllText(f)), @"\bBeginExcludedTyping\(\)")
                .Select(_ => Path.GetFileName(f)))
            .OrderBy(n => n, StringComparer.Ordinal).ToArray();
        // La définition (UsageStats.cs) et l'unique appel (le tutoriel).
        Assert.Equal(new[] { "LessonsWindow.Tutorial.cs", "UsageStats.cs" }, appelants);

        string tutoriel = SourcePur.Code("LessonsWindow.Tutorial.cs");
        int methode = tutoriel.IndexOf("internal void BeginTutorial()", StringComparison.Ordinal);
        int appel = tutoriel.IndexOf("UsageStats.BeginExcludedTyping()", StringComparison.Ordinal);
        int suivante = tutoriel.IndexOf("\n    internal ", methode + 10, StringComparison.Ordinal);
        Assert.True(methode >= 0 && appel > methode && (suivante < 0 || appel < suivante),
            "l'appel doit être dans BeginTutorial");
    }

    // M5.6 I2 : la minuterie de copie est détruite à la fermeture. NON GARANTI par le produit
    // (constat) : Close() masque la fenêtre sans KillTimer ; la minuterie ne meurt que dans son
    // propre WM_TIMER (1500 ms) ou à DestroyWindow. Le test échoue sur sa dernière assertion.
    [Fact]
    public void M5_6_I2_MinuterieDeCopie_DetruiteALaFermeture()
    {
        using var iso = new BancCapture.Isolation();
        // Témoin (première fenêtre, détruite avant la seconde : la classe est partagée) : sans
        // fermeture, la copie arme bien une minuterie, donc KillTimer rend vrai.
        var temoin = new UsageStatsWindow();
        try
        {
            temoin.ClipboardWriter = (_, _) => true;
            Assert.NotEqual(IntPtr.Zero, BancCapture.Handle(temoin));
            BancCapture.Call(temoin, "OnCopyStats");
            Assert.True(Win32.KillTimer(BancCapture.Handle(temoin), (UIntPtr)TimerCopieStats));
        }
        finally { BancCapture.Teardown(temoin); }

        var fenetre = new UsageStatsWindow();
        try
        {
            fenetre.ClipboardWriter = (_, _) => true;
            Assert.NotEqual(IntPtr.Zero, BancCapture.Handle(fenetre));
            BancCapture.Call(fenetre, "OnCopyStats");
            Assert.True(BancCapture.Field<bool>(fenetre, "_showCopiedFeedback"));
            fenetre.Close();
            Assert.False(Win32.KillTimer(BancCapture.Handle(fenetre), (UIntPtr)TimerCopieStats),
                "M5.6 I2 : la minuterie de copie (4210) vit encore après Close()");
        }
        finally { BancCapture.Teardown(fenetre); }
    }

    // M5.12 I3 : trois rappels ignorés arrêtent définitivement. Par le câblage réel : trois
    // NIN_BALLOONTIMEOUT sur une bulle d'entraînement en attente, puis la décision de rappel.
    [Fact]
    public void M5_12_I3_TroisRappelsIgnores_ArretDefinitif()
    {
        using var iso = new BancCapture.Isolation();
        int delaiAvant = ConfigManager.SaveDelayMilliseconds;
        ConfigManager.SaveDelayMilliseconds = -1;
        try
        {
            var (tray, _, wndProc) = Tray();
            var genre = typeof(TrayApplication).GetNestedType("PendingBalloonKind", BindingFlags.NonPublic)!;
            for (int i = 0; i < 3; i++)
            {
                BancCapture.SetField(tray, "_pendingBalloon", Enum.Parse(genre, "Training"));
                wndProc((IntPtr)0x1234, WM_TRAYICON, IntPtr.Zero, (IntPtr)NIN_BALLOONTIMEOUT);
            }
            Assert.Equal(3u, ConfigManager.TrainingIgnoredCount);   // témoin : le câblage compte les ignorés

            var signaux = TrainingReminders.Snapshot() with
            {
                Enabled = true, ChallengeAvailable = true, SequenceIndex = 0, SessionLocked = false,
                LastSessionDate = null, LastReminderDate = null, ReviewPromptLastShown = null,
            };
            var soir = new DateTime(2030, 1, 1, 18, 0, 0);
            Assert.True(TrainingReminders.ShouldRemind(soir, signaux with { IgnoredCount = 2 }));   // témoin : deux ignorés, rappel
            Assert.False(TrainingReminders.ShouldRemind(soir, signaux));
            Assert.False(TrainingReminders.ShouldRemind(soir.AddDays(40), signaux));                // définitif
        }
        finally { ConfigManager.SaveDelayMilliseconds = delaiAvant; }
    }
}

/// <summary>Magasin de progression (M5.7), exercice (M5.8), maîtrise d'un geste (M5.9).</summary>
public sealed class Regles130PurProgressionTests : IDisposable
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 17, 12, 0, 0, TimeSpan.Zero);
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "AZGPUR_" + Guid.NewGuid().ToString("N"));

    public Regles130PurProgressionTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        LessonProgressStore.TakeQuarantineNotice();   // un fichier mis de côté arme un drapeau statique
        try { Directory.Delete(_dir, true); } catch { }
    }

    private string Chemin(string nom = "lessons-progress.json") => Path.Combine(_dir, nom);

    private const string GesteDu =
        "{\"version\":1,\"gestures\":{\"é\":{\"attempts\":1,\"errors\":0,\"box\":1," +
        "\"lastReviewUtc\":\"2026-11-01T00:00:00.0000000+00:00\",\"dueUtc\":\"2026-11-02T00:00:00.0000000+00:00\"}},\"exercises\":{}}";

    // M5.7 I2 : un rechargement conditionnel ne remplace pas un état échoué. Un fichier d'un format
    // plus récent met le magasin en échec ; le fichier devient ensuite valide (date d'écriture
    // changée) : le magasin reste en échec et ne le relit pas.
    [Fact]
    public void M5_7_I2_RechargementConditionnel_NeRemplacePasUnEtatEchoue()
    {
        using var iso = new BancCapture.Isolation();
        string chemin = Chemin();
        File.WriteAllText(chemin, "{\"version\":99,\"exercises\":{}}");
        var magasin = new LessonProgressStore(chemin, () => T0);

        File.WriteAllText(chemin, GesteDu);
        File.SetLastWriteTimeUtc(chemin, new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        // Témoin : un magasin neuf lit ce fichier et y trouve le geste dû.
        Assert.Single(new LessonProgressStore(chemin, () => T0).DueGestures(10));
        Assert.Empty(magasin.DueGestures(10));
    }

    // M5.7 I3 : un chargement ne lève jamais d'exception sur un JSON valide de forme inattendue
    // (racine non objet, collections de mauvais type, valeurs de mauvais type).
    [Fact]
    public void M5_7_I3_JsonValideDeFormeInattendue_ChargementSansException()
    {
        using var iso = new BancCapture.Isolation();
        // Témoin : un fichier ordinaire se charge et rend ses gestes.
        string temoin = Chemin("temoin.json");
        File.WriteAllText(temoin, GesteDu);
        Assert.Single(new LessonProgressStore(temoin, () => T0).Gestures);

        var formes = new[]
        {
            "[]", "42", "\"texte\"", "null", "true",
            "{\"version\":\"1\"}", "{\"version\":1.5}", "{\"version\":true}", "{\"version\":null}",
            "{\"version\":1,\"exercises\":[1,2]}", "{\"version\":1,\"exercises\":5}", "{\"version\":1,\"exercises\":null}",
            "{\"version\":1,\"exercises\":{\"a\":5,\"b\":null,\"c\":[],\"d\":\"x\",\"e\":{\"hash\":7}}}",
            "{\"version\":1,\"exercises\":{\"f\":{\"hash\":\"h\",\"completed\":\"oui\",\"successfulAttempts\":\"3\"," +
                "\"bestWpm\":{},\"bestAccuracyPercent\":[],\"bestSeconds\":\"x\",\"hintsUsed\":1e99,\"lastCompletedUtc\":12,\"errorMatrix\":1}}}",
            "{\"version\":1,\"gestures\":[1]}", "{\"version\":1,\"gestures\":\"x\"}",
            "{\"version\":1,\"gestures\":{\"a\":5,\"b\":null,\"c\":[],\"\":{},\"d\":{\"attempts\":\"x\",\"box\":1e99,\"errors\":-5,\"lastReviewUtc\":3,\"dueUtc\":{}}}}",
            "{\"version\":1,\"onboardingSyncedExercises\":\"x\"}", "{\"version\":1,\"onboardingSyncedExercises\":{}}",
            "{\"version\":1,\"onboardingSyncedExercises\":[\"a\",{},-5,1.5,null,99999999999]}",
            "{\"version\":1,\"onboardingSyncedMaxStep\":\"7\"}", "{\"version\":1,\"onboardingSyncedMaxStep\":-3}",
            "{\"version\":1,\"lastModuleId\":5,\"lastLessonId\":[],\"lastExerciseIndex\":\"3\"}",
        };
        var echecs = new List<string>();
        for (int i = 0; i < formes.Length; i++)
        {
            string chemin = Chemin($"forme{i}.json");
            File.WriteAllText(chemin, formes[i]);
            var exception = Record.Exception(() =>
            {
                var magasin = new LessonProgressStore(chemin, () => T0);
                _ = magasin.DueGestures(5);
                _ = magasin.Exercises.Count;
                _ = magasin.OnboardingSyncedMaxStep;
            });
            if (exception != null) echecs.Add($"{formes[i]} => {exception.GetType().Name}: {exception.Message}");
        }
        Assert.True(echecs.Count == 0, "chargement levé sur : " + string.Join(" | ", echecs));
    }

    // M5.7 I4 : ResetAll remet tout l'état de progression : exercices, gestes, dernière position
    // (module, leçon, rang) et rangs importés, dans le magasin et sur le disque.
    [Fact]
    public void M5_7_I4_ResetAll_RemetToutLEtatDeProgression()
    {
        using var iso = new BancCapture.Isolation();
        string chemin = Chemin();
        var catalogue = LessonCatalogLoader.LoadFromResource();
        var exercice = new LessonExercise("m", "l", 2, "practice", "Tape", "é", LessonTypingMode.Flexible);
        var session = new LessonTypingSession(exercice, () => T0);
        session.TypeChar('é');
        var magasin = new LessonProgressStore(chemin, () => T0);
        magasin.RecordSuccess(exercice, session.Stats, c => c == 'é');
        magasin.SetLastPosition(exercice);
        magasin.SyncOnboardingProgress(catalogue, 3);

        // Témoins : tout l'état est non vide avant la remise à zéro.
        Assert.NotEmpty(magasin.Exercises);
        Assert.NotEmpty(magasin.Gestures);
        Assert.Equal("m", magasin.LastModuleId);
        Assert.Equal("l", magasin.LastLessonId);
        Assert.Equal(2, magasin.LastExerciseIndex);
        Assert.Equal(3, magasin.OnboardingSyncedMaxStep);

        magasin.ResetAll(onboardingSyncedMaxStep: 0);
        var relu = new LessonProgressStore(chemin, () => T0);
        foreach (var m in new[] { magasin, relu })
        {
            Assert.Empty(m.Exercises);
            Assert.Empty(m.Gestures);
            Assert.Empty(m.OnboardingSyncedExercises);
            Assert.Null(m.LastModuleId);
            Assert.Null(m.LastLessonId);
            Assert.Equal(0, m.LastExerciseIndex);
        }
    }

    private static string Entree(string cle, string empreinte, int tentatives, int wpm) =>
        $"    \"{cle}\": {{ \"hash\": \"{empreinte}\", \"completed\": true, \"successfulAttempts\": {tentatives}, " +
        $"\"bestWpm\": {wpm}, \"bestAccuracyPercent\": 90, \"bestSeconds\": 10, \"lastCompletedUtc\": null, \"hintsUsed\": 0 }}";

    // M5.8 I1 : une migration d'empreinte n'écrase pas une progression plus récente : quand
    // l'entrée existe déjà à l'empreinte actuelle, elle prime sur la fusion des anciennes, en
    // lecture comme à l'écriture suivante.
    [Fact]
    public void M5_8_I1_MigrationDEmpreinte_LEntreeActuellePrimeSurLesAnciennes()
    {
        using var iso = new BancCapture.Isolation();
        var catalogue = LessonCatalogLoader.LoadFromResource();
        var exercice = catalogue.Exercises.First(e => e.LegacyHashes.Count > 0);
        string ancienne = exercice.LegacyHashes[0];
        string chemin = Chemin();
        File.WriteAllText(chemin, "{\n  \"version\": 1,\n  \"exercises\": {\n"
            + Entree(exercice.KeyPrefix + ancienne, ancienne, tentatives: 4, wpm: 30) + ",\n"
            + Entree(exercice.StableKey, exercice.Hash, tentatives: 1, wpm: 55) + "\n  }\n}\n");

        var magasin = new LessonProgressStore(chemin);
        // Témoin : la fusion des anciennes empreintes seule donnerait autre chose (4 réussites, 30 mots/min).
        var fusion = magasin.MergeLegacy(exercice);
        Assert.NotNull(fusion);
        Assert.Equal(4, fusion!.SuccessfulAttempts);

        var courante = magasin.GetValidProgress(exercice);
        Assert.Equal(1, courante!.SuccessfulAttempts);
        Assert.Equal(55, courante.BestWpm);

        magasin.MarkCompletedNeutral(exercice);   // écriture sous l'empreinte actuelle
        var relu = new LessonProgressStore(chemin).Exercises[exercice.StableKey];
        Assert.Equal(1, relu.SuccessfulAttempts);
        Assert.Equal(55, relu.BestWpm);
    }

    // M5.9 I2 : un geste est dû selon l'intervalle de sa boîte : 0, 1, 3, 7, 16, 35 jours ;
    // dû à l'échéance exacte, pas avant.
    [Fact]
    public void M5_9_I2_GesteDuSelonLIntervalleDeSaBoite()
    {
        int[] jours = { 0, 1, 3, 7, 16, 35 };
        for (int boite = 0; boite <= GestureMastery.MaxBox; boite++)
            Assert.Equal(TimeSpan.FromDays(jours[boite]), GestureMastery.IntervalFor(boite));

        Assert.True(new GestureMasteryEntry("é").IsDue(T0));   // jamais revu : dû d'emblée
        for (int boite = 1; boite <= GestureMastery.MaxBox; boite++)
        {
            var geste = new GestureMasteryEntry("é") { Box = boite - 1 };
            GestureMastery.Apply(geste, attempts: 1, errors: 0, T0);   // dû : monte d'une boîte
            Assert.Equal(boite, geste.Box);
            var echeance = T0 + TimeSpan.FromDays(jours[boite]);
            Assert.Equal(echeance, geste.DueUtc);
            Assert.False(geste.IsDue(echeance - TimeSpan.FromSeconds(1)), $"boîte {boite} : dû avant l'échéance");
            Assert.True(geste.IsDue(echeance), $"boîte {boite} : pas dû à l'échéance");
        }
    }

    // M5.9 I3 : une horloge qui recule ne rend pas un geste dû en avance : l'échéance est absolue.
    [Fact]
    public void M5_9_I3_HorlogeQuiRecule_NeRendPasUnGesteDuEnAvance()
    {
        var reussi = new GestureMasteryEntry("é") { Box = 2 };
        GestureMastery.Apply(reussi, 1, 0, T0);           // boîte 3, échéance T0 + 7 jours
        var faute = new GestureMasteryEntry("è");
        GestureMastery.Apply(faute, 1, 1, T0);            // faute : échéance T0
        Assert.False(reussi.IsDue(T0 + TimeSpan.FromDays(6)));   // témoins : l'échéance tient en avançant
        Assert.True(reussi.IsDue(T0 + TimeSpan.FromDays(7)));
        Assert.True(faute.IsDue(T0));

        var recule = T0 - TimeSpan.FromDays(10);          // l'horloge recule avant la dernière revue
        Assert.False(reussi.IsDue(recule));
        Assert.False(faute.IsDue(recule));
        Assert.Empty(GestureMastery.Due(new[] { reussi, faute }, recule, 10));
    }
}

/// <summary>Demande d'avis (M7.6), boîte d'avis du Store (M7.7), Paramètres (M8.1).</summary>
public sealed class Regles130PurAvisEtParametresTests
{
    private const uint WM_APP_TOAST = 0x8004;   // TrayApplication.WM_APP_TOAST (WM_APP + 4)

    // M7.6 I5 : un toast d'avis plus ancien, cliqué après la réponse, ne produit qu'une ouverture
    // sans nouvel effet d'état : le second clic n'écrit plus config.json. Par le câblage réel
    // (WM_APP_TOAST posté par l'abonné du toast) et la couture du shell (liens ouverts comptés).
    [Fact]
    public void M7_6_I5_ToastAncienClique_OuvreSansNouvelEffetDEtat()
    {
        using var iso = new BancCapture.Isolation();
        int delaiAvant = ConfigManager.SaveDelayMilliseconds;
        ConfigManager.SaveDelayMilliseconds = -1;
        try
        {
            var tray = (TrayApplication)RuntimeHelpers.GetUninitializedObject(typeof(TrayApplication));
            var faux = new FauxTrayShell();
            tray.Shell = faux;
            BancCapture.SetField(tray, "_hWnd", (IntPtr)0x1234);
            var wndProc = (Win32.WNDPROC)Delegate.CreateDelegate(typeof(Win32.WNDPROC), tray,
                typeof(TrayApplication).GetMethod("WndProcCallback", BindingFlags.Instance | BindingFlags.NonPublic)!);
            string chemin = Path.Combine(ConfigManager.LogDirectory, "config.json");

            Assert.False(ConfigManager.ReviewPromptClicked);
            wndProc((IntPtr)0x1234, WM_APP_TOAST, (IntPtr)1, IntPtr.Zero);   // premier clic : la réponse
            ConfigManager.Flush();
            Assert.True(ConfigManager.ReviewPromptClicked);                    // témoin : l'état change au premier clic
            int ecritures = ConfigManager.DiskWriteCount;
            byte[] contenu = File.ReadAllBytes(chemin);

            wndProc((IntPtr)0x1234, WM_APP_TOAST, (IntPtr)1, IntPtr.Zero);   // toast ancien cliqué après la réponse
            ConfigManager.Flush();
            Assert.Equal(2, faux.Liens.Count);                                 // il ouvre bien le lien
            Assert.Equal(ecritures, ConfigManager.DiskWriteCount);
            Assert.Equal(contenu, File.ReadAllBytes(chemin));
        }
        finally { ConfigManager.SaveDelayMilliseconds = delaiAvant; }
    }

    // M7.7 I2 : le rappel de fin de la boîte d'avis du Store ne touche ni l'état ni les fenêtres :
    // il n'appelle que ShellExecuteW (repli), le journal et la lecture du résultat. Balayage du
    // texte source (rappel exécuté sur le pool, WinRT, hors portée d'un montage de test).
    [Fact]
    public void M7_7_I2_RappelDeFin_NeFaitQueOuvrirLeLienEtJournaliser()
    {
        string code = SourcePur.Code("StoreReview.cs");
        int methode = code.IndexOf("private static void OnCompleted(", StringComparison.Ordinal);
        int corps = methode < 0 ? -1 : code.IndexOf("try", methode, StringComparison.Ordinal);
        Assert.True(corps > methode && methode >= 0, "OnCompleted introuvable");

        var appels = SourcePur.Appels(code[corps..]);
        Assert.Contains("Win32.ShellExecuteW", appels);   // témoins : le repli et le journal sont bien là
        Assert.Contains("ConfigManager.Log", appels);
        Assert.Subset(new HashSet<string>
        {
            "Win32.ShellExecuteW", "ConfigManager.Log", "operation.GetResults", "InvalidOperationException",
        }, appels);
    }

    // M8.1 I3 : aucune écriture de configuration pendant la pause d'une case affichée cochée :
    // garanti par construction, la pause des Paramètres n'est jamais armée en production. Balayage
    // du texte source : aucun appel de production de SettingsWindow.SetInputPaused.
    [Fact]
    public void M8_1_I3_PauseDesParametres_JamaisArmeeEnProduction()
    {
        string src = SourcePur.DossierSrc();
        var recepteurs = new HashSet<string>(StringComparer.Ordinal);
        var typesParametres = new HashSet<string>(StringComparer.Ordinal);
        foreach (string fichier in Directory.GetFiles(src, "*.cs"))
        {
            string code = SourcePur.SansCommentaires(File.ReadAllText(fichier));
            foreach (Match m in Regex.Matches(code, @"(\w+)\??\.SetInputPaused\("))
                recepteurs.Add(m.Groups[1].Value);
            foreach (Match m in Regex.Matches(code, @"\bSettingsWindow\??\s+(\w+)"))
                typesParametres.Add(m.Groups[1].Value);
            foreach (Match m in Regex.Matches(code, @"(\w+)\s*(?:\?\?)?=\s*new SettingsWindow\b"))
                typesParametres.Add(m.Groups[1].Value);
        }
        // Témoins : la méthode existe, d'autres fenêtres sont bien pausées, et le balayage voit la fenêtre.
        Assert.Contains("void SetInputPaused(", SourcePur.Code("SettingsWindow.cs"));
        Assert.Superset(new HashSet<string> { "_characterSearch", "_lessons" }, recepteurs);
        Assert.Contains("_settings", typesParametres);
        Assert.Empty(recepteurs.Intersect(typesParametres));
    }
}

/// <summary>Liens STATIC (M7.8) : même montage que LinkBehaviorTests (fenêtre message-only, vrai STATIC).</summary>
public sealed class Regles130PurLiensTests : IDisposable
{
    private static readonly IntPtr HWND_MESSAGE = new(-3);
    private const uint SS_NOTIFY = 0x0100;
    private const int LinkId = 42;
    private const int VK_RETURN = 0x0D;

    private readonly string _className = "AZERTYGlobal.Tests.LiensPur." + Guid.NewGuid().ToString("N");
    private readonly Win32.WNDPROC _ownerProc;
    private readonly List<(IntPtr WParam, IntPtr LParam)> _commands = new();
    private readonly IntPtr _owner;
    private readonly IntPtr _link;

    public Regles130PurLiensTests()
    {
        _ownerProc = (h, m, w, l) =>
        {
            if (m == Win32.WM_COMMAND)
                _commands.Add((w, l));
            return Win32.DefWindowProcW(h, m, w, l);
        };
        Assert.True(NativeWindow.RegisterClass(_className, _ownerProc));
        IntPtr module = Win32.GetModuleHandleW(null);
        _owner = Win32.CreateWindowExW(0, _className, string.Empty, 0, 0, 0, 0, 0,
            HWND_MESSAGE, IntPtr.Zero, module, IntPtr.Zero);
        _link = Win32.CreateWindowExW(0, "STATIC", "lien",
            Win32.WS_CHILD | SS_NOTIFY | Win32.WS_TABSTOP, 0, 0, 80, 20,
            _owner, (IntPtr)LinkId, module, IntPtr.Zero);
        Assert.NotEqual(IntPtr.Zero, _owner);
        Assert.NotEqual(IntPtr.Zero, _link);
    }

    public void Dispose()
    {
        Win32.DestroyWindow(_owner);
        NativeWindow.UnregisterClass(_className);
        GC.KeepAlive(_ownerProc);
    }

    // M7.8 I3 : un lien activé à Entrée n'ouvre qu'une URL par appui. NON GARANTI par le produit
    // (constat) : WM_KEYDOWN est traité sans tester le bit de répétition (bit 30 de lParam), donc
    // Entrée maintenue ouvre l'URL à chaque répétition. Le test échoue sur sa dernière assertion.
    [Fact]
    public void M7_8_I3_EntreeMaintenue_NOuvreQuUneUrlParAppui()
    {
        var liens = new LinkBehavior(() => _owner, () => { }, focusFrame: true);
        liens.Attach(_link, 7);
        try
        {
            IntPtr premierAppui = (IntPtr)1;                      // répétition 1, bit 30 levé
            IntPtr repetition = (IntPtr)((1 << 30) | 1);          // bit 30 posé : touche déjà enfoncée
            Win32.SendMessageW(_link, Win32.WM_KEYDOWN, (IntPtr)VK_RETURN, premierAppui);
            Assert.Single(_commands);                              // témoin : l'appui ouvre bien une fois

            Win32.SendMessageW(_link, Win32.WM_KEYDOWN, (IntPtr)VK_RETURN, repetition);
            Win32.SendMessageW(_link, Win32.WM_KEYDOWN, (IntPtr)VK_RETURN, repetition);
            Assert.True(_commands.Count == 1,
                $"M7.8 I3 : Entrée maintenue a ouvert {_commands.Count} fois le lien pour un seul appui");
        }
        finally { liens.Detach(); }
    }
}
