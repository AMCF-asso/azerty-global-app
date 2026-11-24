using System.Globalization;
using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Audit des fichiers de l'app 1.3.0 (16/11/2026, docs/audit-fichiers.md). Chaque test affirme
/// le comportement CORRECT face à un fichier abîmé, d'une autre version, ou à une écriture
/// ratée ; il échoue tant que le défaut nommé dans son commentaire (constat F-xx) n'est pas
/// corrigé. Aucun test ne touche les vrais fichiers de l'utilisateur : tout vit dans un dossier
/// temporaire propre au test.
/// </summary>
public class AuditFichiersTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "AZGFICH_" + Guid.NewGuid().ToString("N"));
    private readonly CultureInfo _cultureOrigin = CultureInfo.CurrentCulture;
    private string ConfigPath => Path.Combine(_dir, "config.json");
    private string StatsPath => Path.Combine(_dir, "usage-stats.json");
    private string ProgressPath => Path.Combine(_dir, "lessons-progress.json");

    // Canal Store : statistiques et sollicitation d'avis n'existent que hors canal sobre ;
    // l'hote de tests est Unpackaged, sobre depuis la decision 5 du 2026-11-17.
    private readonly IDisposable _canalStore =
        AppChannel.OverrideForTests(DistributionChannel.Store);

    public AuditFichiersTests()
    {
        Directory.CreateDirectory(_dir);
        ConfigManager.OverrideConfigPathForTests(ConfigPath);
        UsageStats.OverrideStatsPathForTests(StatsPath);
    }

    public void Dispose()
    {
        _canalStore.Dispose();
        CultureInfo.CurrentCulture = _cultureOrigin;
        UsageStats.OverrideStatsPathForTests(Path.Combine(_dir, "inutilise.json"));
        ConfigManager.SaveDelayMilliseconds = -1;
        for (int essai = 0; essai < 20; essai++)
        {
            try { Directory.Delete(_dir, true); return; }
            catch (IOException) { Thread.Sleep(25); }
            catch (UnauthorizedAccessException) { Thread.Sleep(25); }
        }
    }

    private static LessonExercise Exercice(int rang = 0) =>
        new("m", "l", rang, "practice", "Tape", "ab", LessonTypingMode.Flexible);

    private static LessonAttemptStats Reussite(LessonExercise exercise)
    {
        var session = new LessonTypingSession(exercise);
        session.TypeChar('a');
        session.TypeChar('b');
        return session.Stats;
    }

    // ═══════════════════════════════════════════════════════════════
    // F-01 : texte JSON valide mais demi-caractère isolé (\uD800) : GetString lève
    // InvalidOperationException, hors des filtres de chargement.
    // ═══════════════════════════════════════════════════════════════

    // Constat F-01 (usage-stats.json) : UsageStats.Preload est appelé sans garde par
    // TrayApplication.Run ; une exception ici tue l'app à chaque lancement.
    [Theory]
    [InlineData("""{"firstRemapDate":"\uD800","activeDaysCount":3}""")]
    [InlineData("""{"lastActiveDate":"\uDC00","activeDaysCount":3}""")]
    [InlineData("""{"lastSpecialCharDate":"a\uD800b"}""")]
    public void Statistiques_demi_caractere_isole_ne_fait_pas_planter_le_chargement(string contenu)
    {
        File.WriteAllText(StatsPath, contenu);
        UsageStats.OverrideStatsPathForTests(StatsPath);

        Assert.Null(Record.Exception(UsageStats.Preload));
        Assert.Null(Record.Exception(() => UsageStats.RecordEmittedText("É")));
        Assert.Null(Record.Exception(UsageStats.Flush));
    }

    // Constat F-01 (lessons-progress.json) : le constructeur du magasin lève, la fenêtre des
    // Leçons ne s'ouvre plus (même effet que ME-02).
    [Theory]
    [InlineData("""{"version":1,"lastModuleId":"\uD800","exercises":{}}""")]
    [InlineData("""{"version":1,"exercises":{"\uD800":{"hash":"h","completed":true}}}""")]
    [InlineData("""{"version":1,"exercises":{"m/l/0/x":{"hash":"\uDC00","completed":true}}}""")]
    [InlineData("""{"version":1,"gestures":{"\uD800":{"attempts":1,"errors":0,"box":1}}}""")]
    public void Progression_demi_caractere_isole_ne_bloque_pas_les_Lecons(string contenu)
    {
        File.WriteAllText(ProgressPath, contenu);

        LessonProgressStore? store = null;
        Assert.Null(Record.Exception(() => store = new LessonProgressStore(ProgressPath)));
        var exercise = Exercice();
        Assert.Null(Record.Exception(() => store!.RecordSuccess(exercise, Reussite(exercise))));
        Assert.True(new LessonProgressStore(ProgressPath).IsCompleted(exercise));
    }

    // Constat F-01 (config.json) : clé ou valeur avec un demi-caractère isolé.
    [Theory]
    [InlineData("""{"\uD800":1,"activationConsent":true}""")]
    [InlineData("""{"appLanguage":"\uD800","activationConsent":true}""")]
    [InlineData("""{"_compat_log_salt":"\uDC00\uDC00\uDC00\uDC00\uDC00\uDC00\uDC00\uDC00\uDC00\uDC00\uDC00\uDC00\uDC00\uDC00\uDC00\uDC00\uDC00\uDC00\uDC00\uDC00\uDC00\uDC00\uDC00"}""")]
    [InlineData("""{"compatibility":{"\uD800.exe":"forceOn"}}""")]
    public void Config_demi_caractere_isole_ne_fait_pas_planter(string contenu)
    {
        File.WriteAllText(ConfigPath, contenu);

        Assert.Null(Record.Exception(() =>
        {
            _ = ConfigManager.AppLanguageWithoutLoading();
            _ = ConfigManager.ActivationConsent;
            _ = ConfigManager.AppLanguage;
            _ = ConfigManager.AnonymizeProcessName("jeu.exe");
            _ = ConfigManager.GetAllCompatibilityOverrides();
            ConfigManager.SetNotifications(false);
            ConfigManager.Flush();
        }));
        Assert.False(ConfigManager.NotificationsEnabled);
    }

    // Constat F-01, effet réel dans l'app (config.json) : le premier accès a lieu dans le try du
    // constructeur du tray (TrayApplication.cs:267-278), qui rattrape l'exception. Mais
    // EnsureLoaded a déjà posé _cache, à moitié rempli : les accès suivants le croient chargé,
    // _loadFailed reste faux, et la première écriture remplace config.json par cette moitié.
    // Les clés qui suivaient la clé abîmée sont perdues sans copie ni message.
    [Fact]
    public void Config_demi_caractere_isole_ne_fait_pas_ecraser_les_reglages_suivants()
    {
        const string original = """{"activationConsent":true,"\uD800":1,"notificationsEnabled":false,"appLanguage":"en"}""";
        File.WriteAllText(ConfigPath, original);

        try { _ = ConfigManager.EnsureCurrentVersionFirstRun("1.3.0", new DateOnly(2026, 11, 16), false); }
        catch (InvalidOperationException) { } // ce que fait le try du constructeur du tray
        ConfigManager.SetAutoStartNudgeDone();
        ConfigManager.Flush();

        string[] copies = Directory.GetFiles(_dir, "config.json" + FileQuarantine.Marker + "*");
        bool conserve = File.ReadAllText(ConfigPath) == original
            || copies.Any(c => File.ReadAllText(c) == original)
            || (!ConfigManager.NotificationsEnabled && ConfigManager.AppLanguage == "en");
        Assert.True(conserve, "réglages perdus : " + File.ReadAllText(ConfigPath));
    }

    // Constat F-01, effet réel dans l'app (usage-stats.json) : même mécanisme, _loaded est posé
    // avant la lecture ; les compteurs qui suivaient le champ abîmé repartent de zéro et sont
    // écrits par-dessus au flush suivant. Correct : compteurs gardés, ou fichier d'origine mis de
    // côté intact (décision AF-04 du 16/11 : statistiques corrompues mises de côté).
    [Fact]
    public void Statistiques_demi_caractere_isole_ne_fait_pas_ecraser_les_compteurs_suivants()
    {
        const string original = """{"firstRemapDate":"2025-01-02","lastActiveDate":"\uDC00","activeDaysCount":412,"bestStreak":96}""";
        File.WriteAllText(StatsPath, original);
        UsageStats.OverrideStatsPathForTests(StatsPath);

        try { _ = UsageStats.FirstRemapDate; }
        catch (InvalidOperationException) { } // ce que fait le try du constructeur du tray
        UsageStats.RecordEmittedText("É");
        UsageStats.Flush();

        UsageStats.OverrideStatsPathForTests(StatsPath); // relance simulée
        string[] copies = Directory.GetFiles(_dir, "usage-stats.json" + FileQuarantine.Marker + "*");
        bool conserve = (UsageStats.ActiveDaysCount >= 412 && UsageStats.BestStreak == 96)
            || copies.Any(c => File.ReadAllText(c) == original);
        Assert.True(conserve, "jours d'usage relus : " + UsageStats.ActiveDaysCount + ", aucune copie intacte");
    }

    // Témoin réciproque de F-01 : une vraie paire (𝔸, U+1D538) se relit sans erreur. Sans lui, un
    // chargement qui échouerait pour une autre raison ferait passer les tests ci-dessus pour rien.
    [Fact]
    public void Temoin_paire_complete_se_relit_dans_les_trois_fichiers()
    {
        File.WriteAllText(StatsPath, """{"firstRemapDate":"𝔸","activeDaysCount":3}""");
        UsageStats.OverrideStatsPathForTests(StatsPath);
        Assert.Null(Record.Exception(UsageStats.Preload));
        Assert.Equal(3, UsageStats.ActiveDaysCount);

        File.WriteAllText(ProgressPath, """{"version":1,"lastModuleId":"𝔸","exercises":{}}""");
        Assert.Equal("\U0001D538", new LessonProgressStore(ProgressPath).LastModuleId);

        File.WriteAllText(ConfigPath, """{"𝔸":1,"activationConsent":true}""");
        Assert.True(ConfigManager.ActivationConsent);
    }

    // ═══════════════════════════════════════════════════════════════
    // F-02 : réussite de Leçon perdue sur un échec d'écriture passager
    // ═══════════════════════════════════════════════════════════════

    // Témoin réciproque de F-02 : sans verrou, le même scénario garde les deux réussites.
    [Fact]
    public void Temoin_sans_verrou_les_deux_reussites_sont_gardees()
    {
        var premier = Exercice(0);
        var second = Exercice(1);
        var store = new LessonProgressStore(ProgressPath);
        store.SetLastPosition(premier);

        store.RecordSuccess(premier, Reussite(premier));
        store.RecordSuccess(second, Reussite(second));

        Assert.True(new LessonProgressStore(ProgressPath).IsCompleted(premier));
        Assert.True(new LessonProgressStore(ProgressPath).IsCompleted(second));
    }

    // Constat F-02 : un lecteur tient le fichier (antivirus, sauvegarde) au moment où une
    // réussite s'écrit. CommitOrRollback remet la mémoire à l'état du disque et rien ne
    // réessaie : la réussite disparaît sans un mot, même après la levée du verrou et une
    // nouvelle écriture réussie.
    [Fact]
    public void Progression_reussite_survit_a_un_echec_d_ecriture_passager()
    {
        var premier = Exercice(0);
        var second = Exercice(1);
        var store = new LessonProgressStore(ProgressPath);
        store.SetLastPosition(premier); // fichier présent, écrit normalement

        using (new FileStream(ProgressPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            store.RecordSuccess(premier, Reussite(premier)); // File.Replace refusé : violation de partage

        store.RecordSuccess(second, Reussite(second)); // verrou levé : cette écriture réussit

        Assert.True(store.IsCompleted(premier));
        Assert.True(new LessonProgressStore(ProgressPath).IsCompleted(premier));
    }

    // ═══════════════════════════════════════════════════════════════
    // F-03 : données d'une version plus récente perdues à la première écriture
    // ═══════════════════════════════════════════════════════════════

    // Constat F-03 (usage-stats.json) : SnapshotLocked réécrit la liste fixe de ses champs ;
    // un champ ajouté par une version plus récente (retour arrière, copie d'un autre poste)
    // disparaît. config.json, lui, garde les clés inconnues depuis AG130-11 (a).
    [Fact]
    public void Statistiques_champ_inconnu_d_une_version_recente_survit_a_l_ecriture()
    {
        File.WriteAllText(StatsPath, """{"activeDaysCount":3,"champFutur":{"a":1}}""");
        UsageStats.OverrideStatsPathForTests(StatsPath);

        UsageStats.RecordEmittedText("É");
        UsageStats.Flush();

        Assert.Contains("champFutur", File.ReadAllText(StatsPath));
    }

    // Constat F-03 (lessons-progress.json) : un champ ajouté sous le même format 1 (c'est ainsi
    // que « gestures » est né, voir LessonProgressStore) disparaît à la première réussite.
    [Fact]
    public void Progression_champ_inconnu_du_meme_format_survit_a_l_ecriture()
    {
        File.WriteAllText(ProgressPath, """{"version":1,"exercises":{},"champFutur":[1,2,3]}""");
        var exercise = Exercice();

        new LessonProgressStore(ProgressPath).RecordSuccess(exercise, Reussite(exercise));

        Assert.Contains("champFutur", File.ReadAllText(ProgressPath));
    }

    // Constat F-03 (config.json, sous-objet compatibility) : un mode qu'une version récente
    // connaît et pas celle-ci est jeté au chargement, donc effacé à la première écriture.
    [Fact]
    public void Config_mode_de_compatibilite_inconnu_survit_a_l_ecriture()
    {
        File.WriteAllText(ConfigPath, """{"activationConsent":true,"compatibility":{"jeu.exe":"auto"}}""");

        ConfigManager.SetNotifications(false);
        ConfigManager.Flush();

        Assert.Contains("\"auto\"", File.ReadAllText(ConfigPath));
    }

    // ═══════════════════════════════════════════════════════════════
    // F-05 : statistiques corrompues écrasées sans copie
    // ═══════════════════════════════════════════════════════════════

    // Constat F-05 (à trancher) : config.json et lessons-progress.json corrompus sont mis de
    // côté (FileQuarantine) ; usage-stats.json corrompu est réécrit par-dessus, sans copie.
    // L'ancien contenu (jours d'usage, série, compteurs) n'est plus récupérable.
    [Fact]
    public void Statistiques_corrompues_sont_mises_de_cote_avant_d_etre_reecrites()
    {
        const string original = """{"activeDaysCount":412,"bestStreak":96,""";
        File.WriteAllText(StatsPath, original);
        UsageStats.OverrideStatsPathForTests(StatsPath);

        UsageStats.RecordEmittedText("É");
        UsageStats.Flush();

        var copie = Assert.Single(Directory.GetFiles(_dir, "usage-stats.json" + FileQuarantine.Marker + "*"));
        Assert.Equal(original, File.ReadAllText(copie));
    }

    // ═══════════════════════════════════════════════════════════════
    // F-06 : fichiers temporaires orphelins après une coupure
    // ═══════════════════════════════════════════════════════════════

    // Constat F-06 : une coupure entre l'écriture du fichier temporaire et le remplacement laisse
    // « config.json.<pid>.tmp ». Le PID change au lancement suivant : rien ne le retire jamais.
    [Fact]
    public void Config_temporaire_orphelin_d_un_ancien_processus_est_retire()
    {
        File.WriteAllText(ConfigPath, """{"activationConsent":true}""");
        string orphelin = ConfigManager.BuildTempPath(ConfigPath, 999_999);
        File.WriteAllText(orphelin, """{"activationConsent":true,"x":""");

        ConfigManager.SetNotifications(false);
        ConfigManager.Flush();

        Assert.False(File.Exists(orphelin));
    }

    // Témoin : une écriture réussie ou ratée de ce processus ne laisse aucun temporaire, dans les
    // trois fichiers (les tests existants vérifient encore l'ancien nom fixe « .tmp »).
    [Fact]
    public void Temoin_aucun_temporaire_du_processus_ne_reste_apres_echec()
    {
        var exercise = Exercice();
        var store = new LessonProgressStore(ProgressPath);
        store.SetLastPosition(exercise);
        using (new FileStream(ProgressPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            store.RecordSuccess(exercise, Reussite(exercise));

        UsageStats.RecordEmittedText("É");
        UsageStats.Flush();
        using (new FileStream(StatsPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            UsageStats.RecordEmittedText("Ç");
            UsageStats.Flush();
        }

        ConfigManager.SetNotifications(false);
        ConfigManager.Flush();
        using (new FileStream(ConfigPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            ConfigManager.SetNotifications(true);
            ConfigManager.Flush();
        }

        Assert.Empty(Directory.GetFiles(_dir, "*.tmp"));
    }

    // ═══════════════════════════════════════════════════════════════
    // Réfutations : scénarios essayés, comportement correct (tests verts)
    // ═══════════════════════════════════════════════════════════════

    // R-01 : fichier tronqué à zéro octet ou rempli de zéros (coupure de courant sur un fichier
    // écrit sans Flush(true), par une version antérieure) : aucun plantage, config et progression
    // mises de côté, statistiques reparties de zéro.
    [Theory]
    [InlineData("")]
    [InlineData("\0\0\0\0\0\0\0\0")]
    [InlineData("{\"activeDaysCount\":4")]
    public void Refute_fichier_tronque_ou_nul_ne_fait_pas_planter(string contenu)
    {
        File.WriteAllText(ConfigPath, contenu);
        File.WriteAllText(StatsPath, contenu);
        File.WriteAllText(ProgressPath, contenu);
        UsageStats.OverrideStatsPathForTests(StatsPath);
        var exercise = Exercice();

        Assert.Null(Record.Exception(() =>
        {
            _ = ConfigManager.AppLanguageWithoutLoading();
            _ = ConfigManager.ActivationConsent;
            UsageStats.Preload();
            new LessonProgressStore(ProgressPath).RecordSuccess(exercise, Reussite(exercise));
        }));
        Assert.Single(Directory.GetFiles(_dir, "config.json" + FileQuarantine.Marker + "*"));
        Assert.Single(Directory.GetFiles(_dir, "lessons-progress.json" + FileQuarantine.Marker + "*"));
    }

    // R-02 : config.json d'une autre version où une clé connue a changé de type : chaque
    // accesseur vérifie le type et retombe sur le défaut, sans exception.
    [Fact]
    public void Refute_cles_connues_d_un_autre_type_retombent_sur_le_defaut()
    {
        File.WriteAllText(ConfigPath, """
            {"activationConsent":1,"notificationsEnabled":"non","reviewPromptCount":"2",
             "reviewPromptLastShown":20261116,"lessonsWindowBounds":[1,2,3,4],
             "compatibility":["jeu.exe"],"appLanguage":{"code":"en"}}
            """);

        Assert.Null(Record.Exception(() =>
        {
            Assert.False(ConfigManager.ActivationConsent);
            Assert.True(ConfigManager.NotificationsEnabled);
            Assert.Equal(0, ConfigManager.ReviewPromptCount);
            Assert.Null(ConfigManager.ReviewPromptLastShown);
            Assert.False(ConfigManager.TryGetWindowBounds(ConfigManager.LessonsWindowBoundsKey, out _));
            Assert.Empty(ConfigManager.GetAllCompatibilityOverrides());
            _ = ConfigManager.AppLanguage;
            ConfigManager.SetNotifications(false);
            ConfigManager.Flush();
        }));
    }

    // R-03 : deux magasins de progression dans le même processus (fenêtre des Leçons ouverte,
    // tutoriel qui verse ses gestes) : le second écrivain relit avant d'écrire, rien ne se perd.
    [Fact]
    public void Refute_deux_magasins_du_meme_processus_ne_s_ecrasent_pas()
    {
        var exercise = Exercice();
        var lecons = new LessonProgressStore(ProgressPath);
        lecons.SetLastPosition(exercise);

        var tutoriel = new LessonProgressStore(ProgressPath);
        var stats = Reussite(exercise);
        tutoriel.RecordGestures(stats, _ => true);
        Assert.NotEmpty(new LessonProgressStore(ProgressPath).Gestures);

        lecons.RecordSuccess(exercise, Reussite(exercise), _ => false);

        var relu = new LessonProgressStore(ProgressPath);
        Assert.True(relu.IsCompleted(exercise));
        Assert.NotEmpty(relu.Gestures);
    }

    // ═══════════════════════════════════════════════════════════════
    // F-04 : position des fenêtres écrite dans la culture du moment
    // ═══════════════════════════════════════════════════════════════

    // Constat F-04 : SetWindowBounds formate les entiers avec la culture courante et
    // TryGetWindowBounds les relit avec la culture courante. Une coordonnée négative (écran à
    // gauche du principal) écrite sous un format régional dont le signe moins est U+2212
    // (sv-SE, nb-NO, fi-FI sous ICU) n'est plus lisible après un changement de format.
    [Theory]
    [InlineData("sv-SE", "fr-FR")]
    [InlineData("fr-FR", "sv-SE")]
    public void Config_position_de_fenetre_relue_apres_changement_de_format_regional(string ecriture, string lecture)
    {
        CultureInfo.CurrentCulture = new CultureInfo(ecriture);
        ConfigManager.SetWindowBounds(ConfigManager.LessonsWindowBoundsKey,
            new Win32.RECT { left = -1500, top = -20, right = -500, bottom = 700 });
        ConfigManager.Flush();
        ConfigManager.OverrideConfigPathForTests(ConfigPath); // relance simulée

        CultureInfo.CurrentCulture = new CultureInfo(lecture);
        Assert.True(ConfigManager.TryGetWindowBounds(ConfigManager.LessonsWindowBoundsKey, out var rect));
        Assert.Equal(-1500, rect.left);
        Assert.Equal(-20, rect.top);
    }

    // ═══════════════════════════════════════════════════════════════
    // Revue des correctifs (16/11) : cas qui échappaient au premier correctif
    // ═══════════════════════════════════════════════════════════════

    // Revue d'AF-05 : une position ancienne écrite sous un format arabe, hébreu ou persan (ICU)
    // porte une marque de direction devant le signe moins.
    [Theory]
    [InlineData("؜-1500")]
    [InlineData("‎-1500")]
    [InlineData("‏−" + "1500")]
    public void Config_position_avec_marque_de_direction_se_relit(string gauche)
    {
        File.WriteAllText(ConfigPath, "{\"" + ConfigManager.LessonsWindowBoundsKey + "\":\"" + gauche + ",20,800,600\"}");
        Assert.True(ConfigManager.TryGetWindowBounds(ConfigManager.LessonsWindowBoundsKey, out var rect));
        Assert.Equal(-1500, rect.left);
    }

    // Revue d'AF-02 : après un échec d'écriture, un autre écrivain change le fichier, puis la
    // relecture tombe sur un verrou. La réussite en attente ne doit pas être abandonnée.
    [Fact]
    public void Progression_attente_survit_a_une_relecture_verrouillee()
    {
        var premier = Exercice(0);
        var autre = Exercice(1);
        var troisieme = Exercice(2);
        var store = new LessonProgressStore(ProgressPath);
        store.SetLastPosition(premier);
        using (new FileStream(ProgressPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            store.RecordSuccess(premier, Reussite(premier)); // écriture refusée : en attente
        new LessonProgressStore(ProgressPath).RecordSuccess(autre, Reussite(autre)); // autre écrivain

        using (new FileStream(ProgressPath, FileMode.Open, FileAccess.Read, FileShare.None))
            store.RecordSuccess(troisieme, Reussite(troisieme)); // relecture impossible
        Assert.True(store.RetryPendingSave());

        var relu = new LessonProgressStore(ProgressPath);
        Assert.True(relu.IsCompleted(premier));
        Assert.True(relu.IsCompleted(autre));
        Assert.True(relu.IsCompleted(troisieme));
    }

    // Revue d'AF-02 : un rejeu des heures plus tard garde l'heure réelle du geste.
    [Fact]
    public void Progression_rejeu_garde_l_heure_du_geste()
    {
        var t1 = new DateTimeOffset(2026, 11, 16, 10, 0, 0, TimeSpan.Zero);
        var maintenant = t1;
        var store = new LessonProgressStore(ProgressPath, () => maintenant);
        store.SetLastPosition(Exercice(0));
        using (new FileStream(ProgressPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            store.RecordGestures(Reussite(Exercice(0)), _ => true);
        new LessonProgressStore(ProgressPath).SetLastPosition(Exercice(1)); // force relecture et rejeu

        maintenant = t1.AddHours(5);
        Assert.True(store.RetryPendingSave());
        Assert.Equal(t1, store.Gestures["a"].LastReviewUtc);
    }

    // Revue d'AF-02 : sur un échec durable, la file ne grandit pas à chaque navigation.
    [Fact]
    public void Progression_file_d_attente_bornee_sur_echec_durable()
    {
        Directory.CreateDirectory(ProgressPath); // un dossier à la place du fichier : écriture toujours refusée
        var store = new LessonProgressStore(ProgressPath);
        for (int rang = 0; rang < 5; rang++)
            store.SetLastPosition(Exercice(rang));
        Assert.Equal(1, store.PendingChangeCount);
        Assert.Equal(4, store.LastExerciseIndex);
    }

    // Revue d'AF-02 : la fenêtre des Leçons recréée (changement de langue) reprend le même
    // magasin, donc la même file d'attente.
    [Fact]
    public void Progression_magasin_partage_garde_l_attente_entre_deux_fenetres()
    {
        var exercise = Exercice();
        var premier = LessonProgressStore.Shared;
        premier.SetLastPosition(exercise);
        using (new FileStream(ProgressPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            premier.RecordSuccess(exercise, Reussite(exercise));

        var second = LessonProgressStore.Shared;
        Assert.Same(premier, second);
        Assert.True(second.RetryPendingSave());
        Assert.True(new LessonProgressStore(ProgressPath).IsCompleted(exercise));
    }
}
