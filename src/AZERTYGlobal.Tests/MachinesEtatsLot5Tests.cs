using System.Globalization;
using System.Text;
using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Tests de régression du lot 5 de l'analyse des machines à états (statistiques d'usage,
/// rappels d'entraînement, fenêtre Statistiques, progression des Leçons). Chaque test affirme
/// l'invariant correct : il échoue tant que le constat est vrai.
/// </summary>
public sealed class MachinesEtatsLot5Tests
{
    private static readonly DateOnly Jour = new(2026, 11, 15);
    private static string JourTexte(DateOnly jour) => jour.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>
    /// Exécute l'action sous une culture à calendrier thaï bouddhiste (an 2026 = 1483 grégorien
    /// pour « yyyy »), puis restaure la culture du fil. Échoue bruyamment si la culture n'existe
    /// pas sur la machine : mieux qu'un test vert qui n'a rien éprouvé.
    /// </summary>
    private static void SousCultureThai(Action action)
    {
        CultureInfo thai;
        try { thai = new CultureInfo("th-TH"); }
        catch (CultureNotFoundException ex)
        {
            throw new Xunit.Sdk.XunitException("Culture th-TH indisponible sur cette machine : le constat ne peut pas être éprouvé. " + ex.Message);
        }
        Assert.IsType<ThaiBuddhistCalendar>(thai.DateTimeFormat.Calendar);

        var avant = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = thai;
        try { action(); }
        finally { CultureInfo.CurrentCulture = avant; }
    }

    // Constat C5.1
    // Une séance du défi commun est enregistrée en invariant (« 2026-11-15 ») puis relue par
    // ParseDate sans culture : sous un calendrier thaï la date relue n'est plus celle du jour, et
    // WouldCount recompte une seconde séance le même jour.
    // Modèle suivi : DefiRejouableTests.Apres_la_sequence_le_defi_commun_compte_une_fois_par_jour.
    [Fact]
    public void DefiCommun_UneSeanceDejaFaiteAujourdhui_NeCompteJamaisDeuxFois_QuelleQueSoitLaCulture()
    {
        using var isolation = new BancCapture.Isolation();
        ConfigManager.SetTrainingSequenceIndex(DailyChallenge.SequenceLength);
        ConfigManager.SetTrainingLastSessionDate(JourTexte(Jour));

        Assert.False(TrainingReminders.WouldCount(Jour, DailyChallenge.SequenceLength),
            "témoin : sous la culture de la machine, la séance du jour est déjà comptée");

        SousCultureThai(() =>
            Assert.False(TrainingReminders.WouldCount(Jour, DailyChallenge.SequenceLength),
                "sous un calendrier thaï, la date de séance écrite en invariant n'est plus reconnue comme celle du jour"));
    }

    // Constat C5.1 (variante rappel)
    // Un rappel est déjà émis aujourd'hui (date écrite en invariant) : sous un calendrier thaï,
    // Snapshot relit une autre date et ShouldRemind autorise un second rappel le même jour.
    // Modèle suivi : DailyChallengeTests.TrainingReminderReviewPriorityTests.ReviewPriority_SurvivesProcessRestart.
    [Fact]
    public void Rappel_UnSeulParJour_QuelleQueSoitLaCulture()
    {
        using var isolation = new BancCapture.Isolation();
        ConfigManager.SetTrainingEnabled(true);
        TrainingReminders.MarkReminderShown(Jour);
        var soir = Jour.ToDateTime(new TimeOnly(TrainingReminders.EarliestHour + 1, 0));

        Assert.False(TrainingReminders.ShouldRemind(soir, TrainingReminders.Snapshot()),
            "témoin : sous la culture de la machine, le rappel du jour interdit un second rappel");

        SousCultureThai(() =>
            Assert.False(TrainingReminders.ShouldRemind(soir, TrainingReminders.Snapshot()),
                "sous un calendrier thaï, le rappel déjà émis aujourd'hui n'est plus reconnu et un second part"));
    }

