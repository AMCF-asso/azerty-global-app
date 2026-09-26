using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Témoins de caractérisation de la sollicitation d'avis par notification (audit du 25/09,
/// A-02). Ils figent les décisions de la 1.3.0 telles que <c>TrayApplication</c> les rend,
/// avant et après le regroupement de l'état et des gardes : mêmes entrées, même réponse,
/// mêmes clés écrites.
///
/// Chaque cas part d'un <c>config.json</c> et d'un <c>usage-stats.json</c> isolés, puis
/// appelle les membres privés de <c>TrayApplication</c> par réflexion, sur une instance
/// non construite : aucune fenêtre, aucun hook, aucune icône de zone de notification.
/// Hors package, la sollicitation passe par la bulle, et <c>Shell_NotifyIconW</c> refuse une
/// structure vide sans rien afficher.
///
/// ⚠️ Limite assumée : le câblage (minuteries, fermeture de l'accueil, toast du canal
/// packagé) reste du Win32, prouvé par la recette.
/// </summary>
public class SollicitationAvisCaracterisationTests : IDisposable
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags Statique = BindingFlags.Static | BindingFlags.NonPublic;

    private static DateOnly Aujourdhui => DateOnly.FromDateTime(DateTime.Now);

    private readonly string _dossier;
    private readonly string _config;
    private readonly string _stats;
    private readonly IDisposable _politique;
    private readonly IDisposable _canal;

    public SollicitationAvisCaracterisationTests()
    {
        _dossier = Path.Combine(Path.GetTempPath(), "AZGAvis_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dossier);
        _config = Path.Combine(_dossier, "config.json");
        _stats = Path.Combine(_dossier, "usage-stats.json");
        _politique = PolicyManager.OverrideForTests((_, _) => null);
        _canal = AppChannel.OverrideForTests(DistributionChannel.Unpackaged);
        LearningSessionTracker.ResetForTests();
        DernierEchec(null);
    }

    public void Dispose()
    {
        DernierEchec(null);
        LearningSessionTracker.ResetForTests();
        UsageStats.ClearEnrichedThresholdSignal();
        _canal.Dispose();
        _politique.Dispose();
        ConfigManager.OverrideConfigPathForTests(Path.Combine(_dossier, "fin.json"));
        UsageStats.OverrideStatsPathForTests(Path.Combine(_dossier, "fin-stats.json"));
        try { Directory.Delete(_dossier, true); } catch { }
    }

    // ── Mise en place ──────────────────────────────────────────────

    /// <summary>Réglages persistés de la sollicitation, relus comme au démarrage.</summary>
    private void Config(int? essais = null, DateOnly? derniere = null, bool cliquee = false,
        bool notifications = true, DateOnly? rappelDefi = null)
    {
        var cles = new Dictionary<string, object> { ["notificationsEnabled"] = notifications };
        if (essais.HasValue) cles["reviewPromptCount"] = essais.Value;
        if (derniere.HasValue) cles["reviewPromptLastShown"] = Jour(derniere.Value);
        if (cliquee) cles["reviewPromptClicked"] = true;
        if (rappelDefi.HasValue) cles["trainingLastReminderDate"] = Jour(rappelDefi.Value);
        File.WriteAllText(_config, JsonSerializer.Serialize(cles));
        ConfigManager.OverrideConfigPathForTests(_config);
    }

    /// <summary>Statistiques d'usage. Par défaut : les deux seuils de l'essai 1 tout juste
    /// atteints (20 caractères enrichis, 10 minutes), et assez de jours pour l'essai 2.</summary>
    private void Stats(bool aServi = true, int joursActifs = 10, long minutes = 10,
        long enrichis = 20, int? derniereFrappeIlYa = 0)
    {
        var cles = new Dictionary<string, object>
        {
            ["activeDaysCount"] = joursActifs,
            ["totalActiveMinutes"] = minutes,
            ["accentedUppercaseCount"] = enrichis,
        };
        if (aServi) cles["firstRemapDate"] = Jour(Aujourdhui.AddDays(-30));
        if (derniereFrappeIlYa.HasValue) cles["lastActiveDate"] = Jour(Aujourdhui.AddDays(-derniereFrappeIlYa.Value));
        File.WriteAllText(_stats, JsonSerializer.Serialize(cles));
        UsageStats.OverrideStatsPathForTests(_stats);
    }

    private static string Jour(DateOnly d) => d.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    private static void DernierEchec(DateTime? utc) =>
        typeof(ConfigManager).GetField("_lastErrorUtc", Statique)!.SetValue(null, utc);

    /// <summary>Instance sans constructeur : aucune fenêtre ni hook. <paramref name="migree"/>
    /// et <paramref name="premierLancement"/> tiennent lieu de <c>_versionFirstRun</c> ;
    /// <paramref name="sansDate"/> simule son écriture ratée (champ null).</summary>
    private static TrayApplication Tray(bool migree = false, DateOnly? premierLancement = null,
        bool sansDate = false, bool accueilOuvert = false)
    {
        var tray = (TrayApplication)RuntimeHelpers.GetUninitializedObject(typeof(TrayApplication));
        object? version = sansDate ? null
            : new ConfigManager.VersionFirstRun("1.3.0", premierLancement ?? Aujourdhui, migree);
        typeof(TrayApplication).GetField("_versionFirstRun", Instance)!.SetValue(tray, version);
        typeof(TrayApplication).GetField("_reviewPromptDeferred", Instance)!.SetValue(tray, accueilOuvert);
        return tray;
    }

    private static bool Demander(TrayApplication tray, ReviewPromptTrigger declencheur) =>
        (bool)typeof(TrayApplication).GetMethod("MaybeShowReviewPrompt", Instance)!
            .Invoke(tray, new object[] { declencheur })!;

    private static bool AuFilDeLaFrappe(TrayApplication tray) => Demander(tray, ReviewPromptTrigger.QuietTyping);
    private static bool AuDemarrage(TrayApplication tray) => Demander(tray, ReviewPromptTrigger.Startup);

    private static void TickDeQuietude(TrayApplication tray) =>
        typeof(TrayApplication).GetMethod("MaybeShowReviewAfterQuietTyping", Instance)!.Invoke(tray, null);

    private static bool EncorePossible() =>
        (bool)typeof(TrayApplication).GetMethod("ReviewPromptStillPossible", Statique)!.Invoke(null, null)!;

    private static void ArmerAuChargement() =>
        typeof(TrayApplication).GetMethod("ArmReviewSignalIfAlreadyAboveThreshold", Statique)!.Invoke(null, null);

    /// <summary>Dernière frappe remappée il y a <paramref name="ms"/> millisecondes.</summary>
    private static void DerniereFrappeIlYa(long ms) =>
        typeof(UsageStats).GetField("_lastRemapTickCount", Statique)!
            .SetValue(null, Environment.TickCount64 - ms);

    private static string BullePendante(TrayApplication tray) =>
        typeof(TrayApplication).GetField("_pendingBalloon", Instance)!.GetValue(tray)!.ToString()!;

    private static bool CibleStore(TrayApplication tray) =>
        (bool)typeof(TrayApplication).GetField("_reviewTargetIsStore", Instance)!.GetValue(tray)!;

    private static void AucuneEcriture()
    {
        Assert.Equal(0, ConfigManager.ReviewPromptCount);
        Assert.Null(ConfigManager.ReviewPromptLastShown);
    }

    // ── Essai 1 : au fil de la frappe seulement ─────────────────────

    [Fact]
    public void Essai1_seuils_tout_juste_atteints_part_et_consomme_un_essai()
    {
        Config();
        Stats();
        var tray = Tray();

        Assert.True(AuFilDeLaFrappe(tray));
        Assert.Equal(1, ConfigManager.ReviewPromptCount);
        Assert.Equal(Aujourdhui, ConfigManager.ReviewPromptLastShown);
        // Hors package : bulle, cible page feedback.
        Assert.Equal("Review", BullePendante(tray));
        Assert.False(CibleStore(tray));

        // Écrit sur le disque sous les clés historiques, retour arrière v1.1 compris.
        using var disque = JsonDocument.Parse(File.ReadAllText(_config));
        Assert.Equal(1, disque.RootElement.GetProperty("reviewPromptCount").GetInt32());
        Assert.Equal(Jour(Aujourdhui), disque.RootElement.GetProperty("reviewPromptLastShown").GetString());
        Assert.True(disque.RootElement.GetProperty("reviewPromptDone").GetBoolean());
    }

    [Fact]
    public void Essai1_jamais_depuis_le_demarrage_ni_le_relais_de_l_accueil()
    {
        Config();
        Stats();
        Assert.False(AuDemarrage(Tray()));
        AucuneEcriture();
    }

    [Fact]
    public void Essai1_un_caractere_enrichi_de_moins_refuse()
    {
        Config();
        Stats(enrichis: UsageStats.EnrichedCharsReviewThreshold - 1);
        Assert.False(AuFilDeLaFrappe(Tray()));
        AucuneEcriture();
    }

    [Fact]
    public void Essai1_une_minute_active_de_moins_refuse()
    {
        Config();
        Stats(minutes: 9);
        Assert.False(AuFilDeLaFrappe(Tray()));
        AucuneEcriture();
    }

    [Fact]
    public void Essai1_n_attend_aucun_jour_d_usage()
    {
        Config();
        Stats(joursActifs: 1);
        Assert.True(AuFilDeLaFrappe(Tray()));
    }

    [Fact]
    public void Essai1_rappel_du_defi_deja_parti_aujourd_hui_refuse()
    {
        Config(rappelDefi: Aujourdhui);
        Stats();
        Assert.False(AuFilDeLaFrappe(Tray()));
        AucuneEcriture();
    }

    [Fact]
    public void Essai1_rappel_du_defi_de_la_veille_ne_bloque_pas()
    {
        Config(rappelDefi: Aujourdhui.AddDays(-1));
        Stats();
        Assert.True(AuFilDeLaFrappe(Tray()));
    }

    [Fact]
    public void Essai1_installation_migree_attend_le_lendemain_du_premier_lancement()
    {
        Config();
        Stats();
        Assert.False(AuFilDeLaFrappe(Tray(migree: true, premierLancement: Aujourdhui)));
        AucuneEcriture();
        Assert.True(AuFilDeLaFrappe(Tray(migree: true, premierLancement: Aujourdhui.AddDays(-1))));
    }

    [Fact]
    public void Essai1_installation_neuve_part_le_jour_meme()
    {
        Config();
        Stats();
        Assert.True(AuFilDeLaFrappe(Tray(migree: false, premierLancement: Aujourdhui)));
    }

    [Fact]
    public void Essai1_sans_premier_lancement_etabli_refuse()
    {
        // _versionFirstRun null : UpgradedWithUsage vaut true, faute de date, refus.
        Config();
        Stats();
        Assert.False(AuFilDeLaFrappe(Tray(sansDate: true)));
        AucuneEcriture();
    }

    // ── Essai 2 : démarrage et frappe ────────────────────────────────

    [Fact]
    public void Essai2_dix_jours_actifs_sept_jours_apres_le_premier_part_du_demarrage()
    {
        Config(essais: 1, derniere: Aujourdhui.AddDays(-7));
        Stats(joursActifs: 10, minutes: 0, enrichis: 0);
        var tray = Tray();

        Assert.True(AuDemarrage(tray));
        Assert.Equal(2, ConfigManager.ReviewPromptCount);
        Assert.Equal(Aujourdhui, ConfigManager.ReviewPromptLastShown);
        Assert.Equal("Review", BullePendante(tray));
    }

    [Fact]
    public void Essai2_part_aussi_au_fil_de_la_frappe()
    {
        Config(essais: 1, derniere: Aujourdhui.AddDays(-7));
        Stats(joursActifs: 10);
        Assert.True(AuFilDeLaFrappe(Tray(migree: true, premierLancement: Aujourdhui)));
    }

    [Fact]
    public void Essai2_neuf_jours_actifs_refuse()
    {
        Config(essais: 1, derniere: Aujourdhui.AddDays(-7));
        Stats(joursActifs: 9);
        Assert.False(AuDemarrage(Tray()));
        Assert.Equal(1, ConfigManager.ReviewPromptCount);
    }

    [Fact]
    public void Essai2_six_jours_apres_le_premier_refuse()
    {
        Config(essais: 1, derniere: Aujourdhui.AddDays(-6));
        Stats();
        Assert.False(AuDemarrage(Tray()));
        Assert.Equal(1, ConfigManager.ReviewPromptCount);
    }

    [Fact]
    public void Essai2_sans_date_du_premier_le_plancher_est_acquis()
    {
        // Installation migrée de la v1.1, qui ne connaissait pas cette date.
        Config(essais: 1);
        Stats();
        Assert.True(AuDemarrage(Tray()));
    }

    [Fact]
    public void Essai2_utilisateur_absent_depuis_plus_de_trois_jours_refuse()
    {
        Config(essais: 1, derniere: Aujourdhui.AddDays(-30));
        Stats(derniereFrappeIlYa: 3);
        Assert.True(AuDemarrage(Tray()));

        Config(essais: 1, derniere: Aujourdhui.AddDays(-30));
        Stats(derniereFrappeIlYa: 4);
        Assert.False(AuDemarrage(Tray()));

        Config(essais: 1, derniere: Aujourdhui.AddDays(-30));
        Stats(derniereFrappeIlYa: null);
        Assert.False(AuDemarrage(Tray()));
    }

    [Fact]
    public void Essai2_ignore_le_rappel_du_defi_et_les_seuils_de_l_essai_1()
    {
        Config(essais: 1, derniere: Aujourdhui.AddDays(-7), rappelDefi: Aujourdhui);
        Stats(minutes: 0, enrichis: 0);
        Assert.True(AuDemarrage(Tray()));
    }

    // ── Gardes communes ──────────────────────────────────────────────

    [Fact]
    public void Deux_essais_faits_plus_rien()
    {
        Config(essais: 2, derniere: Aujourdhui.AddDays(-30));
        Stats();
        Assert.False(AuDemarrage(Tray()));
        Assert.False(AuFilDeLaFrappe(Tray()));
        Assert.Equal(2, ConfigManager.ReviewPromptCount);
    }

    [Fact]
    public void Compteur_au_dela_du_plafond_plus_rien()
    {
        Config(essais: 3, derniere: Aujourdhui.AddDays(-30));
        Stats();
        Assert.False(AuDemarrage(Tray()));
    }

    [Fact]
    public void Sollicitation_cliquee_plus_rien()
    {
        Config(cliquee: true);
        Stats();
        Assert.False(AuFilDeLaFrappe(Tray()));
        AucuneEcriture();

        Config(essais: 1, derniere: Aujourdhui.AddDays(-7), cliquee: true);
        Stats();
        Assert.False(AuDemarrage(Tray()));
    }

    [Fact]
    public void Notifications_coupees_refuse()
    {
        Config(notifications: false);
        Stats();
        Assert.False(AuFilDeLaFrappe(Tray()));
        AucuneEcriture();
    }

    [Fact]
    public void Liens_externes_eteints_refuse()
    {
        Config();
        Stats();
        using (AppChannel.OverrideForTests(DistributionChannel.Amcf))
            Assert.False(AuFilDeLaFrappe(Tray()));
        AucuneEcriture();
    }

    [Fact]
    public void Erreur_journalisee_depuis_moins_de_48_heures_refuse()
    {
        Config();
        Stats();
        DernierEchec(DateTime.UtcNow.AddHours(-47));
        Assert.False(AuFilDeLaFrappe(Tray()));
        AucuneEcriture();

        DernierEchec(DateTime.UtcNow.AddHours(-49));
        Assert.True(AuFilDeLaFrappe(Tray()));
    }

    [Fact]
    public void Application_jamais_servie_refuse()
    {
        Config(essais: 1);
        Stats(aServi: false);
        Assert.False(AuDemarrage(Tray()));

        Config();
        Stats(aServi: false);
        Assert.False(AuFilDeLaFrappe(Tray()));
    }

    // ── Séances de Leçons (règle de la 1.3.0) ────────────────────────

    [Fact]
    public void Pendant_une_seance_de_lecons_refuse()
    {
        Config(essais: 1);
        Stats();
        var lecons = new object();
        LearningSessionTracker.Opened(lecons);
        Assert.False(AuDemarrage(Tray()));

        Config();
        Stats();
        Assert.False(AuFilDeLaFrappe(Tray()));
        AucuneEcriture();
    }

    [Fact]
    public void Dans_les_dix_minutes_apres_une_seance_refuse()
    {
        Config(essais: 1);
        Stats();
        var lecons = new object();
        LearningSessionTracker.Opened(lecons);
        LearningSessionTracker.Closed(lecons);
        Assert.False(AuDemarrage(Tray()));
        Assert.Equal(1, ConfigManager.ReviewPromptCount);
    }

    // ── Reste-t-il une sollicitation possible ? ──────────────────────

    [Fact]
    public void Encore_possible_ne_lit_que_les_reglages_persistes()
    {
        Config();
        Stats(aServi: false, minutes: 0, enrichis: 0);
        Assert.True(EncorePossible());

        Config(essais: 1);
        Assert.True(EncorePossible());

        Config(essais: 2);
        Assert.False(EncorePossible());

        Config(cliquee: true);
        Assert.False(EncorePossible());

        Config(notifications: false);
        Assert.False(EncorePossible());

        Config();
        using (AppChannel.OverrideForTests(DistributionChannel.Amcf))
            Assert.False(EncorePossible());
    }

    // ── Armement au chargement ───────────────────────────────────────

    [Fact]
    public void Armement_au_chargement_seulement_pour_l_essai_1_au_dessus_du_seuil()
    {
        Config();
        Stats();
        ArmerAuChargement();
        Assert.True(UsageStats.EnrichedThresholdCrossed);

        Config(essais: 1);
        Stats();
        ArmerAuChargement();
        Assert.False(UsageStats.EnrichedThresholdCrossed);

        Config(cliquee: true);
        Stats();
        ArmerAuChargement();
        Assert.False(UsageStats.EnrichedThresholdCrossed);

        Config();
        Stats(enrichis: UsageStats.EnrichedCharsReviewThreshold - 1);
        ArmerAuChargement();
        Assert.False(UsageStats.EnrichedThresholdCrossed);
    }

    // ── Tick de quiétude et consommation du signal ───────────────────

    [Fact]
    public void Tick_signal_arme_frappe_puis_silence_sollicite_et_desarme()
    {
        Config();
        Stats();
        UsageStats.ArmEnrichedThresholdSignal();
        DerniereFrappeIlYa(20_000);
        var tray = Tray();

        TickDeQuietude(tray);
        Assert.Equal(1, ConfigManager.ReviewPromptCount);
        Assert.False(UsageStats.EnrichedThresholdCrossed);
    }

    [Fact]
    public void Tick_encore_en_train_de_taper_attend()
    {
        Config();
        Stats();
        UsageStats.ArmEnrichedThresholdSignal();
        DerniereFrappeIlYa(5_000);

        TickDeQuietude(Tray());
        AucuneEcriture();
        Assert.True(UsageStats.EnrichedThresholdCrossed);
    }

    [Fact]
    public void Tick_sans_frappe_dans_la_session_attend()
    {
        Config();
        Stats();
        UsageStats.ArmEnrichedThresholdSignal();

        TickDeQuietude(Tray());
        AucuneEcriture();
        Assert.True(UsageStats.EnrichedThresholdCrossed);
    }

    [Fact]
    public void Tick_signal_non_arme_ne_fait_rien()
    {
        Config();
        Stats();
        DerniereFrappeIlYa(20_000);

        TickDeQuietude(Tray());
        AucuneEcriture();
    }

    [Fact]
    public void Tick_accueil_ouvert_differe()
    {
        Config();
        Stats();
        UsageStats.ArmEnrichedThresholdSignal();
        DerniereFrappeIlYa(20_000);

        TickDeQuietude(Tray(accueilOuvert: true));
        AucuneEcriture();
        Assert.True(UsageStats.EnrichedThresholdCrossed);
    }

    [Fact]
    public void Tick_refus_provisoire_garde_le_signal_arme()
    {
        Config();
        Stats(minutes: 9);
        UsageStats.ArmEnrichedThresholdSignal();
        DerniereFrappeIlYa(20_000);

        TickDeQuietude(Tray());
        AucuneEcriture();
        Assert.True(UsageStats.EnrichedThresholdCrossed);
    }

    [Fact]
    public void Tick_erreur_recente_garde_le_signal_arme()
    {
        // Refus provisoire : la sollicitation reste possible après la tentative, le
        // silence suivant retentera.
        Config();
        Stats();
        UsageStats.ArmEnrichedThresholdSignal();
        DerniereFrappeIlYa(20_000);
        DernierEchec(DateTime.UtcNow);

        TickDeQuietude(Tray());
        Assert.True(UsageStats.EnrichedThresholdCrossed);
    }

    [Fact]
    public void Tick_plus_rien_a_solliciter_sort_sans_toucher_au_signal()
    {
        // Le tick tue sa minuterie et rend la main avant toute tentative : le signal
        // reste tel quel.
        Config(cliquee: true);
        Stats();
        UsageStats.ArmEnrichedThresholdSignal();
        DerniereFrappeIlYa(20_000);

        TickDeQuietude(Tray());
        Assert.True(UsageStats.EnrichedThresholdCrossed);
        Assert.Equal(0, ConfigManager.ReviewPromptCount);
    }

    [Fact]
    public void Tick_seance_refermee_sans_frappe_depuis_attend()
    {
        // Dernière frappe pendant la séance, séance refermée depuis plus de dix minutes :
        // la garde des séances refuse, le signal reste armé.
        Config();
        Stats();
        UsageStats.ArmEnrichedThresholdSignal();
        var lecons = new object();
        LearningSessionTracker.Opened(lecons);
        LearningSessionTracker.Closed(lecons);
        typeof(LearningSessionTracker).GetField("_lastCloseTick", Statique)!
            .SetValue(null, Environment.TickCount64 - 12 * 60_000L);
        DerniereFrappeIlYa(12 * 60_000L + 1_000);

        TickDeQuietude(Tray());
        AucuneEcriture();
        Assert.True(UsageStats.EnrichedThresholdCrossed);

        // Une frappe après la fin de la séance, puis le silence : la demande part.
        DerniereFrappeIlYa(20_000);
        TickDeQuietude(Tray());
        Assert.Equal(1, ConfigManager.ReviewPromptCount);
    }
}
