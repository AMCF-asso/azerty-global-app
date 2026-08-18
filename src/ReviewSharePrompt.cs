// Sollicitation d'avis déclenchée par un partage — v1.2.0, corrigée le 2026-08-18.
//
// Ce fichier ferme deux findings de l'audit v1.2.0, tous deux invisibles depuis
// TrayApplication parce que la décision y était mêlée à l'affichage.
//
// R1 — la garde « une seule sollicitation par jour » lisait `_reviewPromptShownDate`,
// un champ d'instance de TrayApplication, donc nul à chaque démarrage du processus.
// Elle ne gardait en réalité qu'« une seule par session de processus » : partager un
// résultat, redémarrer l'application, repartager le même jour brûlait les deux essais
// de la vie de l'installation. La garde travaille désormais sur la date persistée par
// ConfigManager, qui survit au redémarrage.
//
// R3 — ce chemin n'appliquait aucun seuil d'usage là où le chemin par notification en
// applique quatre. Un utilisateur du jour 1 qui terminait le Défi du jour et cliquait
// « Copier mon résultat » recevait la boîte de notation Store dans l'heure, et y
// laissait un des deux essais. Décision du 2026-08-18 : plancher minimal, et non les
// seuils de la notification. Le partage reste privilégié parce qu'il est volontaire —
// une notification interrompt, un partage est demandé — mais l'application doit avoir
// servi : une première frappe remappée, et MinActiveDays jours d'usage distincts.
//
// La décision est pure et prend une photographie de signaux, comme AutoStartNudge et
// TrainingReminders : testable sans fenêtre, sans Store et sans horloge réelle.

namespace AZERTYGlobal;

/// <summary>Photographie des signaux au moment de la décision (testable).</summary>
readonly record struct ReviewSharePromptSignals(
    bool IsPackaged,
    bool PromptClicked,
    int PromptCount,
    DateOnly? PromptLastShown,
    DateTime? LastErrorUtc,
    DateTime UtcNow,
    DateOnly Today,
    DateOnly? FirstRemapDate,
    int ActiveDaysCount);

static class ReviewSharePrompt
{
    /// <summary>Plafond d'essais sur la vie de l'installation, tous canaux confondus.</summary>
    public const int MaxPrompts = 2;

    /// <summary>Jours d'usage distincts avant qu'un partage puisse solliciter. Même valeur
    /// et même raison que <see cref="AutoStartNudge.MinActiveDays"/> — à 2, l'utilisateur
    /// est forcément revenu par ses propres moyens au moins une fois — mais déclarée à
    /// part : ce sont deux politiques distinctes, qui ont le droit de divergier.</summary>
    public const int MinActiveDays = 2;

    /// <summary>Décision pure : ce partage doit-il ouvrir la boîte de notation ? Ne touche
    /// à aucun état, n'affiche rien.</summary>
    public static bool ShouldPrompt(ReviewSharePromptSignals s)
    {
        // Hors package, aucune fiche Store à noter : le canal n'existe pas.
        if (!s.IsPackaged) return false;
        // L'utilisateur a répondu à une sollicitation : on ne le relance plus, quel que
        // soit ce qu'il a fait ensuite sur le Store.
        if (s.PromptClicked) return false;
        if (s.PromptCount >= MaxPrompts) return false;
        // On ne demande pas un avis à quelqu'un qui vient de rencontrer un problème.
        if (s.LastErrorUtc.HasValue &&
            s.UtcNow - s.LastErrorUtc.Value <
                TimeSpan.FromHours(TrayApplication.ReviewPromptErrorCooldownHours))
            return false;
        // R1 : date persistée, pas un champ d'instance. Survit au redémarrage.
        if (s.PromptLastShown == s.Today) return false;
        // R3 : sans première frappe remappée, l'application n'a jamais servi.
        if (s.FirstRemapDate == null) return false;
        if (s.ActiveDaysCount < MinActiveDays) return false;
        return true;
    }

    /// <summary>Photographie les signaux depuis ConfigManager et UsageStats.</summary>
    public static ReviewSharePromptSignals Snapshot() => new(
        IsPackaged: ConfigManager.IsPackaged,
        PromptClicked: ConfigManager.ReviewPromptClicked,
        PromptCount: ConfigManager.ReviewPromptCount,
        PromptLastShown: ConfigManager.ReviewPromptLastShown,
        LastErrorUtc: ConfigManager.LastErrorUtc,
        UtcNow: DateTime.UtcNow,
        Today: DateOnly.FromDateTime(DateTime.Now),
        FirstRemapDate: UsageStats.FirstRemapDate,
        ActiveDaysCount: UsageStats.ActiveDaysCount);
}
