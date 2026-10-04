namespace AZERTYGlobal;

internal static partial class L
{
    public static string Settings_WindowTitle => T($"{Product} — Paramètres", $"{Product} — Settings");

    public static string Settings_SectionShortcuts => T("Raccourcis", "Shortcuts");
    public static string Settings_SectionPreferences => T("Préférences", "Preferences");
    public static string Settings_SectionLanguage => T("Langue", "Language");
    public static string Settings_SectionWindows => T("Fenêtres", "Windows");

    public static string Settings_ShortcutLabelKeyboard => T("Clavier virtuel", "Virtual keyboard");
    public static string Settings_ShortcutLabelSearch => T("Recherche", "Search");
    public static string Settings_ShortcutModifier2 => T("Maj", "Shift");

    public static string Settings_LinkResetDefaults => T("Réinitialiser les raccourcis", "Reset shortcuts");
    public static string Settings_ShortcutCaptureHint => T("Appuyez sur une touche autorisée", "Press an allowed key");

    public static string Settings_AutoStart => T("Lancer au démarrage de Windows", "Launch at Windows startup");
    public static string Settings_Notifications => T("Notifications", "Notifications");
    public static string Settings_OnboardingWindow => T("Afficher l’accueil au démarrage", "Show the welcome screen at startup");

    /// <summary>Mention portée par un réglage imposé par une politique d'entreprise (lot C
    /// du 2026-08-19). Formulation validée mot à mot par Antoine, fr et en : c'est celle que
    /// Windows emploie dans ses propres Paramètres, donc déjà connue en parc.</summary>
    public static string Settings_ManagedByOrganization => T("Géré par votre organisation", "Managed by your organization");

    public static string Settings_ResetVirtualKeyboard => T("Réinitialiser la fenêtre du clavier virtuel", "Reset virtual keyboard window");
    public static string Settings_ResetLessonsModule => T("Réinitialiser la fenêtre Leçons", "Reset Lessons window");

    // Onglets (2026-09-21). Libellés courts : la bande fait 300 px de large à
    // 100 %, trois onglets y tiennent seulement si aucun ne dépasse ~14 signes.
    public static string Settings_TabGeneral => T("Général", "General");
    public static string Settings_TabApplications => T("Applications", "Applications");
    public static string Settings_TabLanguageMaintenance => T("Langue", "Language");

    public static string Settings_ConfirmResetShortcuts => T(
        "Réinitialiser les raccourcis aux valeurs par défaut\n(Ctrl + Maj + Q et Ctrl + Maj + W) ?",
        "Reset shortcuts to default values\n(Ctrl + Shift + Q and Ctrl + Shift + W)?");
    public static string Settings_ShortcutsReset => T("Raccourcis réinitialisés ✓", "Shortcuts reset ✓");
    public static string Settings_VirtualKeyboardWindowReset => T("Fenêtre du clavier virtuel réinitialisée ✓", "Virtual keyboard window reset ✓");
    public static string Settings_LessonsWindowReset => T("Fenêtre Leçons réinitialisée ✓", "Lessons window reset ✓");
    public static string Settings_ShortcutReserved => T("Touche réservée : conflit avec d’autres applications", "Reserved key (conflicts with apps)");
    public static string Settings_ShortcutAlreadyUsed => T("Déjà utilisée", "Already in use");
    public static string Settings_ShortcutKeyboardUpdated => T("Raccourci du clavier virtuel mis à jour ✓", "Virtual keyboard shortcut updated ✓");
    public static string Settings_ShortcutSearchUpdated => T("Raccourci de recherche mis à jour ✓", "Search shortcut updated ✓");

    // ── Section « Apps suspendues » (v1.2.0) — overrides de compatibilité par process ──
    public static string Settings_SectionCompat => T("Compatibilité des applications", "App compatibility");
    public static string Settings_CompatAdd => T("Ajouter…", "Add…");
    public static string Settings_CompatRemove => T("Retirer", "Remove");
    public static string Settings_CompatModeAuto => T("Auto (détection automatique)", "Auto (automatic detection)");
    public static string Settings_CompatModeForceOn => T("Forcer la compatibilité jeu", "Force game compatibility");
    public static string Settings_CompatModeForceOff => T("Forcer la désactivation", "Force off");
    // Libellés courts affichés dans la liste, à côté du nom du process
    public static string Settings_CompatListForceOn => T("compatibilité jeu", "game compatibility");
    public static string Settings_CompatListForceOff => T("désactivée", "disabled");
    public static string Settings_CompatAdded(string process) => T($"« {process} » ajoutée ✓", $"\"{process}\" added ✓");
    public static string Settings_CompatRemoved(string process) => T($"« {process} » retirée ✓", $"\"{process}\" removed ✓");
    public static string Settings_CompatUpdated(string process) => T($"« {process} » mise à jour ✓", $"\"{process}\" updated ✓");
    public static string Settings_CompatForceOnRefused => T(
        "Compatibilité jeu refusée : application protégée ou de connexion à distance",
        "Game compatibility refused: protected or remote-access app");
    public static string Settings_CompatFilterExe => T("Applications (*.exe)", "Applications (*.exe)");
    public static string Settings_CompatPickerTitle => T("Choisir une application", "Choose an app");
}
