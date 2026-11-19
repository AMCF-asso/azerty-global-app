using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Décision d'Antoine du 2026-11-17 (« Garder l'exercice ») : avec <c>LessonsEnabled</c> à 0,
/// « Activer et essayer » de l'accueil ouvre toujours l'exercice d'activation — c'est la prise
/// en main du clavier, pas l'apprentissage ludique. Le parcours passe par
/// <c>ShowTutorialFromOnboarding</c>, que la stratégie ne garde pas ; ce test le prouve de bout
/// en bout (bouton de l'accueil, tray, fenêtre Leçons) au lieu de le laisser à une lecture du code.
/// </summary>
public class ExerciceActivationSansLeconsTests
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;

    private static PolicyValueReader LeconsAZero() =>
        (chemin, nom) => chemin == PolicyManager.KeyPath && nom == PolicyManager.ValueLessons ? 0 : null;

    [Fact]
    public void ActiverEtEssayer_OuvreLExercice_AvecLessonsEnabledAZero()
    {
        using var iso = new BancCapture.Isolation();
        using var politique = PolicyManager.OverrideForTests(LeconsAZero());
        var accueil = OutilsAccueilReglages130.OuvrirAccueil();
        var layout = LayoutLoader.LoadFromResource();
        var mapper = new KeyMapper(layout, new MockWin32Api());
        var hook = new KeyboardHook(mapper); // jamais installé
        var tray = (TrayApplication)RuntimeHelpers.GetUninitializedObject(typeof(TrayApplication));
        LessonsWindow? lecons = null;
        try
        {
            // Témoin : la stratégie est bien en vigueur, les Leçons elles-mêmes sont fermées.
            Assert.False(PolicyManager.LessonsEnabledNow);
            Assert.Equal(new[] { 1010 }, TrayApplication.LearnMenuEntries(challengeEnabled: true,
                lessonsPolicy: false));                                  // « Revoir l'accueil » seul
            BancCapture.SetField(tray, "_activationConsent", true);
            BancCapture.SetField(tray, "_hook", hook);
            BancCapture.SetField(tray, "_mapper", mapper);
            BancCapture.SetField(tray, "_layout", layout);
            BancCapture.Call(tray, "ShowLessonsWindow");
            Assert.Null(BancCapture.Field<LessonsWindow?>(tray, "_lessons"));   // témoin : la porte Leçons reste fermée

            // Le bouton de l'accueil, câblé comme le tray (TrayApplication.cs : TutorialRequested).
            BancCapture.SetField(tray, "_onboarding", accueil);
            accueil.TutorialRequested = (Action)Delegate.CreateDelegate(typeof(Action), tray,
                typeof(TrayApplication).GetMethod("ShowTutorialFromOnboarding", Instance)!);
            BancCapture.Call(accueil, "LaunchTutorial");

            lecons = BancCapture.Field<LessonsWindow?>(tray, "_lessons");
            Assert.NotNull(lecons);
            Assert.True(lecons!.IsTutorialActive, "l'exercice d'activation ne s'est pas ouvert");
            Assert.True(OutilsAccueilReglages130.Bool(accueil, "_tutorialOpen"));
        }
        finally
        {
            if (lecons != null) BancCapture.Teardown(lecons);
            BancCapture.Teardown(accueil);
        }
    }
}

/// <summary>
/// Segmentation Store / MSIX entreprise, 1.3.0 (décisions d'Antoine du 2026-11-17) : trois
/// stratégies de groupe nouvelles — OutboundLinksEnabled, AutoStartEnabled, LessonsEnabled — et
/// le canal hors paquet devenu sobre (ce dernier dans <see cref="SoberChannelTests"/>).
/// </summary>
public class SegmentationStrategiesTests : IDisposable
{
    // La porte et ApplyPolicy journalisent : error.log va dans un dossier jetable, jamais
    // dans celui de l'utilisateur qui lance les tests.
    private readonly string _tempDir;

    public SegmentationStrategiesTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "AZGTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        ConfigManager.OverrideConfigPathForTests(Path.Combine(_tempDir, "config.json"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    private static PolicyValueReader Lecteur(Dictionary<string, object> valeurs) =>
        (chemin, nom) => chemin == PolicyManager.KeyPath && valeurs.TryGetValue(nom, out var v) ? v : null;

    // ── Lecture ───────────────────────────────────────────────────────

