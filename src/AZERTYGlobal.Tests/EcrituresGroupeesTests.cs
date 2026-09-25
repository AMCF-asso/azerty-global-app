using System.Reflection;
using System.Text.Json;
using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Audit du 25/09 — A-05, F-18, M-07, et le signalement « UsageStats.Flush sous verrou sur
/// le fil du hook ».
///
/// Chaque setter de ConfigManager réécrivait tout config.json, FlushFileBuffers compris
/// (3,3 ms mesurées), sur le fil qui sert aussi le rappel du hook clavier : 4 écritures pour
/// une couche cochée au menu, 7 pour un enregistrement des Couches maintenables, 31 pour un
/// redimensionnement du clavier virtuel. Les setters ne font plus que marquer le cache ; une
/// écriture unique suit, sur le pool de threads, ou à la fermeture de l'application.
///
/// Chaque témoin porte sa mutation dans le compte rendu du lot 4.
/// </summary>
public class EcrituresGroupeesTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "AZGBATCH_" + Guid.NewGuid().ToString("N"));
    private string ConfigPath => Path.Combine(_dir, "config.json");
    private string StatsPath => Path.Combine(_dir, "usage-stats.json");

    private const string ConfigExistante =
        "{\"activationConsent\":true,\"appLanguage\":\"fr\",\"notificationsEnabled\":true," +
        "\"showOnboardingAtStartup\":false,\"autoStartEnabled\":false}";

    public EcrituresGroupeesTests()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(ConfigPath, ConfigExistante);
        ConfigManager.OverrideConfigPathForTests(ConfigPath);
        _ = ConfigManager.ActivationConsent; // chargement d'une installation existante, sans écriture
    }

    public void Dispose()
    {
        ConfigManager.DiskWritePhaseForTests = null;
        UsageStats.WritePhaseForTests = null;
        ConfigManager.SaveDelayMilliseconds = -1;
        UsageStats.OverrideStatsPathForTests(Path.Combine(_dir, "inutilise.json"));
        // Le journal et les écritures du pool de threads peuvent finir pendant le nettoyage.
        for (int essai = 0; essai < 20; essai++)
        {
            try { Directory.Delete(_dir, true); return; }
            catch (IOException) { Thread.Sleep(25); }
            catch (UnauthorizedAccessException) { Thread.Sleep(25); }
        }
    }

    private static int Écritures() => Volatile.Read(ref ConfigManager.DiskWriteCount);

    private JsonElement ConfigSurLeDisque()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(ConfigPath));
        return doc.RootElement.Clone();
    }

    private static bool Attendre(Func<bool> condition, int millisecondes = 5_000)
    {
        var limite = DateTime.UtcNow.AddMilliseconds(millisecondes);
        while (DateTime.UtcNow < limite)
        {
            if (condition()) return true;
            Thread.Sleep(10);
        }
        return condition();
    }

    // ═══════════════════════════════════════════════════════════════
    // Une écriture par geste
    // ═══════════════════════════════════════════════════════════════

    /// <summary>ToggleMaintainableLayer : quatre setters. Avant : quatre écritures.</summary>
    [Fact]
    public void UneCoucheCochéeAuMenu_UneSeuleÉcriture()
    {
        int avant = Écritures();

        ConfigManager.SetMaintainableGreekEnabled(true);
        ConfigManager.SetMaintainableCyrillicEnabled(false);
        ConfigManager.SetMaintainableScientificEnabled(false);
        ConfigManager.SetMaintainableLayersEnabled(true);

        Assert.Equal(avant, Écritures()); // les setters n'écrivent plus rien
        ConfigManager.Flush();
        Assert.Equal(avant + 1, Écritures());
        Assert.True(ConfigSurLeDisque().GetProperty("maintainableLayersEnabled").GetBoolean());
    }

    /// <summary>MaintainableLayersWindow.SaveToConfig : sept setters. Avant : sept écritures.</summary>
    [Fact]
    public void EnregistrerLesCouchesMaintenables_UneSeuleÉcriture()
    {
        int avant = Écritures();

        ConfigManager.SetMaintainableGreekEnabled(true);
        ConfigManager.SetMaintainableCyrillicEnabled(true);
        ConfigManager.SetMaintainableScientificEnabled(true);
        ConfigManager.SetMaintainableVisualFeedbackEnabled(true);
        ConfigManager.SetMaintainableDoubleTapMilliseconds(400);
        ConfigManager.SetMaintainableLayersEnabled(true);
        ConfigManager.SetMaintainableTutorialCompleted(true);
        ConfigManager.Flush();

        Assert.Equal(avant + 1, Écritures());
    }

    /// <summary>
    /// F-18 : refermer les Paramètres sans rien toucher réécrivait trois fois le fichier.
    /// Une valeur identique ne marque plus le cache.
    /// </summary>
    [Fact]
    public void FermerLesParamètresSansRienChanger_AucuneÉcriture()
    {
        int avant = Écritures();

        ConfigManager.SetAutoStart(false);
        ConfigManager.SetNotifications(true);
        ConfigManager.SetShowOnboardingAtStartup(false);
        ConfigManager.Flush();

        Assert.Equal(avant, Écritures());
    }

    /// <summary>Contrôle du témoin ci-dessus : un vrai changement s'écrit, une fois.</summary>
    [Fact]
    public void FermerLesParamètresAprèsUnChangement_UneÉcriture()
    {
        int avant = Écritures();

        ConfigManager.SetAutoStart(false);
        ConfigManager.SetNotifications(false);
        ConfigManager.SetShowOnboardingAtStartup(false);
        ConfigManager.Flush();

        Assert.Equal(avant + 1, Écritures());
        Assert.False(ConfigSurLeDisque().GetProperty("notificationsEnabled").GetBoolean());
    }

    /// <summary>VirtualKeyboard sauvegarde ses bornes à chaque WM_SIZE. Avant : 31 écritures.</summary>
    [Fact]
    public void RedimensionnerLeClavierVirtuel_UneSeuleÉcriture()
    {
        int avant = Écritures();

        for (int i = 0; i < 30; i++)
            ConfigManager.SetWindowBounds(ConfigManager.VirtualKeyboardBoundsKey,
                new Win32.RECT { left = 100, top = 100, right = 900 + i, bottom = 400 + i });
        ConfigManager.SetWindowBounds(ConfigManager.VirtualKeyboardBoundsKey,
            new Win32.RECT { left = 100, top = 100, right = 929, bottom = 429 });
        ConfigManager.Flush();

        Assert.Equal(avant + 1, Écritures());
        Assert.Equal("100,100,829,329", ConfigSurLeDisque().GetProperty(ConfigManager.VirtualKeyboardBoundsKey).GetString());
    }

    /// <summary>
    /// La sollicitation d'avis reste durable avant la bulle (risque noté par l'audit sur
    /// A-05) : une écriture pour ses trois clés, faite tout de suite, sans Flush de l'appelant.
    /// </summary>
    [Fact]
    public void SollicitationDAvis_ÉcriteAvantLaBulle_EnUneFois()
    {
        int avant = Écritures();

        ConfigManager.RecordReviewPromptShown(new DateOnly(2026, 9, 25));

        Assert.Equal(avant + 1, Écritures());
        var disque = ConfigSurLeDisque();
        Assert.Equal(1, disque.GetProperty("reviewPromptCount").GetInt32());
        Assert.Equal("2026-09-25", disque.GetProperty("reviewPromptLastShown").GetString());
    }

    // ═══════════════════════════════════════════════════════════════
    // L'écriture différée part du pool de threads
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// En production, l'écriture part seule, après un court délai, sur un autre fil que
    /// celui du geste (qui est le fil du hook). Les quatre setters n'en font qu'une.
    /// </summary>
    [Fact]
    public void ÉcritureDifférée_UneSeule_HorsDuFilDuGeste()
    {
        int filDuGeste = Environment.CurrentManagedThreadId;
        int filÉcrivain = 0;
        ConfigManager.DiskWritePhaseForTests = () => Volatile.Write(ref filÉcrivain, Environment.CurrentManagedThreadId);
        ConfigManager.SaveDelayMilliseconds = 50;
        int avant = Écritures();

        ConfigManager.SetMaintainableGreekEnabled(true);
        ConfigManager.SetMaintainableCyrillicEnabled(false);
        ConfigManager.SetMaintainableScientificEnabled(false);
        ConfigManager.SetMaintainableLayersEnabled(true);

        Assert.True(Attendre(() => Écritures() > avant), "l'écriture différée n'est jamais partie");
        Thread.Sleep(250); // aucune seconde écriture ne doit suivre
        Assert.Equal(avant + 1, Écritures());
        Assert.NotEqual(0, Volatile.Read(ref filÉcrivain));
        Assert.NotEqual(filDuGeste, Volatile.Read(ref filÉcrivain));
        Assert.True(ConfigSurLeDisque().GetProperty("maintainableLayersEnabled").GetBoolean());
    }

    // ═══════════════════════════════════════════════════════════════
    // Instantané sous verrou, disque hors verrou : absence de course
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Pendant qu'une écriture est suspendue au milieu du disque, un réglage se pose et se
    /// relit sans attendre : le verrou du cache n'est pas tenu pendant l'I/O. Et ce réglage
    /// posé pendant l'écriture n'est pas perdu : le cache reste marqué, l'écriture suivante
    /// le porte.
    /// </summary>
    [Fact]
    public void ÉcritureEnCours_NeBloquePasLesRéglages_EtNePerdRien()
    {
        ConfigManager.SetNotifications(false);
        using var dansLÉcriture = new ManualResetEventSlim();
        using var reprendre = new ManualResetEventSlim();
        ConfigManager.DiskWritePhaseForTests = () => { dansLÉcriture.Set(); reprendre.Wait(10_000); };

        var écrivain = new Thread(ConfigManager.Flush) { IsBackground = true };
        écrivain.Start();
        Assert.True(dansLÉcriture.Wait(5_000), "l'écriture n'a pas atteint le disque");
        ConfigManager.DiskWritePhaseForTests = null;

        using var réglageFait = new ManualResetEventSlim();
        var réglage = new Thread(() =>
        {
            ConfigManager.SetNotifications(true);
            _ = ConfigManager.NotificationsUserSetting;
            réglageFait.Set();
        }) { IsBackground = true };
        réglage.Start();
        bool pasBloqué = réglageFait.Wait(2_000);

        reprendre.Set();
        Assert.True(écrivain.Join(10_000));
        Assert.True(réglage.Join(10_000));
        Assert.True(pasBloqué, "le réglage a attendu la fin de l'écriture disque");

        // L'écriture suspendue portait l'instantané d'avant ; le réglage suivant reste dû.
        Assert.False(ConfigSurLeDisque().GetProperty("notificationsEnabled").GetBoolean());
        ConfigManager.Flush();
        Assert.True(ConfigSurLeDisque().GetProperty("notificationsEnabled").GetBoolean());
    }

    /// <summary>
    /// Flush prend le verrou des écritures avant celui du cache. Appelé sous le verrou du
    /// cache, il inverserait l'ordre et pourrait interbloquer avec l'écriture différée : la
    /// garde le refuse plutôt que de laisser l'interblocage au hasard.
    /// </summary>
    [Fact]
    public void FlushSousLeVerrouDuCache_EstRefusé()
    {
        var verrou = typeof(ConfigManager).GetField("_lock", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        ConfigManager.SetNotifications(false);

        Monitor.Enter(verrou);
        try
        {
            Assert.Throws<InvalidOperationException>(ConfigManager.Flush);
        }
        finally
        {
            Monitor.Exit(verrou);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // UsageStats : même règle, le verrou du hook n'attend plus le disque
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// RecordEmittedText tourne dans le rappel du hook et prend le verrou des compteurs.
    /// Flush écrivait le fichier sous ce même verrou : une frappe pendant l'écriture
    /// attendait le disque. Pendant une écriture suspendue, une frappe passe maintenant, et
    /// elle n'est pas perdue.
    /// </summary>
    [Fact]
    public void Statistiques_ÉcritureEnCours_NeBloquePasLaFrappe_EtNePerdRien()
    {
        UsageStats.OverrideStatsPathForTests(StatsPath);
        UsageStats.RecordEmittedText("É");
        using var dansLÉcriture = new ManualResetEventSlim();
        using var reprendre = new ManualResetEventSlim();
        UsageStats.WritePhaseForTests = () => { dansLÉcriture.Set(); reprendre.Wait(10_000); };

        var écrivain = new Thread(UsageStats.Flush) { IsBackground = true };
        écrivain.Start();
        Assert.True(dansLÉcriture.Wait(5_000), "l'écriture n'a pas atteint le disque");
        UsageStats.WritePhaseForTests = null;

        using var frappeFaite = new ManualResetEventSlim();
        var frappe = new Thread(() => { UsageStats.RecordEmittedText("É"); frappeFaite.Set(); }) { IsBackground = true };
        frappe.Start();
        bool pasBloquée = frappeFaite.Wait(2_000);

        reprendre.Set();
        Assert.True(écrivain.Join(10_000));
        Assert.True(frappe.Join(10_000));
        Assert.True(pasBloquée, "la frappe a attendu la fin de l'écriture disque");

        UsageStats.Flush();
        UsageStats.OverrideStatsPathForTests(StatsPath); // redémarrage simulé
        Assert.Equal(2, UsageStats.AccentedUppercaseCount);
    }

    /// <summary>Le minuteur de 5 min passe par le pool de threads : le fil du hook n'écrit plus.</summary>
    [Fact]
    public void Statistiques_FlushInBackground_ÉcritHorsDuFilAppelant()
    {
        UsageStats.OverrideStatsPathForTests(StatsPath);
        UsageStats.RecordEmittedText("É");
        int filAppelant = Environment.CurrentManagedThreadId;
        int filÉcrivain = 0;
        UsageStats.WritePhaseForTests = () => Volatile.Write(ref filÉcrivain, Environment.CurrentManagedThreadId);

        UsageStats.FlushInBackground();

        Assert.True(Attendre(() => File.Exists(StatsPath)), "l'écriture en arrière-plan n'est jamais partie");
        Assert.NotEqual(0, Volatile.Read(ref filÉcrivain));
        Assert.NotEqual(filAppelant, Volatile.Read(ref filÉcrivain));
    }

    // ═══════════════════════════════════════════════════════════════
    // M-07 : le journal critique de l'hôte ne bloque plus le rappel du hook
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Un refus de SendInput journalisait dans le rappel du hook par une écriture disque
    /// synchrone sous <c>_logFlushLock</c>, verrou partagé avec le pool de threads. Verrou
    /// tenu par le test, l'appel de l'hôte rend la main quand même ; la ligne arrive dans
    /// error.log dès que le verrou se libère.
    /// </summary>
    [Fact]
    public void JournalCritiqueDeLHôte_NAttendNiLeVerrouNiLeDisque()
    {
        var verrou = typeof(ConfigManager).GetField("_logFlushLock", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        var hôte = new AzertyGlobalWindowsTypingHost();
        using var fait = new ManualResetEventSlim();
        Thread appel;
        bool pasBloqué;

        Monitor.Enter(verrou);
        try
        {
            appel = new Thread(() =>
            {
                hôte.LogCompatibilityCriticalEvent("EmissionRefusee", "temoin-M07");
                fait.Set();
            }) { IsBackground = true };
            appel.Start();
            pasBloqué = fait.Wait(2_000);
        }
        finally
        {
            Monitor.Exit(verrou);
        }

        Assert.True(appel.Join(10_000));
        Assert.True(pasBloqué, "l'appel de l'hôte a attendu le verrou du journal");
        var journal = Path.Combine(_dir, "error.log");
        Assert.True(Attendre(() =>
        {
            try { return File.Exists(journal) && File.ReadAllText(journal).Contains("EmissionRefusee: temoin-M07"); }
            catch (IOException) { return false; }
        }), "la ligne différée n'est jamais arrivée dans error.log");
    }
}
