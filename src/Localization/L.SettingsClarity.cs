namespace AZERTYGlobal;

internal static partial class L
{
    // ── Paramètres, lot « clarté » (1.3.0, 07/10) ──────────────────────────────────────
    // Objectif d'Antoine : même l'utilisateur le moins à l'aise ne doit jamais être perdu.
    // Chaque réglage dit ce qu'il change, chaque refus dit pourquoi et quoi faire.

    /// <summary>Le geste complet d'un raccourci, espaces insécables : il ne se coupe jamais.</summary>
    public static string SettingsClarity_Combo(string key) =>
        $"Ctrl + {Settings_ShortcutModifier2} + {key}";

    /// <summary>Invite de capture : les touches acceptées sont celles de
    /// ConfigManager.IsShortcutAllowedVk (F1-F12, 0-9, A-Z sauf les lettres réservées).</summary>
    public static string SettingsClarity_CaptureHint => T(
        "Appuyez sur une lettre, un chiffre ou F1 à F12 (quelques lettres sont déjà prises). Échap annule.",
        "Press a letter, a digit or F1 to F12 (a few letters are taken). Esc cancels.");

    /// <summary>La touche choisie ouvre déjà l'autre fenêtre.</summary>
    public static string SettingsClarity_ShortcutTakenByKeyboard(string key) => T(
        $"{SettingsClarity_Combo(key)} affiche déjà le clavier virtuel. Choisissez une autre touche.",
        $"{SettingsClarity_Combo(key)} already shows the virtual keyboard. Choose another key.");

    public static string SettingsClarity_ShortcutTakenBySearch(string key) => T(
        $"{SettingsClarity_Combo(key)} ouvre déjà la recherche de caractères. Choisissez une autre touche.",
        $"{SettingsClarity_Combo(key)} already opens the character search. Choose another key.");

    /// <summary>Lettre réservée : ce que fait déjà Ctrl+Maj+lettre ailleurs. Les usages
    /// reprennent les commentaires de ConfigManager.BlockedLetters.</summary>
    public static string SettingsClarity_ShortcutReservedLetter(char letter)
    {
        string combo = SettingsClarity_Combo(letter.ToString());
        string? use = SettingsClarity_ReservedUse(letter);
        if (use == null)
            return T($"{combo} sert déjà dans d’autres applications. Choisissez une autre touche.",
                $"{combo} is already used by other apps. Choose another key.");
        return T($"{combo} {use}. Choisissez une autre touche.", $"{combo} {use}. Choose another key.");
    }

    /// <summary>Ce que fait Ctrl+Maj+lettre dans les applications courantes ; null pour une
    /// lettre que rien ne réserve.</summary>
    public static string? SettingsClarity_ReservedUse(char letter) => char.ToUpperInvariant(letter) switch
    {
        'T' => T("rouvre l’onglet fermé dans les navigateurs", "reopens a closed tab in web browsers"),
        'N' => T("ouvre une fenêtre privée dans les navigateurs", "opens a private window in web browsers"),
        'V' => T("colle sans mise en forme dans beaucoup d’applications", "pastes without formatting in many apps"),
        'P' => T("ouvre la palette de commandes des éditeurs de code", "opens the command palette in code editors"),
        'I' => T("ouvre les outils de développement des navigateurs", "opens developer tools in web browsers"),
        'S' => T("lance « Enregistrer sous » dans Word, Excel ou Photoshop",
                 "runs “Save As” in Word, Excel or Photoshop"),
        'Z' => T("rétablit l’action annulée dans Photoshop, Figma ou Google Docs",
                 "redoes the last undo in Photoshop, Figma or Google Docs"),
        'F' => T("cherche dans les fichiers des éditeurs de code", "searches across files in code editors"),
        'C' => T("copie dans un terminal", "copies in a terminal"),
        'B' => T("affiche la barre de favoris de Chrome et Edge", "shows the bookmarks bar in Chrome and Edge"),
        'R' => T("recharge entièrement la page dans les navigateurs", "fully reloads the page in web browsers"),
        _ => null,
    };

    /// <summary>Chiffre du pavé numérique : seuls ceux de la rangée du haut sont acceptés.</summary>
    public static string SettingsClarity_ShortcutKeypadDigit => T(
        "Les chiffres du pavé numérique ne peuvent pas servir. Utilisez ceux de la rangée du haut.",
        "Numeric keypad digits can’t be used. Use the ones in the top row.");

    /// <summary>Toute autre touche (flèches, Entrée, ponctuation…).</summary>
    public static string SettingsClarity_ShortcutOtherKey => T(
        "Cette touche ne peut pas servir. Choisissez une lettre, un chiffre ou F1 à F12.",
        "This key can’t be used. Choose a letter, a digit or F1 to F12.");

    // Sous-titres des réglages : ce que l'on constate, pas le mécanisme.
    public static string SettingsClarity_HintKeyboardShortcut => T(
        "Affiche ou masque le clavier à l’écran.",
        "Shows or hides the on-screen keyboard.");
    public static string SettingsClarity_HintSearchShortcut => T(
        $"Ouvre ou ferme la fenêtre « {Search_WindowTitle} ».",
        $"Opens or closes the “{Search_WindowTitle}” window.");
    public static string SettingsClarity_HintAutoStart => T(
        $"{Product} s’ouvre tout seul quand vous ouvrez votre session Windows.",
        $"{Product} opens by itself when you sign in to Windows.");
    public static string SettingsClarity_HintNotifications => T(
        "Petits messages en bas à droite de l’écran. Les avis de pause (jeux, connexion à distance) s’affichent toujours.",
        "Small pop-ups at the bottom right of the screen. Pause notices (games, remote connections) always appear.");
    public static string SettingsClarity_HintOnboarding => T(
        "La fenêtre de bienvenue s’ouvre au lancement tant que ses trois premiers exercices ne sont pas faits.",
        "The welcome window opens at launch until its first three exercises are done.");
    public static string SettingsClarity_HintResetWindow => T(
        "La replace au centre de l’écran, utile si elle est sortie de la vue.",
        "Moves it back to the center of the screen, handy if it went off-screen.");

    // Refus de la compatibilité jeu : pourquoi, puis ce qui se passe à la place.
    public static string SettingsClarity_CompatRefusedAntiCheat => T(
        $"Refusé : l’anti-triche de ce jeu pourrait bannir votre compte. Dans ce jeu, {Product} reste suspendu et le clavier Windows prend le relais.",
        $"Refused: this game’s anti-cheat could ban your account. In this game, {Product} stays suspended and the Windows keyboard takes over.");
    public static string SettingsClarity_CompatRefusedRemote => T(
        $"Refusé : si {Product} tourne aussi sur l’ordinateur distant, chaque frappe serait transformée deux fois. Dans cette application, {Product} reste suspendu et le clavier Windows prend le relais.",
        $"Refused: if {Product} also runs on the remote computer, each keystroke would be changed twice. In this app, {Product} stays suspended and the Windows keyboard takes over.");
}
