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

    public static string Settings_LinkResetDefaults => T("Valeurs par défaut", "Default values");
    public static string Settings_ShortcutCaptureHint => T("Appuyez sur une touche autorisée", "Press an allowed key");

    public static string Settings_AutoStart => T("Lancer au démarrage de Windows", "Launch at Windows startup");
    public static string Settings_Notifications => T("Notifications", "Notifications");
    public static string Settings_OnboardingWindow => T("Fenêtre de bienvenue au démarrage", "Welcome window at startup");

    public static string Settings_ResetVirtualKeyboard => T("Réinitialiser clavier virtuel", "Reset virtual keyboard");
    public static string Settings_ResetLessonsModule => T("Réinitialiser module Leçons", "Reset Lessons module");

    public static string Settings_ConfirmResetShortcuts => T(
        "Réinitialiser les raccourcis aux valeurs par défaut\n(Ctrl+Maj+Q et Ctrl+Maj+W) ?",
        "Reset shortcuts to default values\n(Ctrl+Shift+Q and Ctrl+Shift+W)?");
    public static string Settings_ShortcutsReset => T("Raccourcis réinitialisés ✓", "Shortcuts reset ✓");
    public static string Settings_VirtualKeyboardWindowReset => T("Fenêtre clavier virtuel réinitialisée ✓", "Virtual keyboard window reset ✓");
    public static string Settings_LessonsWindowReset => T("Fenêtre Leçons réinitialisée ✓", "Lessons window reset ✓");
    public static string Settings_ShortcutReserved => T("Touche réservée (conflit applications)", "Reserved key (conflicts with apps)");
    public static string Settings_ShortcutAlreadyUsed => T("Déjà utilisée", "Already in use");
    public static string Settings_ShortcutKeyboardUpdated => T("Raccourci clavier virtuel mis à jour ✓", "Virtual keyboard shortcut updated ✓");
    public static string Settings_ShortcutSearchUpdated => T("Raccourci recherche mis à jour ✓", "Search shortcut updated ✓");

    // ── Section « Apps suspendues » (v1.2.0) — overrides de compatibilité par process ──
    public static string Settings_SectionCompat => T("Apps suspendues", "Suspended apps");
    public static string Settings_CompatAdd => T("Ajouter…", "Add…");
    public static string Settings_CompatRemove => T("Retirer", "Remove");
    public static string Settings_CompatModeAuto => T("Auto (détection automatique)", "Auto (automatic detection)");
    public static string Settings_CompatModeForceOn => T("Forcer compatibilité jeu", "Force game compatibility");
    public static string Settings_CompatModeForceOff => T("Forcer désactivation", "Force disable");
    // Libellés courts affichés dans la liste, à côté du nom du process
    public static string Settings_CompatListForceOn => T("compatibilité jeu", "game compatibility");
    public static string Settings_CompatListForceOff => T("désactivée", "disabled");
    public static string Settings_CompatAdded(string process) => T($"« {process} » ajoutée ✓", $"\"{process}\" added ✓");
    public static string Settings_CompatRemoved(string process) => T($"« {process} » retirée ✓", $"\"{process}\" removed ✓");
    public static string Settings_CompatUpdated(string process) => T($"« {process} » mise à jour ✓", $"\"{process}\" updated ✓");
    public static string Settings_CompatForceOnRefused => T(
        "Compatibilité jeu refusée : application protégée ou de connexion à distance",
        "Game compatibility refused: protected or remote-access app");
    public static string Settings_CompatFilterExe => T("Applications (*.exe)", "Applications (*.exe)");
    public static string Settings_CompatPickerTitle => T("Choisir une application", "Choose an application");
}
