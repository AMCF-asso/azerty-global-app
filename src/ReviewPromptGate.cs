// Sollicitation d'avis : état persistant, photographie des signaux et décisions pures.
// Audit du 24/09 (portes de l'essai 1 et des séances), audit du 25/09 (A-02 : un seul
// état et une seule décision pour la notification).
using System.Globalization;

namespace AZERTYGlobal;

/// <summary>Ce qui a amené <c>TrayApplication.MaybeShowReviewPrompt</c> à se poser la question.</summary>
internal enum ReviewPromptTrigger
{
    /// <summary>Démarrage de l'application, ou fermeture de l'accueil qui l'avait différé.</summary>
    Startup,
    /// <summary>Seuil de caractères enrichis armé, frappe dans cette session, puis silence.</summary>
    QuietTyping,
    /// <summary>Partage d'un résultat (chemin <c>ReviewSharePrompt</c>). N'emprunte que la
    /// garde des séances d'apprentissage (<see cref="ReviewPromptGate.LearningAllowsPrompt"/>).</summary>
    Share,
}

/// <summary>
/// État persistant de la sollicitation d'avis, lu d'un bloc : tout lecteur passe par
/// <see cref="Load"/>. Les clés restent celles des versions publiées
/// (<c>reviewPromptCount</c>, <c>reviewPromptLastShown</c>, <c>reviewPromptClicked</c>) et
/// s'écrivent toujours par <c>ConfigManager.RecordReviewPromptShown</c>, qui pose aussi
/// <c>reviewPromptDone</c> pour un retour arrière en v1.1, et
/// <c>ConfigManager.SetReviewPromptClicked</c>.
/// </summary>
/// <param name="Attempts">Essais déjà affichés, plafonnés à <see cref="ReviewPromptGate.MaxAttempts"/>
/// par l'écriture.</param>
/// <param name="LastShown">Date locale du dernier essai, ou null (installation venue de
/// la v1.1, qui ne l'écrivait pas).</param>
/// <param name="Answered">L'utilisateur a répondu : clic sur une sollicitation, ou
/// « Noter » du menu. Plus aucune relance ensuite.</param>
internal readonly record struct ReviewState(int Attempts, DateOnly? LastShown, bool Answered)
{
    internal static ReviewState Load() => new(ConfigManager.ReviewPromptCount,
        ConfigManager.ReviewPromptLastShown, ConfigManager.ReviewPromptClicked);
}

/// <summary>Photographie des signaux de la sollicitation par notification, prise au moment
/// de la décision (testable). Les durées valent <see cref="long.MaxValue"/> quand l'événement
/// n'a pas eu lieu dans ce processus.</summary>
internal readonly record struct ReviewPromptSignals(
    bool ExternalLinks,
    bool NotificationsEnabled,
    ReviewState State,
    bool LearningOpen,
    long MillisecondsSinceLearningClosed,
    long MillisecondsSinceLastRemap,
    DateTime? LastErrorUtc,
    DateTime UtcNow,
    DateOnly Today,
    DateOnly? FirstRemapDate,
    int ActiveDaysCount,
    DateOnly? LastActiveDate,
    long EnrichedChars,
    long ActiveMinutes,
    bool UpgradedWithUsage,
    DateOnly? VersionFirstRunDate,
    string? TrainingLastReminderDate);

/// <summary>
/// Décisions pures de la sollicitation d'avis : sans état, sans I/O, pour être éprouvées
/// sans fenêtre. La notification a sa décision complète ici
/// (<see cref="ShouldShowNotification"/>) ; le partage garde la sienne dans
/// <c>ReviewSharePrompt</c>, qui emprunte le plafond, le clic et le silence après erreur.
/// </summary>
internal static class ReviewPromptGate
{
    /// <summary>Plafond d'essais sur la vie de l'installation, tous chemins confondus.</summary>
    internal const int MaxAttempts = 2;

