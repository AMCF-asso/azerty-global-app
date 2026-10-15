namespace AZERTYGlobal;

internal static partial class L
{
    // ── Navigation ─────────────────────────────────────────────────
    public static string Onboarding_Next => T("Suivant", "Next");
    public static string Onboarding_Prev => T("Précédent", "Previous");
    public static string Onboarding_TryNow => T("Essayer maintenant", "Try it now");
    public static string Onboarding_ActivateAndTry => T("Activer et essayer", "Turn on and try");
    public static string Onboarding_Activate => T($"Activer {Product}", $"Turn on {Product}");
    public static string Onboarding_InactiveSubtitle => T("Activez votre AZERTY amélioré.", "Turn on your improved AZERTY.");
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
    public static string Onboarding_SectionResources => T("Ressources et communauté", "Resources & community");

    // ── En-tête ────────────────────────────────────────────────────
    public static string Onboarding_Subtitle => T("Votre AZERTY est maintenant amélioré.", "Your AZERTY is now improved.");

    // ── Étape 1 — les 5 améliorations ─────────────────────────────────
    public static string Onboarding_Step1Title => T("5 changements, 99 % des frappes préservées", "5 changes, 99% of keystrokes preserved");
    public static string Onboarding_Feature1Title => T("Verrouillage majuscule intelligent", "Smart Caps Lock");
    public static string Onboarding_Feature2Title => T("Le point sans Majuscule", "Direct period access");
    public static string Onboarding_Feature2Desc => T("Le point et le point-virgule échangent leurs places.", "The period and semicolon swap places.");
    public static string Onboarding_Feature3Title => T("Arobase et dièse en haut à gauche", "@ and # on the top-left key");
    public static string Onboarding_Feature3Desc => T("Accès direct sans AltGr.", "Direct access without AltGr.");
    public static string Onboarding_Feature1Prefix => T("Verr. Maj. + ", "Caps Lock + ");
    // Insécables dans « sur la rangée de repos » : à l'étape 1, le titre passe à la ligne
    // après « programmation » plutôt que de laisser « repos » seul.
    public static string Onboarding_Feature4Title => T("Symboles de programmation sur la rangée de repos", "Programming symbols on the home row");
    public static string Onboarding_Feature4DescSuffix => T(" avec AltGr.", " with AltGr.");
    public static string Onboarding_Feature5Title => T("Accents internationaux sur la touche ù", "International accents on the ù key");
    public static string Onboarding_Feature5Desc => T(
        "Accent aigu, accent grave et tilde, en touches mortes.",
        "Acute accent, grave accent and tilde, as dead keys.");
    // Refonte du 15/10 (textes validés par Antoine) : le clavier et ses cinq notes au trait.
    public static string Onboarding_Step1Subtitle => T(
        "Les nouveautés sont en bleu ; vos lettres restent à leur place.",
        "What’s new is in blue; your letters stay where they are.");
    public static string Onboarding_NoteAt => T("sans AltGr (# avec Maj)", "without AltGr (# with Shift)");
    public static string Onboarding_NoteCaps => T("Verr. Maj., puis é è ç à", "Caps Lock, then é è ç à");
    public static string Onboarding_NoteCoding => T("Programmation facilitée : AltGr + D à K", "Easier coding: AltGr + D to K");
    public static string Onboarding_NotePeriod => T("le point, sans Maj", "the period, without Shift");
    public static string Onboarding_NoteAccents => T("puis la lettre ; ù : AltGr + u", "then the letter; ù: AltGr + u");
    // « Aucune frappe enregistrée » serait faux : l'app garde des compteurs locaux agrégés (UsageStats).
    public static string Onboarding_PrivacyReassurance => T(
        "Tout se passe sur votre ordinateur : ce que vous tapez n’est ni enregistré ni envoyé.",
        "Everything happens on your computer: what you type is never recorded or sent.");
    public static string Onboarding_TurnOffAnyTime => T("Vous pouvez le désactiver à tout moment.", "You can turn it off at any time.");

