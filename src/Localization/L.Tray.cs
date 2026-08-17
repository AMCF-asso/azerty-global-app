namespace AZERTYGlobal;

internal static partial class L
{
    // ── Erreurs de démarrage ─────────────────────────────────────────
    public static string Tray_WindowCreationError => T(
        "Impossible de créer la fenêtre interne d’AZERTY Global. L’application va se fermer.",
        "AZERTY Global couldn't create its internal window. The application will close.");
    public static string Tray_StartupError => T(
        "AZERTY Global n’a pas pu démarrer correctement.\n\n" +
        "Le détail technique a été enregistré dans error.log. " +
        "Si le problème persiste, contactez le support : https://azerty.global/soutien",
        "AZERTY Global couldn't start properly.\n\n" +
        "Technical details were recorded in error.log. " +
        "If the problem persists, contact support: https://azerty.global/soutien");

    // ── Balloons d'état ───────────────────────────────────────────────
    public static string Tray_ActiveBalloonBody => T(
        "est actif.\nCtrl+Maj+Verr.Maj pour activer/désactiver.",
        "is active.\nCtrl+Shift+Caps Lock to turn on/off.");
    public static string Tray_PausedBalloonBody => T(
        "est en pause — Ctrl+Maj+Verr.Maj ou menu tray pour reprendre.",
        "is paused — Ctrl+Shift+Caps Lock or tray menu to resume.");
    public static string Tray_DisabledBalloonBody => T(
        "est désactivé — Ctrl+Maj+Verr.Maj pour réactiver.",
        "is off — Ctrl+Shift+Caps Lock to turn back on.");
    public static string Tray_ActiveAgain => T("est de nouveau actif.", "is active again.");

    public static string Tray_GameCompatDisabledTitle => T("Compatibilité jeu désactivée", "Game compatibility disabled");
    public static string Tray_GameCompatDisabledBody(string list) => T(
        $"AZERTY Global a désactivé l’option de compatibilité pour : {list}. Ces jeux sont désormais protégés par un anti-cheat. AZERTY Global se mettra automatiquement en pause quand ils seront ouverts.",
        $"AZERTY Global disabled the compatibility option for: {list}. These games are now protected by anti-cheat. AZERTY Global will automatically pause itself when they are open.");

    public static string Tray_ThisGameFallback => T("ce jeu", "this game");
    public static string Tray_AntiCheatToggleRefused(string procName) => T(
        $"AZERTY Global ne peut pas être activé pendant que {procName} tourne : son anti-cheat pourrait considérer cela comme de la triche et bannir votre compte.",
        $"AZERTY Global can't be turned on while {procName} is running: its anti-cheat could flag this as cheating and get your account banned.");

    public static string Tray_PausedForDuration(string durationText) => T($"en pause pour {durationText}.", $"paused for {durationText}.");
    public static string Tray_PauseEnded => T("pause terminée.", "pause ended.");
    public static string Tray_PauseStopped => T("pause arrêtée.", "pause stopped.");