    // Constat C5.1 (variante statistiques)
    // usage-stats.json porte « 2026-11-15 » en invariant ; UsageStats.ParseDate le relit sans
    // culture : sous un calendrier thaï, LastActiveDate rend l'an 1483 au lieu de 2026 (la série,
    // comparée à partir de ces dates, en dépend).
    // Modèle suivi : UsageStatsTests.SeedFile (fichier préchargé via OverrideStatsPathForTests).
    [Fact]
    public void Statistiques_UneDateEcriteEnInvariantSeRelitAIdentique_QuelleQueSoitLaCulture()
    {
        using var isolation = new BancCapture.Isolation();
        string dossier = Path.Combine(Path.GetTempPath(), "AZGLot5_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dossier);
        try
        {
            string chemin = Path.Combine(dossier, "usage-stats.json");
            File.WriteAllText(chemin,
                "{ \"firstRemapDate\": \"2026-11-01\", \"lastActiveDate\": \"2026-11-15\", " +
                "\"activeDaysCount\": 3, \"currentStreak\": 2, \"bestStreak\": 2 }",
                new UTF8Encoding(false));
            UsageStats.OverrideStatsPathForTests(chemin);

            Assert.Equal(Jour, UsageStats.LastActiveDate); // témoin : culture de la machine

            SousCultureThai(() =>
            {
                Assert.Equal(Jour, UsageStats.LastActiveDate);
                Assert.Equal(new DateOnly(2026, 11, 1), UsageStats.FirstRemapDate);
            });
        }
        finally { try { Directory.Delete(dossier, true); } catch { } }
    }

    // Constat C5.1 (variante série sur deux jours)
    // La série compare deux dates relues. Sous un calendrier thaï, « 2024-02-29 » est une date
    // refusée (l'an 2024 bouddhiste correspond à l'an 1481, qui n'est pas bissextile) : la série
    // repartait à 1 le 1er mars. Deux jours qui se suivent font une série, quelle que soit la culture.
    [Theory]
    [InlineData("2026-11-14", "2026-11-15", true)]
    [InlineData("2024-02-29", "2024-03-01", true)]
    [InlineData("2026-12-31", "2027-01-01", true)]
    [InlineData("2026-11-13", "2026-11-15", false)]
    [InlineData("2026-11-15", "2026-11-15", false)]
    public void Statistiques_DeuxJoursQuiSeSuivent_FontUneSerie_QuelleQueSoitLaCulture(string veille, string jour, bool attendu)
    {
        Assert.Equal(attendu, UsageStats.IsConsecutiveDay(veille, jour));

        SousCultureThai(() => Assert.Equal(attendu, UsageStats.IsConsecutiveDay(veille, jour)));
    }

    private static void ForcerUsageStats(string dossier, string derniereActivite, int jours, int serie, int meilleure)
    {
        string chemin = Path.Combine(dossier, "usage-stats.json");
        File.WriteAllText(chemin,
            "{ \"firstRemapDate\": \"2026-01-01\", \"lastActiveDate\": \"" + derniereActivite + "\", " +
            "\"activeDaysCount\": " + jours.ToString(CultureInfo.InvariantCulture) +
            ", \"currentStreak\": " + serie.ToString(CultureInfo.InvariantCulture) +
            ", \"bestStreak\": " + meilleure.ToString(CultureInfo.InvariantCulture) + " }",
            new UTF8Encoding(false));
        UsageStats.OverrideStatsPathForTests(chemin);
    }

    // Constat C5.7
    // Horloge ou fuseau qui recule : lastActiveDate (demain) est postérieure à la date courante.
    // RecordActivityLocked ne reconnaît pas « déjà compté » (égalité stricte) et recompte le jour.
    // Un jour déjà compté ne l'est pas de nouveau. (Le test évite la zone de minuit : « demain »
    // est calculé juste avant la frappe ; un passage de minuit entre les deux rend le test vert.)
    // Modèle suivi : UsageStatsTests.RecordEmittedText_ConsecutiveDay_IncrementsStreak (SeedFile).
    [Fact]
    public void Statistiques_DerniereActiviteDansLeFutur_LeJourNEstPasRecompte()
    {
        using var isolation = new BancCapture.Isolation();
        while (UsageStats.IsTypingExcluded) UsageStats.EndExcludedTyping();
        string dossier = Path.Combine(Path.GetTempPath(), "AZGLot5_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dossier);
        try
        {
            string demain = JourTexte(DateOnly.FromDateTime(DateTime.Now).AddDays(1));
            ForcerUsageStats(dossier, demain, jours: 10, serie: 5, meilleure: 5);

            UsageStats.RecordEmittedText("É");

            Assert.Equal(10, UsageStats.ActiveDaysCount);
        }
        finally { try { Directory.Delete(dossier, true); } catch { } }
    }

    // Constat C5.7 (variante série)
    // Même scénario : la série en cours (5) ne repart pas à 1 parce que l'horloge a reculé d'un jour,
    // et la dernière activité enregistrée redevient aujourd'hui.
    // Modèle suivi : UsageStatsTests.CurrentStreak_ActiveYesterday_StillReadsAsCurrent.
    [Fact]
    public void Statistiques_DerniereActiviteDansLeFutur_LaSerieEnCoursNEstPasRemiseAZero()
    {
        using var isolation = new BancCapture.Isolation();
        while (UsageStats.IsTypingExcluded) UsageStats.EndExcludedTyping();
        string dossier = Path.Combine(Path.GetTempPath(), "AZGLot5_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dossier);
        try
        {
            var aujourdhui = DateOnly.FromDateTime(DateTime.Now);
            ForcerUsageStats(dossier, JourTexte(aujourdhui.AddDays(1)), jours: 10, serie: 5, meilleure: 5);

            UsageStats.RecordEmittedText("É");

            Assert.Equal(5, UsageStats.CurrentStreak);
            Assert.Equal(aujourdhui, UsageStats.LastActiveDate);
        }
        finally { try { Directory.Delete(dossier, true); } catch { } }
    }

    // Constat C5.7 (variante lecture)
    // Sans nouvelle frappe, la série affichée ne tombe pas à 0 parce que la dernière activité
    // enregistrée est datée de demain.
    [Fact]
    public void Statistiques_DerniereActiviteDansLeFutur_LaSerieAfficheeNeTombePasAZero()
    {
        using var isolation = new BancCapture.Isolation();
        string dossier = Path.Combine(Path.GetTempPath(), "AZGLot5_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dossier);
        try
        {
            string demain = JourTexte(DateOnly.FromDateTime(DateTime.Now).AddDays(1));
            ForcerUsageStats(dossier, demain, jours: 10, serie: 5, meilleure: 5);

            Assert.Equal(5, UsageStats.CurrentStreak);
        }
        finally { try { Directory.Delete(dossier, true); } catch { } }
    }

    // Constat C5.7 (variante Défi)
    // Une séance du défi commun datée de demain (horloge reculée) : la séance du jour est déjà
    // faite, elle ne doit pas se recompter aujourd'hui.
    // Modèle suivi : DefiRejouableTests.Apres_la_sequence_le_defi_commun_compte_une_fois_par_jour.
    [Fact]
    public void DefiCommun_SeanceDatee_DuLendemain_NeSeRecomptePasAujourdhui()
    {
        using var isolation = new BancCapture.Isolation();
        ConfigManager.SetTrainingSequenceIndex(DailyChallenge.SequenceLength);
        ConfigManager.SetTrainingLastSessionDate(JourTexte(Jour.AddDays(1)));

        Assert.False(TrainingReminders.WouldCount(Jour, DailyChallenge.SequenceLength));
    }

    // Constat C5.7 (variante rappels)
    // Une date de rappel ou de séance postérieure à aujourd'hui vaut aujourd'hui : pas de second
    // rappel le même jour après un recul d'horloge.
    [Fact]
    public void Rappel_DernierRappelDateDuLendemain_NeRepartPasAujourdhui()
    {
        using var isolation = new BancCapture.Isolation();
        ConfigManager.SetTrainingEnabled(true);
        TrainingReminders.MarkReminderShown(Jour.AddDays(1));
        var soir = Jour.ToDateTime(new TimeOnly(TrainingReminders.EarliestHour + 1, 0));

        Assert.False(TrainingReminders.ShouldRemind(soir, TrainingReminders.Snapshot()));
    }

    [Fact]
    public void Rappel_DerniereSeanceDateeDuLendemain_VautAujourdhui()
    {
        using var isolation = new BancCapture.Isolation();
        ConfigManager.SetTrainingEnabled(true);
        ConfigManager.SetTrainingLastSessionDate(JourTexte(Jour.AddDays(1)));
        var soir = Jour.ToDateTime(new TimeOnly(TrainingReminders.EarliestHour + 1, 0));

        Assert.False(TrainingReminders.ShouldRemind(soir, TrainingReminders.Snapshot()));
    }
}