    // Essai 1 — v1.3.0, décision d'Antoine du 2026-09-21 : le déclencheur est la preuve
    // d'usage de ce qu'AZERTY Global apporte, pas le calendrier. Le seuil de caractères
    // enrichis vit dans UsageStats.EnrichedCharsReviewThreshold, avec le compteur qu'il borne :
    // UsageStats.TryCategorize exclut par construction tout ce que l'AZERTY traditionnel de
    // Windows donne déjà. ⛔ Ne pas y recumuler les planchers de jours qu'il remplace : la
    // 1.1.0 a fait 0 notation pour 394 utilisateurs actifs en juillet 2026, le risque n'est
    // pas de trop demander.
    //
    // Plancher de temps de frappe actif, en minutes distinctes avec au moins une frappe
    // remappée (UsageStats.TotalActiveMinutes). Le compte de caractères seul se franchit en
    // quelques secondes sur un texte riche en accents, une durée ne se fabrique pas. Les
    // deux gardes sont cumulatives : QUOI a servi (20 caractères) et COMBIEN (10 minutes).
    internal const int FirstMinActiveMinutes = 10;
    /// <summary>Essai 2 : jours d'usage distincts.</summary>
    internal const int SecondActiveDays = 10;
    /// <summary>Essai 2 : jours au moins après l'essai 1, sinon le second tombe dans la même
    /// semaine et se lit comme une relance. Garde-fou d'espacement, pas preuve d'usage.</summary>
    internal const int SecondMinGapDays = 7;
    /// <summary>Au-delà de ces jours sans frappe, l'utilisateur est considéré comme parti :
    /// on ne relance pas un absent.</summary>
    internal const int StaleDays = 3;
    /// <summary>Pas de sollicitation dans ces heures après une erreur journalisée : on ne
    /// demande pas un avis à quelqu'un qui vient d'avoir un problème. Même valeur pour la
    /// notification et le partage.</summary>
    internal const int ErrorCooldownHours = 48;

    /// <summary>Reste-t-il un essai ? Ni réponse de l'utilisateur, ni plafond atteint.</summary>
    internal static bool AttemptsLeft(int attempts, bool answered)
        => !answered && attempts < MaxAttempts;

    /// <summary>Une erreur a-t-elle été journalisée il y a moins de
    /// <see cref="ErrorCooldownHours"/> heures ?</summary>
    internal static bool InErrorCooldown(DateTime? lastErrorUtc, DateTime utcNow)
        => lastErrorUtc.HasValue && utcNow - lastErrorUtc.Value < TimeSpan.FromHours(ErrorCooldownHours);

    /// <summary>
    /// Reste-t-il une sollicitation par notification à faire sur cette installation ? Ne
    /// lit que des réglages, jamais l'usage : dit si la minuterie de quiétude a une raison
    /// de tourner, pas si la sollicitation doit partir. Les liens externes comptent : la
    /// sollicitation ouvre la fiche Store et tombe avec eux (canal sobre D3, ou politique
    /// d'entreprise du lot C).
    /// </summary>
    internal static bool StillPossible(bool externalLinks, bool notificationsEnabled, ReviewState state)
        => externalLinks && notificationsEnabled && AttemptsLeft(state.Attempts, state.Answered);