    public static string Tray_SuspendedUnknownForeground => T(
        "mis en pause par précaution : l’application au premier plan n’a pas pu être identifiée.",
        "paused as a precaution: the foreground application couldn't be identified.");
    public static string Tray_DisabledForAntiCheat(string procName) => T(
        $"désactivé temporairement pour {procName}\n(anti-cheat : injection de frappes interdite).",
        $"temporarily disabled for {procName}\n(anti-cheat: keystroke injection not allowed).");
    public static string Tray_DisabledForRemoteAccess(string procName) => T(
        $"suspendu temporairement pendant l’utilisation de {procName} pour éviter un double remappage sur l’ordinateur distant.",
        $"temporarily suspended while using {procName} to prevent double remapping on the remote computer.");
    public static string Tray_DisabledByUserOverride(string procName) => T(
        $"suspendu dans {procName} selon votre réglage de compatibilité.",
        $"suspended in {procName} according to your compatibility setting.");
    public static string Tray_ForceOnRefused(string proc) => T(
        $"AZERTY Global ne peut pas être activé sur {proc} : son anti-cheat pourrait considérer cela comme de la triche et bannir votre compte.",
        $"AZERTY Global can't be turned on for {proc}: its anti-cheat could flag this as cheating and get your account banned.");
    public static string Tray_ForceOnRemoteRefused(string proc) => T(
        $"AZERTY Global reste suspendu sur {proc} pour éviter que les frappes soient transformées deux fois entre cet ordinateur et l’ordinateur distant.",
        $"AZERTY Global stays suspended for {proc} to prevent keystrokes from being transformed twice between this computer and the remote computer.");
    public static string Tray_UserOverrideToggleRefused(string proc) => T(
        $"AZERTY Global reste suspendu dans {proc}. Choisissez « Auto » dans Compatibilité des applications pour retirer ce réglage.",
        $"AZERTY Global stays suspended in {proc}. Choose “Auto” under App compatibility to remove this setting.");

    // ── Tooltip ────────────────────────────────────────────────────────
    public static string Tray_TooltipSuspendedCompat => T("Suspendu pour compatibilité", "Suspended for compatibility");
    public static string Tray_TooltipPaused(string remaining) => T($"En pause {remaining}", $"Paused {remaining}");
    public static string Tray_TooltipDisabled => T("Désactivé", "Off");
    public static string Tray_TooltipActive => T("Actif", "Active");
    public static string Tray_TooltipCapsLock => T("Verr. Maj.", "Caps Lock");
    public static string Tray_TooltipDeadKey(string symbol) => T($"Touche morte : {symbol}", $"Dead key: {symbol}");

    // ── Sollicitation d'avis ────────────────────────────────────────────
    // Entrée tray de bascule directe de langue : libellée dans la langue CIBLE (même
    // logique que le drapeau de la fenêtre de bienvenue) — en interface française on
    // propose « Switch to English » et inversement, d'où l'inversion volontaire des variantes.
    public static string Tray_MenuSwitchLanguage => T("Switch to English", "Passer en français");

    // Sollicitation d'avis (v1.2.0) — deux essais au maximum sur toute la vie de
    // l'installation, paramétrés par le rang de l'essai. Le second ne répète pas le
    // premier : il parle du projet et annonce sa propre fin, ce qui est vrai (plafond de
    // deux) et lève la crainte du harcèlement au moment précis où on la formule.
    // Aucun chiffre d'usage n'y figure — décision du 2026-08-16 : les statistiques restent
    // affaire de la fenêtre « Mes statistiques », pas des notifications. Le texte reste
    // neutre : les règles du Store autorisent à solliciter un avis, pas à orienter vers
    // une note positive.
    public static string Tray_ReviewPromptTitle(int attempt) => attempt >= 2
        ? T("Aider AZERTY Global", "Help AZERTY Global")
        : T("AZERTY Global vous plaît ?", "Enjoying AZERTY Global?");

    // Corps nommés par CIBLE du clic (Store ou page feedback) : depuis la v1.2.0 le
    // packagé vise toujours le Store, la page feedback ne sert plus qu'aux installations
    // hors Store, qui n'ont pas de fiche à noter.
    public static string Tray_ReviewPromptBodyStore(int attempt) => attempt >= 2
        ? T("Le projet avance grâce aux retours de ses utilisateurs. Quelques secondes sur le Store, et c’est la dernière fois qu’on vous le demande.\nCliquez sur cette notification pour noter l’app.",
            "The project moves forward thanks to user feedback. A few seconds on the Store, and we won’t ask again.\nClick this notification to rate the app.")
        : T("AZERTY Global est gratuit et open source. Un avis sur le Microsoft Store aide vraiment le projet à avancer.\nCliquez sur cette notification pour noter l’app.",
            "AZERTY Global is free and open source. A review on the Microsoft Store genuinely helps the project move forward.\nClick this notification to rate the app.");

