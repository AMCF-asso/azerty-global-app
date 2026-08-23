namespace AZERTYGlobal;

internal static partial class L
{
    // ── Erreurs de démarrage ─────────────────────────────────────────
    public static string Tray_WindowCreationError => T(
        $"Impossible de créer la fenêtre interne d’{Product}. L’application va se fermer.",
        $"{Product} couldn't create its internal window. The application will close.");
    public static string Tray_StartupError => T(
        $"{Product} n’a pas pu démarrer correctement.\n\n" +
        "Le détail technique a été enregistré dans error.log. " +
        $"Si le problème persiste, contactez le support : {ProductIdentity.Url("/soutien")}",
        $"{Product} couldn't start properly.\n\n" +
        "Technical details were recorded in error.log. " +
        $"If the problem persists, contact support: {ProductIdentity.Url("/soutien")}");

    // ── Balloons d'état ───────────────────────────────────────────────
    // ── Titres des notifications (audit du 2026-08-23) ────────────────
    // Le titre porte l'etat, le corps ne dit que l'action. Le nom du produit reste dans
    // le corps : l'attribution systeme n'est documentee qu'a partir de Windows 11, et
    // 569 des 1 604 installations du Store sont sous Windows 10.
    public static string Tray_ActiveTitle => T("Actif", "Active");
    public static string Tray_PausedTitle => T("En pause", "Paused");
    public static string Tray_DisabledTitle => T("Désactivé", "Off");
    public static string Tray_ActiveAgainTitle => T("De nouveau actif", "Active again");
    public static string Tray_PausedForDurationTitle(string durationText) =>
        T($"En pause pour {durationText}", $"Paused for {durationText}");
    public static string Tray_PauseEndedTitle => T("Pause terminée", "Pause ended");
    public static string Tray_PauseStoppedTitle => T("Pause arrêtée", "Pause stopped");
    public static string Tray_PrecautionTitle => T("En pause par précaution", "Paused as a precaution");
    public static string Tray_ToggleRefusedTitle(string proc) =>
        T($"Activation refusée dans {proc}", $"Can't turn on in {proc}");
    public static string Tray_SuspendedInTitle(string proc) =>
        T($"Suspendu dans {proc}", $"Suspended in {proc}");
    public static string Tray_SuspendedDuringTitle(string proc) =>
        T($"Suspendu pendant {proc}", $"Suspended during {proc}");
    public static string Tray_ForceRefusedTitle =>
        T("Compatibilité forcée refusée", "Can't force compatibility");
    public static string Tray_ActiveBalloonBody => T(
        $"Ctrl+Maj+Verr. Maj pour activer ou désactiver {Product}.",
        $"Ctrl+Shift+Caps Lock turns {Product} on and off.");
    public static string Tray_PausedBalloonBody => T(
        $"Ctrl+Maj+Verr. Maj ou le menu de l’icône pour reprendre {Product}.",
        $"Ctrl+Shift+Caps Lock or the icon menu resumes {Product}.");
    public static string Tray_DisabledBalloonBody => T(
        $"Ctrl+Maj+Verr. Maj pour réactiver {Product}.",
        $"Ctrl+Shift+Caps Lock turns {Product} back on.");
    public static string Tray_ActiveAgain => T(
        $"L’application qui suspendait {Product} n’est plus au premier plan.",
        $"The app that suspended {Product} is no longer in the foreground.");

    public static string Tray_GameCompatDisabledTitle => T("Compatibilité jeu désactivée", "Game compatibility disabled");
    public static string Tray_GameCompatDisabledBody(string list) => T(
        $"Ces jeux sont protégés par un anti-cheat : {list}. {Product} se mettra en pause à leur ouverture.",
        $"These games are anti-cheat protected: {list}. {Product} will pause when they open.");

    public static string Tray_ThisGameFallback => T("ce jeu", "this game");
    // Refus de bascule ET refus de compatibilite forcee : meme situation vue par
    // l'utilisateur, deux chemins de code. Le nom du process vit dans le titre.
    public static string Tray_AntiCheatRefusedBody => T(
        $"L’anti-cheat du jeu peut y voir de la triche et bannir votre compte. {Product} reste désactivé.",
        $"The game's anti-cheat could see it as cheating and ban your account. {Product} stays off.");