    [Fact]
    public void Lecture_LesTroisValeursNouvelles_ZeroUnAbsente()
    {
        var zero = PolicyManager.Read(Lecteur(new()
        {
            [PolicyManager.ValueOutboundLinks] = 0,
            [PolicyManager.ValueAutoStart] = 0,
            [PolicyManager.ValueLessons] = 0,
        }));
        Assert.Equal((false, false, false), (zero.OutboundLinks, zero.AutoStart, zero.Lessons));

        var un = PolicyManager.Read(Lecteur(new()
        {
            [PolicyManager.ValueOutboundLinks] = 1,
            [PolicyManager.ValueAutoStart] = 1,
            [PolicyManager.ValueLessons] = 1,
        }));
        Assert.Equal((true, true, true), (un.OutboundLinks, un.AutoStart, un.Lessons));

        var absent = PolicyManager.Read(Lecteur(new()));
        Assert.Null(absent.OutboundLinks);
        Assert.Null(absent.AutoStart);
        Assert.Null(absent.Lessons);
    }

    /// <summary>Les noms sont une interface publique pour les DSI (note de décision du
    /// 2026-11-17) : un renommage doit rougir ici avant d'atteindre un parc.</summary>
    [Fact]
    public void Noms_SontCeuxDeLaNoteDeDecision()
    {
        Assert.Equal("OutboundLinksEnabled", PolicyManager.ValueOutboundLinks);
        Assert.Equal("AutoStartEnabled", PolicyManager.ValueAutoStart);
        Assert.Equal("LessonsEnabled", PolicyManager.ValueLessons);
    }

    [Fact]
    public void Lecture_ChaîneAuLieuDeDword_NeContraintRien()
    {
        var set = PolicyManager.Read(Lecteur(new() { [PolicyManager.ValueOutboundLinks] = "0" }));
        Assert.Null(set.OutboundLinks);
    }

    // ── OutboundLinksEnabled ──────────────────────────────────────────