    /// <summary>
    /// La sollicitation par notification doit-elle partir ? Deux essais au plus sur la vie
    /// de l'installation, déclenchés par l'usage réel et non par le calendrier.
    ///
    /// Essai 1 : <see cref="UsageStats.EnrichedCharsReviewThreshold"/> caractères que
    /// l'AZERTY traditionnel de Windows ne donne pas et <see cref="FirstMinActiveMinutes"/>
    /// minutes actives, aucun plancher de jours. Il ne part qu'au fil de la frappe, et une
    /// installation mise à jour depuis une version qui a servi attend le lendemain de son
    /// premier lancement (<see cref="FirstAttemptAllowed"/>). Il cède aussi la journée au
    /// rappel du Défi s'il est déjà parti ; l'inverse est tenu par <c>TrainingReminders</c>,
    /// qui se tait le jour d'un avis.
    ///
    /// Essai 2 : <see cref="SecondActiveDays"/> jours d'usage distincts,
    /// <see cref="SecondMinGapDays"/> jours au moins après l'essai 1, et une frappe il y a
    /// au plus <see cref="StaleDays"/> jours. Il part du démarrage comme de la frappe.
    ///
    /// Gardes communes : il reste un essai et personne n'a répondu, liens externes et
    /// notifications permis, aucune séance d'apprentissage ouverte ni refermée depuis moins
    /// de <see cref="LearningCooldownMinutes"/> minutes (<see cref="LearningAllowsPrompt"/>),
    /// aucune erreur depuis <see cref="ErrorCooldownHours"/> heures, et au moins une frappe
    /// remappée depuis l'installation.
    /// </summary>
    /// <param name="attempt">Numéro de l'essai qui part (textes de la notification) ; sans
    /// signification quand la réponse est non.</param>
    internal static bool ShouldShowNotification(ReviewPromptTrigger trigger, ReviewPromptSignals s,
        out int attempt)
    {
        attempt = s.State.Attempts + 1;
        if (!StillPossible(s.ExternalLinks, s.NotificationsEnabled, s.State)) return false;
        // Refus provisoires : le signal de frappe reste armé (TrayApplication.ShouldConsumeEnrichedSignal).
        if (!LearningAllowsPrompt(trigger, s.LearningOpen, s.MillisecondsSinceLearningClosed,
                s.MillisecondsSinceLastRemap, LearningCooldownMs))
            return false;
        if (InErrorCooldown(s.LastErrorUtc, s.UtcNow)) return false;
        // Sans première frappe remappée, l'application n'a jamais servi : rien à noter.
        if (s.FirstRemapDate == null) return false;

        if (attempt == 1)
        {
            if (!FirstAttemptAllowed(trigger, s.Today, s.UpgradedWithUsage, s.VersionFirstRunDate))
                return false;
            // Une seule sollicitation par jour avec le rappel du Défi. Défi masqué en 1.3.0 :
            // plus aucun rappel n'écrit cette date, la garde est inerte ; une date du jour ne
            // peut venir que d'un rappel réellement affiché aujourd'hui par une version
            // antérieure. Comparaison de la chaîne persistée, telle qu'écrite.
            if (s.TrainingLastReminderDate == s.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
                return false;
            if (s.EnrichedChars < UsageStats.EnrichedCharsReviewThreshold) return false;
            if (s.ActiveMinutes < FirstMinActiveMinutes) return false;
        }
        else
        {
            if (s.ActiveDaysCount < SecondActiveDays) return false;
            // Null pour une installation migrée depuis la v1.1, qui ne connaissait pas cette
            // date : l'essai 1 y est forcément ancien, le plancher est acquis.
            var lastShown = s.State.LastShown;
            if (lastShown.HasValue && s.Today.DayNumber - lastShown.Value.DayNumber < SecondMinGapDays)
                return false;
            if (s.LastActiveDate == null || s.Today.DayNumber - s.LastActiveDate.Value.DayNumber > StaleDays)
                return false;
        }
        return true;
    }

    /// <summary>Photographie les signaux depuis ConfigManager, PolicyManager, UsageStats et
    /// LearningSessionTracker. <paramref name="versionFirstRun"/> est le premier lancement de
    /// la version, établi une fois au démarrage ; null s'il n'a pas pu l'être, l'installation
    /// est alors traitée comme migrée et sans date, donc l'essai 1 refusé.</summary>
    internal static ReviewPromptSignals Snapshot(ConfigManager.VersionFirstRun? versionFirstRun) => new(
        ExternalLinks: PolicyManager.ExternalLinksEnabledNow,
        NotificationsEnabled: ConfigManager.NotificationsEnabled,
        State: ReviewState.Load(),
        LearningOpen: LearningSessionTracker.IsOpen,
        MillisecondsSinceLearningClosed: LearningSessionTracker.MillisecondsSinceLastClose,
        MillisecondsSinceLastRemap: UsageStats.MillisecondsSinceLastRemap,
        LastErrorUtc: ConfigManager.LastErrorUtc,
        UtcNow: DateTime.UtcNow,
        Today: DateOnly.FromDateTime(DateTime.Now),
        FirstRemapDate: UsageStats.FirstRemapDate,
        ActiveDaysCount: UsageStats.ActiveDaysCount,
        LastActiveDate: UsageStats.LastActiveDate,
        EnrichedChars: UsageStats.TotalSpecialCharsCount,
        ActiveMinutes: UsageStats.TotalActiveMinutes,
        UpgradedWithUsage: versionFirstRun?.UpgradedWithUsage ?? true,
        VersionFirstRunDate: versionFirstRun?.Date,
        TrainingLastReminderDate: ConfigManager.TrainingLastReminderDate);

    /// <summary>
    /// L'essai 1 peut-il partir ? Jamais depuis le démarrage (ni depuis la fermeture de
    /// l'accueil qui en est le relais) : seulement au fil de la frappe. Une installation qui
    /// avait déjà des statistiques d'usage au premier lancement de cette version attend en
    /// plus le lendemain de ce lancement, pour que la demande porte sur la version installée.
    /// Une installation neuve garde la règle d'avant, sans délai.
    ///
    /// Le défaut corrigé (audit du 24/09) : un utilisateur actif de la 1.1 dépasse déjà les
    /// deux seuils et son compteur d'essais repart à 0 (migration du 2026-09-20) ; au premier
    /// lancement de la 1.3, la demande partait dès la fermeture de l'accueil.
    /// </summary>
    /// <param name="versionFirstRunDate">Date locale du premier lancement de cette version,
    /// ou null si elle n'a pas pu être établie : l'installation migrée est alors refusée,
    /// faute de preuve du jour écoulé.</param>
    internal static bool FirstAttemptAllowed(ReviewPromptTrigger trigger, DateOnly today,
        bool upgradedWithUsage, DateOnly? versionFirstRunDate)
    {
        if (trigger != ReviewPromptTrigger.QuietTyping) return false;
        if (!upgradedWithUsage) return true;
        return versionFirstRunDate.HasValue && today > versionFirstRunDate.Value;
    }

    /// <summary>
    /// Le tick de quiétude doit-il tenter la sollicitation ? Le signal doit être armé, une
    /// frappe remappée avoir eu lieu dans cette session de processus
    /// (<paramref name="millisecondsSinceLastRemap"/> vaut <see cref="long.MaxValue"/>
    /// sinon), puis <paramref name="requiredSilenceMs"/> millisecondes de silence. Sans la
    /// deuxième condition, un signal armé au chargement partirait sans aucune frappe.
    /// </summary>
    internal static bool ShouldTryAfterQuietTyping(bool signalArmed, long millisecondsSinceLastRemap,
        long requiredSilenceMs, bool reviewDeferred)
    {
        if (!signalArmed || reviewDeferred) return false;
        if (millisecondsSinceLastRemap == long.MaxValue) return false;
        return millisecondsSinceLastRemap >= requiredSilenceMs;
    }

    /// <summary>
    /// Armer le signal au chargement quand le total dépasse déjà le seuil ? Seulement si
    /// l'essai 1 reste à faire : le signal au fil de la frappe ne sert qu'à lui, le second
    /// essai garde son chemin de démarrage et ses règles.
    /// </summary>
    internal static bool ShouldArmSignalAtLoad(int reviewPromptCount, bool stillPossible,
        long enrichedTotal, long threshold)
        => stillPossible && reviewPromptCount == 0 && enrichedTotal >= threshold;

    /// <summary>Délai sans sollicitation après la fin d'une séance de Leçons ou d'un
    /// tutoriel (décision d'Antoine du 2026-09-24).</summary>
    internal const int LearningCooldownMinutes = 10;
    internal const long LearningCooldownMs = LearningCooldownMinutes * 60_000L;

    /// <summary>
    /// Une séance d'apprentissage permet-elle de solliciter un avis ? Décision B d'Antoine
    /// du 2026-09-24 : jamais pendant une séance de Leçons ou un tutoriel, ni dans les
    /// <paramref name="cooldownMs"/> millisecondes qui suivent sa fin.
    ///
    /// Les frappes de la fenêtre Leçons passent par le moteur comme toutes les autres :
    /// elles comptent dans les statistiques et peuvent armer le seuil de caractères
    /// enrichis (le tutoriel de l'accueil, lui, en est exclu). Elles ne suffisent pas à
    /// déclencher la demande : au fil de la frappe, il faut en plus une frappe remappée
    /// après la fin de la séance (<paramref name="millisecondsSinceLastRemap"/> plus court
    /// que <paramref name="millisecondsSinceLearningClosed"/>), que le tick suivra de son
    /// silence habituel. Démarrage, relais de l'accueil et partage n'ont pas cette
    /// condition : ils ne naissent pas d'une frappe.
    /// </summary>
    /// <param name="millisecondsSinceLearningClosed"><see cref="long.MaxValue"/> si aucune
    /// séance ne s'est refermée dans ce processus.</param>
    /// <param name="millisecondsSinceLastRemap"><see cref="long.MaxValue"/> si aucune frappe
    /// remappée dans ce processus.</param>
    internal static bool LearningAllowsPrompt(ReviewPromptTrigger trigger, bool learningOpen,
        long millisecondsSinceLearningClosed, long millisecondsSinceLastRemap, long cooldownMs)
    {
        if (learningOpen) return false;
        if (millisecondsSinceLearningClosed == long.MaxValue) return true;
        if (millisecondsSinceLearningClosed < cooldownMs) return false;
        if (trigger != ReviewPromptTrigger.QuietTyping) return true;
        return millisecondsSinceLastRemap < millisecondsSinceLearningClosed;
    }
}
