// Relance unique du lancement automatique (v1.2.0).
//
// Décision du 2026-08-17. Le démarrage automatique est désactivé par défaut
// (`Enabled="false"` dans msix/AppxManifest.xml) et n'a qu'un seul chemin d'activation
// dans le flux normal : la case de l'étape 3 de l'accueil, que OnboardingWindow.Close()
// ne persiste que si `_step3Reached` — c'est-à-dire après deux clics sur « Suivant ».
// Qui referme l'accueil à l'étape 1 ou 2 (croix, Échap, « Quitter ») n'obtient donc
// jamais l'autostart, malgré une case pré-cochée qu'il n'a pas vue : l'application ne
// revient pas au démarrage suivant, et il la relance à la main ou l'oublie.
//
// Cette relance ne revient pas sur la décision v0.9.7.1 (aucune case jamais vue n'est
// persistée en silence) : elle demande, et n'active rien sans un clic explicite.
//
// Le déclencheur est `ActiveDaysCount >= 2` plutôt qu'un compteur de lancements. Deux
// jours d'usage distincts sans autostart signifient que l'utilisateur a relancé
// l'application lui-même : l'intention est déjà démontrée, on ne fait que lui épargner
// le geste. Un compteur de lancements confondrait ce cas avec deux redémarrages le même
// jour, et l'application ne compte de toute façon pas ses lancements.

namespace AZERTYGlobal;

/// <summary>Photographie des signaux au moment de la décision (testable).</summary>
readonly record struct AutoStartSignals(
    bool NudgeDone,
    bool AlreadyRegistered,
    bool NotificationsEnabled,
    DateOnly? FirstRemapDate,
    int ActiveDaysCount);

static class AutoStartNudge
{
    /// <summary>Jours d'usage distincts avant de proposer. À 2, l'utilisateur est
    /// forcément revenu par ses propres moyens au moins une fois.</summary>
    public const int MinActiveDays = 2;

    /// <summary>Décision pure : faut-il proposer le lancement automatique ? Ne touche
    /// à aucun état.</summary>
    public static bool ShouldPrompt(AutoStartSignals s)
    {
        if (s.NudgeDone) return false;              // une seule fois sur la vie de l'installation
        if (s.AlreadyRegistered) return false;      // déjà actif, rien à proposer
        if (!s.NotificationsEnabled) return false;  // canal coupé : l'entrée du menu reste
        // Sans première frappe remappée, l'application n'a jamais servi : rien à ancrer.
        if (s.FirstRemapDate == null) return false;
        if (s.ActiveDaysCount < MinActiveDays) return false;
        return true;
    }

    /// <summary>Photographie les signaux depuis AutoStart, ConfigManager et UsageStats.</summary>
    public static AutoStartSignals Snapshot() => new(
        NudgeDone: ConfigManager.AutoStartNudgeDone,
        AlreadyRegistered: AutoStart.IsRegistered,
        NotificationsEnabled: ConfigManager.NotificationsEnabled,
        FirstRemapDate: UsageStats.FirstRemapDate,
        ActiveDaysCount: UsageStats.ActiveDaysCount);

    /// <summary>Marque la proposition comme faite. Appelé AVANT l'affichage : une
    /// balloon qui échoue ne doit pas rendre une seconde tentative possible.</summary>
    public static void MarkPromptShown() => ConfigManager.SetAutoStartNudgeDone();
}
