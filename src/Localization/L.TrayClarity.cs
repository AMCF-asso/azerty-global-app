namespace AZERTYGlobal;

// ── Zone de notification : « pourquoi et quoi faire » (lot clarté du 2026-10-07) ──
// Chaque bulle de pause, de refus ou de désactivation dit trois choses : ce qui se passe
// (le titre, inchangé), pourquoi, et ce que l'utilisateur peut faire, y compris « rien ».
// Ces chaînes remplacent, à l'appel, celles de L.Tray.cs qui taisaient l'un des trois.
//
// Bornes : Windows coupe le titre à 63 caractères et le corps à 255 (NOTIFYICONDATAW,
// szInfoTitle[64] et szInfo[256], SizeConst de Win32.cs) et recommande 48 et 200 pour
// laisser la place à la traduction. L'infobulle s'arrête à 127 (szTip[128]).
// TrayClarityTests tient ces bornes, en FR comme en EN.
//
// « Tout redevient comme avant » et non « tout reprend » : en sortant de l'application,
// le hook retrouve l'état choisi par l'utilisateur (actif, en pause ou désactivé), pas
// forcément l'état actif (TrayApplication.OnForegroundChanged, cas Leave).
internal static partial class L
{
    // ── Suspensions annoncées à l'entrée dans l'application ─────────
    public static string TrayClarity_AntiCheatSuspendedBody => T(
        $"Son anti-triche pourrait prendre {Product} pour de la triche et bannir votre compte. Rien à faire : tout redevient comme avant quand vous changez de fenêtre.",
        $"Its anti-cheat could mistake {Product} for cheating and ban your account. Nothing to do: everything returns to normal when you switch windows.");

    public static string TrayClarity_RemoteSuspendedBody => T(
        "En connexion à distance, vos touches risqueraient d’être transformées deux fois. Rien à faire : tout redevient comme avant quand vous changez de fenêtre.",
        "In a remote session, your keys could be transformed twice. Nothing to do: everything returns to normal when you switch windows.");

    public static string TrayClarity_UnknownSuspendedBody => T(
        $"{Product} ne reconnaît pas la fenêtre active et s’arrête au cas où ce serait un jeu protégé. Rien à faire : tout redevient comme avant quand vous changez de fenêtre.",
        $"{Product} can’t identify the active window and stops in case it is a protected game. Nothing to do: everything returns to normal when you switch windows.");

    public static string TrayClarity_UserOverrideSuspendedBody => T(
        "Vous avez choisi « Forcer la désactivation » pour cette application. Pour annuler : menu de l’icône › Compatibilité des applications › Auto.",
        "You chose “Force off” for this app. To undo it: icon menu › App compatibility › Auto.");

    // ── Activation refusée (raccourci ou menu « Activer ») ───────────
    // _enabled est faux sur ce chemin : en quittant l'application, rien ne se réactive
    // seul. D'où « réactivez » plutôt que « rien à faire ».
    public static string TrayClarity_AntiCheatToggleRefusedBody => T(
        $"Son anti-triche pourrait prendre {Product} pour de la triche et bannir votre compte. Réactivez-le une fois sorti du jeu.",
        $"Its anti-cheat could mistake {Product} for cheating and ban your account. Turn it back on once you leave the game.");

    public static string TrayClarity_RemoteToggleRefusedBody => T(
        $"En connexion à distance, vos touches risqueraient d’être transformées deux fois. Réactivez {Product} dans une autre fenêtre.",
        $"In a remote session, your keys could be transformed twice. Turn {Product} back on in another window.");

    public static string TrayClarity_UnknownToggleRefusedBody => T(
        $"{Product} ne reconnaît pas la fenêtre active : au cas où ce serait un jeu protégé, il reste arrêté. Réactivez-le dans une autre fenêtre.",
        $"{Product} can’t identify the active window: in case it is a protected game, it stays stopped. Turn it back on in another window.");

    public static string TrayClarity_UserOverrideToggleRefusedBody => T(
        "Vous avez choisi « Forcer la désactivation » ici. Pour l’activer : menu de l’icône › Compatibilité des applications › Auto, puis Ctrl + Maj + Verr. Maj.",
        "You chose “Force off” for this app. To turn it on: icon menu › App compatibility › Auto, then Ctrl + Shift + Caps Lock.");

    // ── « Forcer la compatibilité jeu » refusé (menu Compatibilité) ──
    // ApplyCompatibilityOverride sort sans écrire : le réglage précédent reste en place.
    public static string TrayClarity_AntiCheatForceRefusedBody => T(
        $"Son anti-triche pourrait prendre {Product} pour de la triche et bannir votre compte. Votre réglage n’a pas changé.",
        $"Its anti-cheat could mistake {Product} for cheating and ban your account. Your setting hasn’t changed.");

    public static string TrayClarity_RemoteForceRefusedBody => T(
        "En connexion à distance, vos touches risqueraient d’être transformées deux fois. Votre réglage n’a pas changé.",
        "In a remote session, your keys could be transformed twice. Your setting hasn’t changed.");

    // ── Réglage forcé retiré au démarrage ────────────────────────────
    public static string TrayClarity_GameCompatRemovedTitle =>
        T("Compatibilité jeu forcée retirée", "Forced game compatibility removed");
    public static string TrayClarity_GameCompatRemovedBody(string list) => T(
        $"Ces jeux ont un anti-triche qui pourrait bannir votre compte : {list}. {Product} s’y mettra en pause tout seul. Rien à faire.",
        $"These games have anti-cheat that could ban your account: {list}. {Product} will pause in them on its own. Nothing to do.");

    // ── Raccourci d'outil pendant une pause ou une suspension ────────
    public static string TrayClarity_PausedByYouBody => T(
        $"Vous avez mis {Product} en pause. Pour reprendre : Ctrl + Maj + Verr. Maj. ou le menu de l’icône.",
        $"You paused {Product}. To resume: Ctrl + Shift + Caps Lock or the icon menu.");

    public static string TrayClarity_SearchSecureBody => T(
        "Par sécurité, la recherche est coupée dans les champs de mot de passe. Elle revient dès que vous en sortez.",
        "For security, search is off in password fields. It comes back as soon as you leave the field.");

    // ── Infobulle de l'icône et ligne d'état du menu ─────────────────
    // Le pourquoi en quelques mots : l'infobulle est coupée à 127 caractères, nom et
    // version compris.
    public static string TrayClarity_ReasonAntiCheat => T("En pause : jeu avec anti-triche", "Paused: anti-cheat game");
    public static string TrayClarity_ReasonRemote => T("En pause : connexion à distance", "Paused: remote session");
    public static string TrayClarity_ReasonUserOverride => T("En pause ici, selon votre réglage", "Paused here by your setting");
    public static string TrayClarity_ReasonUnknown => T("En pause : fenêtre non reconnue", "Paused: unrecognized window");
    public static string TrayClarity_TooltipPausedByYou(string remaining) =>
        T($"En pause à votre demande, reprise dans {remaining}", $"Paused by you, resumes in {remaining}");
    public static string TrayClarity_TooltipDisabled =>
        T("Désactivé (Ctrl + Maj + Verr. Maj. pour réactiver)", "Off (Ctrl + Shift + Caps Lock to turn on)");
}