    public static string Tray_ReviewPromptBodyFeedback(int attempt) => attempt >= 2
        ? T("Le projet avance grâce aux retours de ses utilisateurs. Quelques secondes suffisent, et c’est la dernière fois qu’on vous le demande.\nCliquez sur cette notification pour donner votre avis.",
            "The project moves forward thanks to user feedback. It only takes a moment, and we won’t ask again.\nClick this notification to share yours.")
        : T("AZERTY Global est gratuit et open source. Votre avis aide vraiment le projet à avancer.\nCliquez sur cette notification pour le donner.",
            "AZERTY Global is free and open source. Your feedback genuinely helps the project move forward.\nClick this notification to share it.");

    // ── Menu tray ────────────────────────────────────────────────────
    public static string Tray_MenuDisable => T("Désactiver\tCtrl+Maj+Verr.Maj", "Turn off\tCtrl+Shift+Caps Lock");
    public static string Tray_MenuEnable => T("Activer\tCtrl+Maj+Verr.Maj", "Turn on\tCtrl+Shift+Caps Lock");
    public static string Tray_MenuResumeNow => T("Reprendre maintenant", "Resume now");
    public static string Tray_MenuPauseEllipsis => T("Mettre en pause…", "Pause…");
    public static string Tray_MenuHideVirtualKeyboard(string key) => T($"Masquer le clavier virtuel\tCtrl+Maj+{key}", $"Hide virtual keyboard\tCtrl+Shift+{key}");
    public static string Tray_MenuVirtualKeyboard(string key) => T($"Clavier virtuel\tCtrl+Maj+{key}", $"Virtual keyboard\tCtrl+Shift+{key}");
    public static string Tray_MenuSearchCharacter(string key) => T($"Rechercher un caractère\tCtrl+Maj+{key}", $"Find a character\tCtrl+Shift+{key}");
    public static string Tray_MenuLessons => T("Leçons", "Lessons");
    public static string Tray_MenuWelcomeWindow => T("Fenêtre de bienvenue", "Welcome window");
    public static string Tray_MenuPrivacySecurity => T("Confidentialité && sécurité", "Privacy && security");
    public static string Tray_MenuPrintableGuide => T("Guide utilisateur imprimable", "Printable user guide");
    public static string Tray_MenuFiveChanges => T("Les 5 changements", "The 5 changes");
    public static string Tray_MenuKeyboardCards => T("Cartes du clavier", "Keyboard cards");
    public static string Tray_MenuWhatsNew => T("Nouveautés de la version", "What's new in this version");
    public static string Tray_MenuResources => T("Ressources", "Resources");
    public static string Tray_MenuRateStore => T("Noter sur le Microsoft Store", "Rate on the Microsoft Store");
    public static string Tray_MenuSupportProject => T("Soutenir le projet", "Support the project");
    public static string Tray_MenuGiveFeedback => T("Donner mon avis", "Give feedback");
    public static string Tray_MenuReportBug => T("Signaler un bug", "Report a bug");
    public static string Tray_MenuFeedbackSupport => T("Retours et soutien", "Feedback && support");
    // Lancement automatique : affordance permanente, cochée quand la tâche est
    // enregistrée. Seul chemin visible pour qui a fermé l'accueil avant l'étape 3.
    public static string Tray_MenuAutoStart => T("Lancer au démarrage de Windows", "Launch at Windows startup");
    public static string Tray_MenuSettings => T("Paramètres", "Settings");
    // Annonce unique du Défi du jour aux utilisateurs existants (v1.2.0, jamais réémise).
    public static string Tray_ChallengeAnnounceTitle => T("Nouveau : Défi du jour", "New: Daily challenge");
    public static string Tray_ChallengeAnnounceBody => T(
        "Un extrait à taper chaque jour, le même pour tout le monde. Cliquez pour ouvrir la séance du jour.",
        "A passage to type every day, the same one for everyone. Click to open today’s session.");
    // Relance unique du lancement automatique (v1.2.0, jamais réémise).
    public static string Tray_AutoStartNudgeTitle => T(
        "Lancer AZERTY Global au démarrage ?",
        "Launch AZERTY Global at startup?");
    public static string Tray_AutoStartNudgeBody => T(
        "L’application ne démarre pas encore avec Windows : il faut la lancer à la main à chaque fois. Cliquez pour l’activer.",
        "The app doesn’t start with Windows yet: you have to launch it manually every time. Click to turn it on.");
    public static string Tray_AutoStartEnabledTitle => T(
        "Lancement au démarrage activé",
        "Launch at startup enabled");
    public static string Tray_AutoStartEnabledBody => T(
        "AZERTY Global démarrera désormais avec Windows. Vous pouvez revenir dessus depuis le menu ou les Paramètres.",
        "AZERTY Global will now start with Windows. You can undo this from the menu or in Settings.");
    public static string Tray_MenuActiveApp(string procName) => T($"Application active : {procName}", $"Active application: {procName}");
    public static string Tray_MenuCompatAuto => T("Auto (détection automatique)", "Auto (automatic detection)");
    public static string Tray_MenuCompatForceOn => T("Forcer compatibilité jeu", "Force game compatibility");
    public static string Tray_MenuCompatForceOff => T("Forcer désactivation", "Force disable");
    public static string Tray_MenuCompatInfo => T("Comprendre la compatibilité…", "About app compatibility…");
    public static string Tray_MenuAppCompat => T("Compatibilité des applications", "App compatibility");
    public static string Tray_CompatInfoTitle => T("Compatibilité des applications", "App compatibility");
    public static string Tray_CompatInfoBody => T(
        "AZERTY Global adapte automatiquement son fonctionnement à l’application active.\n\n" +
        "Auto\nUtilise le mode normal, un mode spécial pour certains jeux, ou suspend l’application lorsque c’est nécessaire.\n\n" +
        "Forcer compatibilité jeu\nUtilise des combinaisons de touches natives dans l’application sélectionnée. Ce mode aide les jeux qui ignorent l’injection Unicode.\n\n" +
        "Forcer désactivation\nSuspend AZERTY Global uniquement dans l’application sélectionnée.\n\n" +
        "Connexions à distance\nAZERTY Global se suspend automatiquement lorsque Parsec, le Bureau à distance Microsoft, AnyDesk, TeamViewer ou RustDesk est au premier plan. Cela évite de transformer deux fois les frappes si AZERTY Global fonctionne aussi sur l’ordinateur distant.\n\n" +
        "La détection reste entièrement locale. Aucun nom d’application n’est transmis.",
        "AZERTY Global automatically adapts its behavior to the active app.\n\n" +
        "Auto\nUses normal mode, a special mode for some games, or suspends the app when necessary.\n\n" +
        "Force game compatibility\nUses native key combinations in the selected app. This mode helps games that ignore Unicode input injection.\n\n" +
        "Force disable\nSuspends AZERTY Global only in the selected app.\n\n" +
        "Remote connections\nAZERTY Global automatically suspends itself while Parsec, Microsoft Remote Desktop, AnyDesk, TeamViewer, or RustDesk is in the foreground. This prevents keystrokes from being transformed twice when AZERTY Global also runs on the remote computer.\n\n" +
        "Detection stays entirely local. No app names are transmitted.");
    public static string Tray_MenuAbout => T("À propos", "About");
    public static string Tray_MenuResetOnboardingDebug => T("🛠 [DEBUG] Réinitialiser onboarding", "🛠 [DEBUG] Reset onboarding");
    public static string Tray_MenuQuit => T("Quitter", "Quit");
}
