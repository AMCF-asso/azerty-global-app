// Portes de la sollicitation d'avis — audit 24/09 (décision d'Antoine du 2026-09-24).
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
/// Décisions pures de la sollicitation d'avis ajoutées par l'audit du 24/09 : sans état,
/// sans I/O, pour être éprouvées sans fenêtre. Les gardes historiques (plafond de deux
/// essais, clic, erreur récente, seuils d'usage) restent dans
/// <c>TrayApplication.MaybeShowReviewPrompt</c>.
///
/// Le défaut corrigé : un utilisateur actif de la 1.1 dépasse déjà 20 caractères enrichis
/// et 10 minutes, et son compteur d'essais repart à 0 (migration du 2026-09-20). Au premier
/// lancement de la 1.3, la demande partait dès la fermeture de l'accueil, avant la moindre
/// frappe dans cette version.
/// </summary>
internal static class ReviewPromptGate
{
    /// <summary>
    /// L'essai 1 peut-il partir ? Jamais depuis le démarrage (ni depuis la fermeture de
    /// l'accueil qui en est le relais) : seulement au fil de la frappe. Une installation qui
    /// avait déjà des statistiques d'usage au premier lancement de cette version attend en
    /// plus le lendemain de ce lancement, pour que la demande porte sur la version installée.
    /// Une installation neuve garde la règle d'avant, sans délai.
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
