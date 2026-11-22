namespace AZERTYGlobal;

internal static partial class L
{
    // ── Paramètres, lot « clarté » (1.3.0, 07/10) ──────────────────────────────────────
    // Objectif d'Antoine : même l'utilisateur le moins à l'aise ne doit jamais être perdu.
    // Chaque réglage dit ce qu'il change, chaque refus dit pourquoi et quoi faire.

    /// <summary>Étiquettes des modificateurs d'un raccourci, dans l'ordre d'affichage (Ctrl, Alt, Maj/Shift).</summary>
    public static string[] SettingsClarity_ModifierTags(TypingEngine.Windows.ShortcutModifiers modifiers)
    {
        if (!TypingEngine.Windows.ShortcutModifierRules.IsValid(modifiers))
            modifiers = TypingEngine.Windows.ShortcutModifierRules.Default;
        var tags = new List<string>(3);
        if ((modifiers & TypingEngine.Windows.ShortcutModifiers.Ctrl) != 0) tags.Add("Ctrl");
        if ((modifiers & TypingEngine.Windows.ShortcutModifiers.Alt) != 0) tags.Add("Alt");
        if ((modifiers & TypingEngine.Windows.ShortcutModifiers.Shift) != 0) tags.Add(Settings_ShortcutModifier2);
        return tags.ToArray();
    }

    /// <summary>Le geste complet d'un raccourci, espaces insécables : il ne se coupe jamais.</summary>
    public static string SettingsClarity_Combo(TypingEngine.Windows.ShortcutModifiers modifiers, string key) =>
        string.Join(" + ", SettingsClarity_ModifierTags(modifiers)) + " + " + key;

    /// <summary>Même geste, forme compacte sans espaces (menu de la zone de notification).</summary>
    public static string SettingsClarity_ComboCompact(TypingEngine.Windows.ShortcutModifiers modifiers, string key) =>
        string.Join("+", SettingsClarity_ModifierTags(modifiers)) + "+" + key;

    /// <summary>Le geste complet d'un raccourci Ctrl+Maj, espaces insécables : il ne se coupe jamais.</summary>
    public static string SettingsClarity_Combo(string key) =>
        SettingsClarity_Combo(TypingEngine.Windows.ShortcutModifierRules.Default, key);

    /// <summary>Invite de capture : combinaison complète (au moins 2 modificateurs + touche).</summary>
    public static string SettingsClarity_CaptureHint => T(
        "Appuyez sur la combinaison voulue (au moins 2 touches parmi Ctrl, Maj, Alt + lettre, chiffre ou F1 à F12). Échap annule.",
        "Press the desired shortcut (at least 2 of Ctrl, Shift, Alt + a letter, digit or F1 to F12). Esc cancels.");

    /// <summary>Moins de deux modificateurs tenus lors de la capture.</summary>
    public static string SettingsClarity_ShortcutNeedModifiers => T(
        "Ajoutez au moins deux modificateurs parmi Ctrl, Maj et Alt (par exemple Ctrl + Maj ou Alt + Maj).",
        "Include at least two modifiers from Ctrl, Shift and Alt (for example Ctrl + Shift or Alt + Shift).");

    /// <summary>Win ou Alt Gr tenu lors de la capture.</summary>
    public static string SettingsClarity_ShortcutNoWinOrAltGr => T(
        "Les touches Win et Alt Gr ne peuvent pas servir dans un raccourci. Utilisez Ctrl, Maj et Alt gauche.",
        "Win and Alt Gr cannot be used in a shortcut. Use Ctrl, Shift and left Alt.");

    /// <summary>La combinaison choisie ouvre déjà l'autre fenêtre.</summary>
    public static string SettingsClarity_ShortcutTakenByKeyboard(TypingEngine.Windows.ShortcutModifiers modifiers, string key) => T(
        $"{SettingsClarity_Combo(modifiers, key)} affiche déjà le clavier virtuel. Choisissez une autre combinaison.",
        $"{SettingsClarity_Combo(modifiers, key)} already shows the virtual keyboard. Choose another shortcut.");

    public static string SettingsClarity_ShortcutTakenBySearch(TypingEngine.Windows.ShortcutModifiers modifiers, string key) => T(
        $"{SettingsClarity_Combo(modifiers, key)} ouvre déjà la recherche de caractères. Choisissez une autre combinaison.",
        $"{SettingsClarity_Combo(modifiers, key)} already opens the character search. Choose another shortcut.");

    public static string SettingsClarity_ShortcutTakenByKeyboard(string key) =>
        SettingsClarity_ShortcutTakenByKeyboard(TypingEngine.Windows.ShortcutModifierRules.Default, key);

    public static string SettingsClarity_ShortcutTakenBySearch(string key) =>
        SettingsClarity_ShortcutTakenBySearch(TypingEngine.Windows.ShortcutModifierRules.Default, key);

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

    /// <summary>Ctrl tenu seul (sans Maj) dans la case : le raccourci est toujours Ctrl + Maj +
    /// la touche, la case ne reçoit que la touche. Ctrl+A ne doit pas enregistrer « A ».</summary>
    public static string SettingsClarity_ShortcutCtrlOnly => T(
        $"Appuyez sur la touche seule, sans Ctrl : le raccourci sera toujours {SettingsClarity_Combo("touche")}.",
        $"Press the key on its own, without Ctrl: the shortcut will always be {SettingsClarity_Combo("key")}.");

    /// <summary>Retour arrière dans la case : la touche d'origine est rendue.</summary>
    public static string SettingsClarity_ShortcutRestored(string key) => T(
        $"Touche d’origine rétablie : {SettingsClarity_Combo(key)} ✓",
        $"Default key restored: {SettingsClarity_Combo(key)} ✓");

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
        "Petits messages à l’écran : bulles de la zone de notification et indicateur de Ctrl + Maj + Verr. Maj. Les avis de pause (jeux, connexion à distance) s’affichent toujours.",
        "Small pop-ups on screen: notification-area bubbles and the Ctrl + Shift + Caps Lock indicator. Pause notices (games, remote connections) always appear.");
    /// <summary>Audit 09/11, W24 : une fois les trois exercices faits, la case n'a plus d'effet (la fenêtre ne
    /// s'ouvre plus d'elle-même) ; l'aide le dit au lieu de promettre une ouverture qui n'arrive pas.</summary>
    public static string SettingsClarity_HintOnboarding =>
        ConfigManager.LearningMaxStepCompleted >= TutorialSteps.DoneThreshold
            ? SettingsClarity_HintOnboardingDone
            : SettingsClarity_HintOnboardingPending;
    public static string SettingsClarity_HintOnboardingPending => T(
        "L’accueil s’ouvre au lancement tant que ses trois premiers exercices ne sont pas faits.",
        "The welcome screen opens at launch until its first three exercises are done.");
    public static string SettingsClarity_HintOnboardingDone => T(
        "Les trois premiers exercices sont faits : l’accueil ne s’ouvre plus de lui-même au lancement. Il reste dans le menu de la zone de notification.",
        "The first three exercises are done: the welcome screen no longer opens by itself at launch. It stays in the notification-area menu.");
    public static string SettingsClarity_HintSystemLayoutWarning => T(
        "Prévient si la disposition Windows est active.",
        "Warns if the Windows layout is also active.");
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