    [Theory]
    [InlineData(null, true)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void LiensSortants_SeulZéroContraint(bool? politique, bool attendu) =>
        Assert.Equal(attendu, PolicyManager.OutboundLinksEnabled(politique));

    /// <summary>À 1, la stratégie générale ne rallume rien : ni le canal sobre, ni une
    /// ExternalLinksEnabled à 0. À 0, elle éteint tout, Store compris.</summary>
    [Theory]
    [InlineData("Store", null, null, true)]
    [InlineData("Store", null, true, true)]
    [InlineData("Store", null, false, false)]
    [InlineData("Store", false, true, false)]
    [InlineData("Amcf", null, true, false)]
    [InlineData("Unpackaged", null, true, false)]
    [InlineData("Amcf", true, false, false)]
    [InlineData("Amcf", true, true, true)]
    public void LiensDeSoutien_SousLaStratégieGénérale(string canal, bool? externes, bool? sortants, bool attendu) =>
        Assert.Equal(attendu, PolicyManager.ExternalLinksEnabled(externes,
            Enum.Parse<DistributionChannel>(canal), sortants));

    [Fact]
    public void Menu_LiensSortantsÀZéro_RetireRetoursNotationEtRessources()
    {
        Assert.Empty(TrayApplication.FeedbackMenuEntries(DistributionChannel.Store, null, outboundLinksPolicy: false));
        Assert.Empty(TrayApplication.FeedbackTopLevelEntries(DistributionChannel.Store, null, outboundLinksPolicy: false));
        Assert.Empty(TrayApplication.ResourcesMenuEntries(outboundLinksPolicy: false));
    }

    /// <summary>Par défaut, rien ne change : D4 tient sur tous les canaux tant que la
    /// stratégie n'est pas à 0.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData(true)]
    public void Menu_LiensSortantsNonÀZéro_InchangéD4(bool? sortants)
    {
        Assert.Equal(new[] { TrayApplication.IDM_SUPPORT, TrayApplication.IDM_FEEDBACK, TrayApplication.IDM_DISCORD, TrayApplication.IDM_BUG },
            TrayApplication.FeedbackMenuEntries(DistributionChannel.Store, null, sortants));
        Assert.Equal(new[] { TrayApplication.IDM_FEEDBACK, TrayApplication.IDM_BUG },
            TrayApplication.FeedbackMenuEntries(DistributionChannel.Amcf, null, sortants));
        Assert.Equal(5, TrayApplication.ResourcesMenuEntries(sortants).Length);
    }

    [Fact]
    public void ÀPropos_LiensSortantsÀZéro_AucunLien()
    {
        Assert.Empty(AboutWindow.LinkEntries(DistributionChannel.Store, null, outboundLinksPolicy: false));
        Assert.Empty(AboutWindow.LinkEntries(DistributionChannel.Amcf, null, outboundLinksPolicy: false));
        Assert.Equal(4, AboutWindow.LinkEntries(DistributionChannel.Store, null, outboundLinksPolicy: true).Length);
    }

    /// <summary>Accueil, dernière étape : Leçons, guide, Afrique, avis, Discord.</summary>
    [Theory]
    [InlineData(true, true, true, 5)]
    [InlineData(true, true, false, 4)]
    [InlineData(false, true, true, 4)]
    [InlineData(true, false, true, 1)]
    [InlineData(false, false, true, 0)]
    public void Accueil_LignesDeLiens(bool leçons, bool sortants, bool externes, int lignes) =>
        Assert.Equal(lignes, OnboardingWindow.ResourceLinkEntries(leçons, sortants, externes).Length);

    [Fact]
    public void Porte_RefuseQuandLaStratégieCoupe_SansAppelerLeShell()
    {
        int appels = 0;
        ShellExecuteFn shell = (_, _, _, _, _, _) => { appels++; return IntPtr.Zero; };

        Assert.False(OutboundLinks.Open("https://exemple.invalid", shell, allowed: false));
        Assert.Equal(0, appels);

        string? ouvert = null;
        ShellExecuteFn shell2 = (_, op, file, _, _, _) => { ouvert = op + " " + file; return IntPtr.Zero; };
        Assert.True(OutboundLinks.Open("https://exemple.invalid", shell2, allowed: true));
        Assert.Equal("open https://exemple.invalid", ouvert);
    }

    /// <summary>
    /// « Tous les liens » est une promesse d'exhaustivité faite aux DSI : tout ShellExecuteW
    /// du produit passe par <see cref="OutboundLinks"/>. Ce test lit les sources et rougit sur
    /// un appel neuf qui contournerait la porte.
    /// </summary>
    [Fact]
    public void Sources_AucunShellExecuteHorsDeLaPorte()
    {
        string src = Path.Combine(RacineDuDépôt(), "src");
        var autorisés = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Win32.cs",          // déclaration P/Invoke
            "TrayShell.cs",      // shell injectable de la barre des tâches
            "OutboundLinks.cs",  // la porte
            "FauxTrayShell.cs",  // double de test
        };
        var écarts = new List<string>();
        int lus = 0, porte = 0;
        foreach (var fichier in Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories))
        {
            string rel = Path.GetRelativePath(src, fichier);
            if (rel.Contains("Tests") || rel.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                || rel.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                continue;
            if (autorisés.Contains(Path.GetFileName(fichier))) continue;
            lus++;
            if (File.ReadAllText(fichier).Contains("OutboundLinks.Open(")) porte++;
            int n = 0;
            foreach (var ligne in File.ReadLines(fichier))
            {
                n++;
                if (ligne.TrimStart().StartsWith("//")) continue;
                // Un appel (« ShellExecuteW( »), pas le groupe de méthodes passé à la porte.
                if (Regex.IsMatch(ligne, @"ShellExecuteW\s*\("))
                    écarts.Add($"{rel}:{n}");
            }
        }
        Assert.Empty(écarts);
        // Témoins : le balayage a bien lu le produit, et la porte y a ses appelants.
        Assert.True(lus > 50, $"{lus} fichiers lus");
        Assert.True(porte >= 6, $"{porte} fichiers appellent la porte");
    }

    // ── LessonsEnabled ────────────────────────────────────────────────

