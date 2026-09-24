// Portes de la sollicitation d'avis — audit 24/09 (décision d'Antoine du 2026-09-24).
namespace AZERTYGlobal;

/// <summary>Ce qui a amené <c>TrayApplication.MaybeShowReviewPrompt</c> à se poser la question.</summary>
internal enum ReviewPromptTrigger
{
    /// <summary>Démarrage de l'application, ou fermeture de l'accueil qui l'avait différé.</summary>
    Startup,
    /// <summary>Seuil de caractères enrichis armé, frappe dans cette session, puis silence.</summary>
    QuietTyping,
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
}
