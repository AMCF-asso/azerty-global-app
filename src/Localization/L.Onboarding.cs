namespace AZERTYGlobal;

internal static partial class L
{
    // ── Navigation ─────────────────────────────────────────────────
    public static string Onboarding_Next => T("Suivant", "Next");
    public static string Onboarding_Prev => T("Précédent", "Previous");
    public static string Onboarding_TryNow => T("Essayer maintenant", "Try it now");
    public static string Onboarding_LetsGo => T("C’est parti !", "Let's go!");

    // ── Étape 3 — liens et préférences ────────────────────────────────
    public static string Onboarding_LinkLessons => T("Continuer avec les leçons", "Continue with the lessons");
    public static string Onboarding_LinkGuide => T("Guide de prise en main", "Getting started guide");
    public static string Onboarding_LinkDiscord => T("Échanger avec les autres utilisateurs", "Chat with other users");
    public static string Onboarding_ChkAutoStart => T("Lancer au démarrage de Windows (recommandé)", "Launch at Windows startup (recommended)");
    public static string Onboarding_ChkDontShow => T("Ne plus afficher cet écran au démarrage", "Don't show this screen again at startup");
    // Opt-in Défi du jour (v1.2.0) — même préférence que IDC_CHK_TRAINING dans SettingsWindow.cs
    // (ConfigManager.TrainingEnabled), décochée par défaut, appliquée immédiatement au clic.
    public static string Onboarding_ChkTraining => T("Rappels d’entraînement « Défi du jour »", "\"Daily challenge\" training reminders");
    public static string Onboarding_ChkTrainingDesc => T(
        "Une séance courte par jour, proposée par une notification discrète. S’arrête automatiquement si vous n’y répondez pas.",
        "One short daily session, offered through a discreet notification. Stops automatically if you don't respond.");
    public static string Onboarding_SectionResources => T("Ressources & communauté", "Resources & community");

    // ── En-tête ────────────────────────────────────────────────────
    public static string Onboarding_Subtitle => T("Votre clavier est maintenant amélioré.", "Your keyboard is now improved.");

    // ── Étape 1 — les 5 améliorations ─────────────────────────────────
    public static string Onboarding_Step1Title => T("5 améliorations, 99 % de vos habitudes préservées", "5 improvements, 99% of your habits preserved");
    public static string Onboarding_Feature1Title => T("Verrouillage Majuscule intelligent", "Smart Caps Lock");
    public static string Onboarding_Feature2Title => T("Point en accès direct", "Direct period access");
    public static string Onboarding_Feature2Desc => T("Le point et le point-virgule échangent leurs places.", "The period and semicolon swap places.");
    public static string Onboarding_Feature3Title => T("@ et # sur la touche en haut à gauche", "@ and # on the top-left key");
    public static string Onboarding_Feature3Desc => T("Accès direct sans AltGr.", "Direct access without AltGr.");
    public static string Onboarding_Feature1Prefix => T("Verr. Maj. + ", "Caps Lock + ");
    public static string Onboarding_Feature4Title => T("Symboles de programmation accessibles", "Accessible programming symbols");
    public static string Onboarding_Feature4DescSuffix => T(" sur la rangée de repos avec AltGr.", " on the home row with AltGr.");
    public static string Onboarding_Feature5Title => T("Accents internationaux", "International accents");
    public static string Onboarding_Feature5Desc => T(
        "Accents aigu, grave et tilde sur la touche à droite du M.",
        "Acute, grave and tilde accents on the key to the right of M.");
    public static string Onboarding_PrivacyReassurance => T(
        "Cette application améliore votre clavier. Aucune frappe n’est enregistrée ni transmise.",
        "This application improves your keyboard. No keystroke is ever logged or transmitted.");

    // ── Étape 2 — comment utiliser ────────────────────────────────────
    public static string Onboarding_Step2Title => T($"Comment utiliser {Product}", $"How to use {Product}");
    public static string Onboarding_Card1Title => T("L’icône AG est dans la barre des tâches", "The AG icon is in the taskbar");
    public static string Onboarding_Card1Desc => T(
        "Elle indique si le remapping est actif. Clic droit pour accéder aux options.",
        "It shows whether remapping is active. Right-click to access the options.");
    public static string Onboarding_Card2Title => T("Activez / désactivez à tout moment", "Turn on / off at any time");
    public static string Onboarding_Card2ShortcutPrefix => T("Raccourci : ", "Shortcut: ");
    public static string Onboarding_CapsLockWord => T("Verr. Maj.", "Caps Lock");
    public static string Onboarding_Card3Title => T("Explorez avec le clavier virtuel", "Explore with the virtual keyboard");
    public static string Onboarding_Card3Suffix => T(
        " pour voir tous les caractères disponibles.",
        " to see every available character.");
    public static string Onboarding_Card4Title => T("Recherchez n’importe quel caractère", "Search for any character");
    public static string Onboarding_Card4Suffix => T(
        " puis tapez le nom d’un caractère pour le copier et voir comment le taper sur le clavier virtuel.",
        " then type a character's name to copy it and see how to type it on the virtual keyboard.");
}