    public static string Tray_PausedForDuration => T(
        $"{Product} reprendra tout seul, ou par le menu de l’icône.",
        $"{Product} will resume on its own, or from the icon menu.");
    // Un seul corps pour les deux fins de pause : seul le titre les distingue.
    public static string Tray_PauseOverBody => T(
        $"{Product} est de nouveau actif.",
        $"{Product} is active again.");

    public static string Tray_SuspendedUnknownForeground => T(
        $"{Product} s’est suspendu : l’application au premier plan est inconnue.",
        $"{Product} suspended itself: the foreground app is unknown.");
    public static string Tray_DisabledForAntiCheat => T(
        $"L’anti-cheat du jeu interdit l’injection de frappes. {Product} se désactive.",
        $"The game's anti-cheat doesn't allow keystroke injection. {Product} turns itself off.");
    public static string Tray_DisabledForRemoteAccess => T(
        $"{Product} se suspend pour ne pas s’appliquer deux fois, ici et à distance.",
        $"{Product} suspends itself so it doesn't apply twice, here and remotely.");
    public static string Tray_DisabledByUserOverride => T(
        $"Votre réglage de compatibilité suspend {Product} ici.",
        $"Your compatibility setting suspends {Product} here.");
    public static string Tray_ForceOnRemoteRefused => T(
        $"{Product} reste suspendu : il s’appliquerait deux fois, ici et à distance.",
        $"{Product} stays suspended: it would apply twice, here and remotely.");
    public static string Tray_UserOverrideToggleRefused => T(
        $"Choisissez « Auto » dans Compatibilité des applications pour réactiver {Product} ici.",
        $"Choose “Auto” under App compatibility to turn {Product} back on here.");

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
    // premier : il ajoute le cadre associatif que le premier laisse de côté.
    // Aucune des deux n'annonce sa propre fin. La formule « c'est la dernière fois qu'on
    // vous le demande », décidée le 2026-08-16 pour lever la crainte du harcèlement, a été
    // retirée le 2026-08-18 au premier smoke test : annoncer un plafond fait peser la
    // demande au lieu de l'alléger. Aucune ne donne d'ordre non plus — la ligne de clic
    // est descriptive, elle dit ce que la notification ouvre au lieu de l'imposer.
    // Aucun chiffre d'usage n'y figure — décision du 2026-08-16 : les statistiques restent
    // affaire de la fenêtre « Mes statistiques », pas des notifications. Le texte reste
    // neutre : les règles du Store autorisent à solliciter un avis, pas à orienter vers
    // une note positive.
    public static string Tray_ReviewPromptTitle(int attempt) => attempt >= 2
        ? T($"Votre avis sur {Product} ?", $"Your feedback on {Product}?")
        : T($"{Product} vous plaît ?", $"Enjoying {Product}?");

    // Corps nommés par CIBLE du clic (Store ou page feedback) : depuis la v1.2.0 le
    // packagé vise toujours le Store, la page feedback ne sert plus qu'aux installations
    // hors Store, qui n'ont pas de fiche à noter.
    public static string Tray_ReviewPromptBodyStore(int attempt) => attempt >= 2
        ? T("Gratuit, open source, porté par une association. Un avis sur le Store est le plus simple pour le soutenir.\nCette notification ouvre la fenêtre de notation.",
            "Free, open source, run by a nonprofit. A review on the Store is the simplest way to support it.\nThis notification opens the rating window.")
        : T("Le projet est gratuit et open source. Un avis sur le Store est le plus simple pour le soutenir.\nCette notification ouvre la fenêtre de notation.",
            "The project is free and open source. A review on the Store is the simplest way to support it.\nThis notification opens the rating window.");

