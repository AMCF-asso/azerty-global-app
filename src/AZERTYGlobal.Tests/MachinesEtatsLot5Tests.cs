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

    // Constat C5.7 (suite ME-10 : la date lointaine est réécrite)
    // Une date de rappel ou de séance à +400 jours (horloge avancée puis remise) ne bloque que la
    // journée en cours : RepairFutureDates la ramène à aujourd'hui et la réécrit, le lendemain le
    // rappel repart et la séance du Défi commun compte de nouveau.
    [Fact]
    public void Rappel_DatesLointainesDansLeFutur_LeLendemainLeRappelRepart()
    {
        using var isolation = new BancCapture.Isolation();
        ConfigManager.SetTrainingEnabled(true);
        TrainingReminders.MarkReminderShown(Jour.AddDays(400));
        ConfigManager.SetTrainingLastSessionDate(JourTexte(Jour.AddDays(400)));

        TrainingReminders.RepairFutureDates(Jour);

        Assert.Equal(JourTexte(Jour), ConfigManager.TrainingLastReminderDate);
        Assert.Equal(JourTexte(Jour), ConfigManager.TrainingLastSessionDate);
        var soir = Jour.ToDateTime(new TimeOnly(TrainingReminders.EarliestHour + 1, 0));
        Assert.False(TrainingReminders.ShouldRemind(soir, TrainingReminders.Snapshot()),
            "le jour même, le rappel reste bloqué");
        Assert.True(TrainingReminders.ShouldRemind(soir.AddDays(1), TrainingReminders.Snapshot()),
            "le lendemain, le rappel repart");
    }

    [Fact]
    public void DefiCommun_SeanceDateeDe400Jours_LeLendemainLaSeanceCompteDeNouveau()
    {
        using var isolation = new BancCapture.Isolation();
        ConfigManager.SetTrainingSequenceIndex(DailyChallenge.SequenceLength);
        ConfigManager.SetTrainingLastSessionDate(JourTexte(Jour.AddDays(400)));

        TrainingReminders.RepairFutureDates(Jour);

        Assert.False(TrainingReminders.WouldCount(Jour, DailyChallenge.SequenceLength));
        Assert.True(TrainingReminders.WouldCount(Jour.AddDays(1), DailyChallenge.SequenceLength));
    }

    // Constat C5.7 (garde : une date passée ou du jour n'est pas touchée)
    [Fact]
    public void Rappel_DatesDuJourOuPassees_RepairFutureDatesNeLesChangePas()
    {
        using var isolation = new BancCapture.Isolation();
        TrainingReminders.MarkReminderShown(Jour);
        ConfigManager.SetTrainingLastSessionDate(JourTexte(Jour.AddDays(-3)));

        TrainingReminders.RepairFutureDates(Jour);

        Assert.Equal(JourTexte(Jour), ConfigManager.TrainingLastReminderDate);
        Assert.Equal(JourTexte(Jour.AddDays(-3)), ConfigManager.TrainingLastSessionDate);
    }

    // Constat C5.7 (suite ME-10 : les deux appels de réparation)
    // Les tests ci-dessus n'appellent que RepairFutureDates. Retirer l'appel de la fin de séance du
    // Défi, ou celui du rappel, les laissait verts : deux tests les couvrent maintenant.
    // Fin de séance : une date de rappel à +400 jours est ramenée à aujourd'hui par Complete (la
    // séance elle-même ne touche pas à la date de rappel), et la date de séance à +400 jours, qui
    // empêcherait de compter la séance du jour, l'est aussi.
    [Fact]
    public void FinDeSeance_RepareUneDateLointaineDansLeFutur()
    {
        using var isolation = new BancCapture.Isolation();
        ConfigManager.SetTrainingEnabled(true);
        ConfigManager.SetTrainingSequenceIndex(DailyChallenge.SequenceLength);
        var aujourdhui = DateOnly.FromDateTime(DateTime.Now);
        ConfigManager.SetTrainingLastReminderDate(JourTexte(aujourdhui.AddDays(400)));
        ConfigManager.SetTrainingLastSessionDate(JourTexte(aujourdhui.AddDays(400)));
        var etat = new ChallengeSessionState();
        etat.LoadCatalog();

        etat.Complete(new LessonAttemptStats(), previousBestWpm: null);

        Assert.Equal(JourTexte(aujourdhui), ConfigManager.TrainingLastReminderDate);
        Assert.Equal(JourTexte(aujourdhui), ConfigManager.TrainingLastSessionDate);
    }

    // Rappel : la réparation précède la garde NotificationsEnabled, sinon un utilisateur qui a coupé
    // les notifications garderait une date lointaine que rien ne répare. MaybeShowTrainingReminder
    // est une méthode d'instance privée de TrayApplication (fenêtre cachée) : test de texte source, dans le style
    // des tests voisins (voir MachinesEtatsLot4Tests).
    [Fact]
    public void MaybeShowTrainingReminder_ReparePuisSeulementEnsuiteLaGardeDesNotifications()
    {
        string source = File.ReadAllText(SourceDeLApp("TrayApplication.cs"));
        int debut = source.IndexOf("private void MaybeShowTrainingReminder()", StringComparison.Ordinal);
        Assert.True(debut > 0, "MaybeShowTrainingReminder introuvable dans TrayApplication.cs");
        int fin = source.IndexOf("catch (Exception ex)", debut, StringComparison.Ordinal);
        Assert.True(fin > debut, "fin de MaybeShowTrainingReminder introuvable");
        string methode = source[debut..fin];

        int reparation = methode.IndexOf("TrainingReminders.RepairFutureDates(", StringComparison.Ordinal);
        int garde = methode.IndexOf("ConfigManager.NotificationsEnabled", StringComparison.Ordinal);
        Assert.True(reparation >= 0, "RepairFutureDates n'est plus appelé par MaybeShowTrainingReminder");
        Assert.True(garde >= 0, "la garde NotificationsEnabled a disparu");
        Assert.True(reparation < garde, "RepairFutureDates doit précéder la garde NotificationsEnabled");
    }

    private static string SourceDeLApp(string fichier)
    {
        for (var dossier = new DirectoryInfo(AppContext.BaseDirectory); dossier != null; dossier = dossier.Parent)
        {
            string chemin = Path.Combine(dossier.FullName, "src", fichier);
            if (File.Exists(chemin)) return chemin;
        }
        throw new FileNotFoundException(fichier);
    }

    // Constat C5.5
    // Statistiques ouverte avec la section « Défi du jour » visible (opt-in coché) ; l'opt-in est
    // décoché depuis les Paramètres (fenêtre non modale) pendant l'ouverture. La peinture relit le
    // prédicat, la mise en page ne suit pas : la hauteur de contenu garde la section disparue.
    // Modèle suivi : Audit0911AccueilAProposTests.Statistiques_LangueChangéePendantLOuverture_LesTextesSuivent
    // (fenêtre ouverte, réglage changé à chaud) et TroncatureLibellesTests (UsageStatsWindow, _scroll).
    [Fact]
    public void Statistiques_SectionDefiMasqueePendantLOuverture_LaHauteurDeMiseEnPageSuit()
    {
        using var isolation = new BancCapture.Isolation();
        ConfigManager.SetTrainingEnabled(true);
        var fenetre = new UsageStatsWindow();
        UsageStatsWindow? reference = null;
        try
        {
            int hauteurSectionVisible = BancCapture.Field<WindowScroll>(fenetre, "_scroll").ContentHeight;

            ConfigManager.SetTrainingEnabled(false);

            reference = new UsageStatsWindow(); // mise en page faite d'emblée sans la section
            int hauteurAttendue = BancCapture.Field<WindowScroll>(reference, "_scroll").ContentHeight;
            Assert.True(hauteurSectionVisible > hauteurAttendue,
                "témoin : la section Défi doit ajouter de la hauteur quand elle est visible");

            Assert.Equal(hauteurAttendue, BancCapture.Field<WindowScroll>(fenetre, "_scroll").ContentHeight);
        }
        finally
        {
            if (reference != null) BancCapture.Teardown(reference);
            BancCapture.Teardown(fenetre);
        }
    }

    private static string ÉcrireProgression(string dossier, string exercices)
    {
        string chemin = Path.Combine(dossier, LessonProgressStore.FileName);
        File.WriteAllText(chemin, "{\"version\":1,\"exercises\":" + exercices + "}", new UTF8Encoding(false));
        return chemin;
    }

    // Constat C5.6
    // lessons-progress.json valide mais avec exercises: { "abc": 3 } : ReadString appelle
    // JsonElement.TryGetProperty sur un nombre, qui lève InvalidOperationException, hors du filtre
    // de Load. Une entrée mal formée ne doit pas faire échouer le chargement.
    // Modèle suivi : QuarantaineTests (fichier de progression temporaire, new LessonProgressStore(path)).
    [Fact]
    public void Progression_UneEntreeNonObjet_NeFaitPasEchouerLeChargement()
    {
        using var isolation = new BancCapture.Isolation();
        string dossier = Path.Combine(Path.GetTempPath(), "AZGLot5_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dossier);
        try
        {
            string chemin = ÉcrireProgression(dossier, "{\"abc\":3}");

            var exception = Record.Exception(() => new LessonProgressStore(chemin));

            Assert.Null(exception);
        }
        finally { try { Directory.Delete(dossier, true); } catch { } }
    }

    // Constat C5.6 (variante : les autres exercices survivent)
    // Entrées nombre, null, tableau et chaîne mêlées à une entrée valide : seules les entrées mal
    // formées sont ignorées, l'entrée valide est conservée et le fichier n'est pas mis de côté.
    // Modèle suivi : QuarantaineTests (ProgressionValide, Copies, FileQuarantine.Marker).
    [Fact]
    public void Progression_EntreesMalFormees_IgnoreesSansPerdreLesExercicesValides()
    {
        using var isolation = new BancCapture.Isolation();
        string dossier = Path.Combine(Path.GetTempPath(), "AZGLot5_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dossier);
        try
        {
            string chemin = ÉcrireProgression(dossier,
                "{\"x\":3,\"y\":null,\"z\":[1],\"s\":\"texte\",\"ok\":{\"hash\":\"h\",\"completed\":true}}");

            LessonProgressStore? magasin = null;
            var exception = Record.Exception(() => magasin = new LessonProgressStore(chemin));

            Assert.Null(exception);
            Assert.NotNull(magasin);
            Assert.True(magasin!.Exercises.ContainsKey("ok"), "l'exercice valide a été perdu");
            Assert.True(magasin.Exercises["ok"].Completed);
            Assert.Single(magasin.Exercises);
            Assert.Empty(Directory.GetFiles(dossier, LessonProgressStore.FileName + FileQuarantine.Marker + "*"));
        }
        finally { try { Directory.Delete(dossier, true); } catch { } }
    }
}