    // ── Étape 2 — comment utiliser ────────────────────────────────────
    public static string Onboarding_Step2Title => T($"Comment utiliser {Product}", $"How to use {Product}");
    public static string Onboarding_Card1Title => T("L’icône AG est dans la zone de notification", "The AG icon is in the notification area");
    public static string Onboarding_Card1Desc => T(
        "Près de l’horloge (parfois sous ^). Elle indique si l’AZERTY amélioré est actif. Clic droit pour le menu.",
        "Near the clock (sometimes under ^). It shows whether the improved AZERTY is on. Right-click for the menu.");
    public static string Onboarding_Card2Title => T("Activez / désactivez à tout moment", "Turn on / off at any time");
    public static string Onboarding_Card2ShortcutPrefix => T("Raccourci : ", "Shortcut: ");
    public static string Onboarding_CapsLockWord => T("Verr. Maj.", "Caps Lock");
    public static string Onboarding_Card3Title => T("Explorez avec le clavier virtuel", "Explore with the virtual keyboard");
    public static string Onboarding_Card3Suffix => T(
        " pour voir tous les caractères disponibles.",
        " to see every available character.");
    public static string Onboarding_Card4Title => T("Recherchez n’importe quel caractère", "Search for any character");
    public static string Onboarding_Card4Suffix => T(
        " puis tapez le nom d’un caractère : Entrée l’insère dans votre texte et le clavier virtuel montre comment le taper.",
        " then type a character's name: Enter inserts it into your text and the virtual keyboard shows how to type it.");

    // ── Progression (4 étapes depuis la direction C, 08/10) ──────────
    public static string Onboarding_StepOf(int step, int count) => T($"Étape {step} sur {count}", $"Step {step} of {count}");

    // ── Étape 2 — écrire le peul, le wolof, le bambara (direction C, 08/10) ──
    public static string Onboarding_LanguagesTitle => T("Écrire le peul, le wolof, le bambara", "Write Fula, Wolof and Bambara");
    public static string Onboarding_LanguagesSubtitle => T(
        "Leurs lettres sont sur le clavier, en deux frappes au plus.",
        "Their letters are on the keyboard, two keystrokes at most.");
    public static string Onboarding_LangFula => T("Peul", "Fula");
    public static string Onboarding_LangWolof => "Wolof";
    public static string Onboarding_LangBambara => "Bambara";
    /// <summary>Une langue pour le lecteur d'écran : « Peul : ɓ ɗ ƴ ñ ŋ. ɓ = Frappez ù… ».</summary>
    public static string Onboarding_LanguageSummary(string name, string letters, string example, string gesture) => T(
        $"{name} : {letters}. {example} = {gesture}",
        $"{name}: {letters}. {example} = {gesture}");
    // L'essai : « Essayez ici : frappez ù (´), puis b. » ; ù et b en gras.
    public static string Onboarding_TrialPrefix => T("Essayez ici : frappez ", "Try it here: press ");
    public static string Onboarding_TrialDeadKey => "ù (´)";
    public static string Onboarding_TrialThen => T(", puis ", ", then ");
    public static string Onboarding_TrialLetter => "b";
    public static string Onboarding_TrialSuffix => ".";
    /// <summary>Nom accessible du champ d'essai.</summary>
    public static string Onboarding_TrialFieldName => T(
        "Essayez ici : frappez ù, puis b.",
        "Try it here: press ù, then b.");
    public static string Onboarding_TrialMatch(string letter) => T($"C’est bien {letter}.", $"That’s {letter}.");
    public static string Onboarding_TrialOther => T(
        "Ce n’est pas ɓ. Réessayez : ù, puis b.",
        "That isn’t ɓ. Try again: ù, then b.");
    // Astuce : « Une autre lettre : Ctrl + Maj + W, puis tapez « peul » ou « bambara ». »
    public static string Onboarding_LanguagesTipPrefix => T("Une autre lettre : ", "Another letter: ");
    public static string Onboarding_LanguagesTipSuffix => T(
        ", puis tapez « peul » ou « bambara ».",
        ", then type “Fula” or “Bambara”.");
}