    public static string Tray_ReviewPromptBodyFeedback(int attempt) => attempt >= 2
        ? T("Gratuit, open source, porté par une association. Votre avis est le plus simple pour le soutenir.\nCette notification ouvre la page d’avis.",
            "Free, open source, run by a nonprofit. Your feedback is the simplest way to support it.\nThis notification opens the feedback page.")
        : T("Le projet est gratuit et open source. Votre avis est le plus simple pour le soutenir.\nCette notification ouvre la page d’avis.",
            "The project is free and open source. Your feedback is the simplest way to support it.\nThis notification opens the feedback page.");

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
    public static string Tray_ChallengeAnnounceTitle =>
        T("Nouveau : le Défi du jour", "New: the daily challenge");
    public static string Tray_ChallengeAnnounceBody => T(
        $"Chaque jour, un extrait à taper dans {Product}, le même pour tout le monde. Cliquez pour ouvrir.",
        $"Every day, a passage to type in {Product}, the same one for everyone. Click to open.");
    // Relance unique du lancement automatique (v1.2.0, jamais réémise).
    public static string Tray_AutoStartNudgeTitle => T(
        $"Lancer {Product} au démarrage ?",
        $"Launch {Product} at startup?");
    public static string Tray_AutoStartNudgeBody => T(
        "L’application ne démarre pas encore avec Windows. Cliquez pour l’activer.",
        "The app doesn't start with Windows yet. Click to turn it on.");
    public static string Tray_AutoStartEnabledTitle => T(
        "Lancement au démarrage activé",
        "Launch at startup enabled");
    public static string Tray_AutoStartEnabledBody => T(
        $"{Product} démarrera avec Windows. Réversible depuis le menu ou les Paramètres.",
        $"{Product} will start with Windows. You can undo this from the menu or Settings.");
    public static string Tray_MenuActiveApp(string procName) => T($"Application active : {procName}", $"Active application: {procName}");
    public static string Tray_MenuCompatAuto => T("Auto (détection automatique)", "Auto (automatic detection)");
    public static string Tray_MenuCompatForceOn => T("Forcer compatibilité jeu", "Force game compatibility");
    public static string Tray_MenuCompatForceOff => T("Forcer désactivation", "Force disable");
    public static string Tray_MenuCompatInfo => T("Comprendre la compatibilité…", "About app compatibility…");
    public static string Tray_MenuAppCompat => T("Compatibilité des applications", "App compatibility");
    public static string Tray_CompatInfoTitle => T("Compatibilité des applications", "App compatibility");
    public static string Tray_CompatInfoBody => T(
        $"{Product} adapte automatiquement son fonctionnement à l’application active.\n\n" +
        "Auto\nUtilise le mode normal, un mode spécial pour certains jeux, ou suspend l’application lorsque c’est nécessaire.\n\n" +
        "Forcer compatibilité jeu\nUtilise des combinaisons de touches natives dans l’application sélectionnée. Ce mode aide les jeux qui ignorent l’injection Unicode.\n\n" +
        $"Forcer désactivation\nSuspend {Product} uniquement dans l’application sélectionnée.\n\n" +
        $"Connexions à distance\n{Product} se suspend automatiquement lorsque Parsec, le Bureau à distance Microsoft, AnyDesk, TeamViewer ou RustDesk est au premier plan. Cela évite de transformer deux fois les frappes si {Product} fonctionne aussi sur l’ordinateur distant.\n\n" +
        "La détection reste entièrement locale. Aucun nom d’application n’est transmis.",
        $"{Product} automatically adapts its behavior to the active app.\n\n" +
        "Auto\nUses normal mode, a special mode for some games, or suspends the app when necessary.\n\n" +
        "Force game compatibility\nUses native key combinations in the selected app. This mode helps games that ignore Unicode input injection.\n\n" +
        $"Force disable\nSuspends {Product} only in the selected app.\n\n" +
        $"Remote connections\n{Product} automatically suspends itself while Parsec, Microsoft Remote Desktop, AnyDesk, TeamViewer, or RustDesk is in the foreground. This prevents keystrokes from being transformed twice when {Product} also runs on the remote computer.\n\n" +
        "Detection stays entirely local. No app names are transmitted.");
    public static string Tray_MenuAbout => T("À propos", "About");
    public static string Tray_MenuResetOnboardingDebug => T("🛠 [DEBUG] Réinitialiser onboarding", "🛠 [DEBUG] Reset onboarding");
    public static string Tray_MenuQuit => T("Quitter", "Quit");
}