    [Theory]
    [InlineData(null, true)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void Leçons_SeulZéroContraint(bool? politique, bool attendu) =>
        Assert.Equal(attendu, PolicyManager.LessonsEnabled(politique));

    [Theory]
    [InlineData(true, null, true)]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, null, false)]
    public void Défi_InterrupteurEtStratégie(bool interrupteur, bool? leçons, bool attendu) =>
        Assert.Equal(attendu, PolicyManager.ChallengeEnabled(interrupteur, leçons));

    [Fact]
    public void Menu_LeçonsÀZéro_NeGardeQueLAccueil()
    {
        Assert.Equal(new[] { TrayApplication.IDM_ONBOARDING },
            TrayApplication.LearnMenuEntries(challengeEnabled: true, lessonsPolicy: false));
        Assert.Equal(new[] { TrayApplication.IDM_EXERCISES, TrayApplication.IDM_CHALLENGE, TrayApplication.IDM_ONBOARDING },
            TrayApplication.LearnMenuEntries(challengeEnabled: true, lessonsPolicy: true));
        Assert.Equal(new[] { TrayApplication.IDM_EXERCISES, TrayApplication.IDM_ONBOARDING },
            TrayApplication.LearnMenuEntries(challengeEnabled: false));
    }

    // ── AutoStartEnabled ──────────────────────────────────────────────

    [Fact]
    public void Démarrage_SansStratégie_RienNEstÉcrit()
    {
        int écritures = 0;
        Assert.True(AutoStart.ApplyPolicy(null, _ => { écritures++; return true; }, () => false));
        Assert.Equal(0, écritures);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Démarrage_ÉtatRéelDifférent_LaStratégieLÉcrit(bool politique, bool inscrit)
    {
        bool? écrit = null;
        Assert.True(AutoStart.ApplyPolicy(politique, v => { écrit = v; return true; }, () => inscrit));
        Assert.Equal(politique, écrit);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Démarrage_ÉtatDéjàConforme_RienNEstÉcrit(bool politique)
    {
        int écritures = 0;
        Assert.True(AutoStart.ApplyPolicy(politique, _ => { écritures++; return true; }, () => politique));
        Assert.Equal(0, écritures);
    }

    /// <summary>Refus exprimé dans Windows : l'application ne le contourne pas, elle le
    /// constate sans boîte (choix d'Antoine du 2026-11-17).</summary>
    [Fact]
    public void Démarrage_RefuséParWindows_RendFauxSansLever()
    {
        Assert.False(AutoStart.ApplyPolicy(true, _ => false, () => false));
        Assert.False(AutoStart.ApplyPolicy(true, _ => throw new InvalidOperationException("WinRT"), () => false));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public void Démarrage_CaseGriséeDèsQueLaStratégieEstPosée(bool? politique, bool grisée) =>
        Assert.Equal(grisée, PolicyManager.IsManaged(politique));

    // ── Modèle ADMX ───────────────────────────────────────────────────

    /// <summary>Chaque stratégie nouvelle a son entrée ADMX, sur la bonne valeur, avec la
    /// polarité voulue : « Masquer… » activé écrit 0, le démarrage activé écrit 1. Et chaque
    /// chaîne référencée existe dans les deux ADML.</summary>
    [Theory]
    [InlineData("RemoveOutboundLinks", "OutboundLinksEnabled", "0", "1")]
    [InlineData("AutoStartEnabled", "AutoStartEnabled", "1", "0")]
    [InlineData("RemoveLessons", "LessonsEnabled", "0", "1")]
    public void Admx_EntréeEtPolarité(string stratégie, string valeur, string activé, string désactivé)
    {
        XNamespace ns = "http://schemas.microsoft.com/GroupPolicy/2006/07/PolicyDefinitions";
        var admx = XDocument.Load(Path.Combine(RacineDuDépôt(), "entreprise", "AZERTYGlobal.admx"));
        var policy = admx.Descendants(ns + "policy").Single(p => (string?)p.Attribute("name") == stratégie);
        Assert.Equal(valeur, (string?)policy.Attribute("valueName"));
        Assert.Equal(activé, (string?)policy.Element(ns + "enabledValue")!.Element(ns + "decimal")!.Attribute("value"));
        Assert.Equal(désactivé, (string?)policy.Element(ns + "disabledValue")!.Element(ns + "decimal")!.Attribute("value"));
    }

    [Theory]
    [InlineData("fr-FR")]
    [InlineData("en-US")]
    public void Adml_ToutesLesChaînesDeLAdmxExistent(string langue)
    {
        string racine = RacineDuDépôt();
        string admx = File.ReadAllText(Path.Combine(racine, "entreprise", "AZERTYGlobal.admx"));
        var adml = XDocument.Load(Path.Combine(racine, "entreprise", langue, "AZERTYGlobal.adml"));
        var ids = adml.Descendants().Where(e => e.Name.LocalName is "string" or "presentation")
            .Select(e => (string?)e.Attribute("id")).ToHashSet();
        var manquantes = Regex.Matches(admx, @"\$\((?:string|presentation)\.([A-Za-z0-9_]+)\)")
            .Select(m => m.Groups[1].Value).Distinct().Where(id => !ids.Contains(id)).ToList();
        Assert.Empty(manquantes);
    }

    private static string RacineDuDépôt()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "entreprise", "AZERTYGlobal.admx")))
                return dir.FullName;
        }
        throw new DirectoryNotFoundException("entreprise/AZERTYGlobal.admx introuvable depuis les tests.");
    }
}
