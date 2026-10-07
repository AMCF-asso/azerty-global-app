namespace AZERTYGlobal;

internal static partial class L
{
    // ── Clavier virtuel « couche active » (refonte 1.3.0, maquette A du 07/10) ──
    // Le bandeau dit ce que montre le clavier ; les mots en gras (modificateurs, signe de la
    // touche morte) sont posés par VirtualKeyboard entre ces morceaux.

    /// <summary>Barre de titre : « AZERTY Global, Clavier virtuel ».</summary>
    public static string VirtualKeyboardPaper_Title => T("Clavier virtuel", "Virtual keyboard");

    /// <summary>Rien n'est tenu : « Chaque touche montre ce qu'elle tape. Tenez **Maj** ou **AltGr**… »</summary>
    public static string VirtualKeyboardPaper_IdleLead =>
        T("Chaque touche montre ce qu’elle tape. Tenez ", "Each key shows what it types. Hold ");
    public static string VirtualKeyboardPaper_Or => T(" ou ", " or ");
    public static string VirtualKeyboardPaper_IdleTail =>
        T(" pour voir les autres signes.", " to see the other characters.");

    /// <summary>Après « **AltGr** » : un modificateur tenu, puis le nom répété en fin de phrase.</summary>
    public static string VirtualKeyboardPaper_HeldOne =>
        T(" tenu : chaque touche montre ce qu’elle tape. Les touches effacées ne tapent rien avec ",
          " held: each key shows what it types. Faded keys type nothing with ");
    /// <summary>Après « **Maj** et **AltGr** » : deux modificateurs tenus.</summary>
    public static string VirtualKeyboardPaper_HeldMany =>
        T(" tenus : chaque touche montre ce qu’elle tape. Les touches effacées ne tapent rien avec ",
          " held: each key shows what it types. Faded keys type nothing with ");

    /// <summary>Après « **Verr. Maj.** », seul : le verrou est enclenché.</summary>
    public static string VirtualKeyboardPaper_CapsOn =>
        T(" activé : chaque touche montre ce qu’elle tape.", " is on: each key shows what it types.");

    /// <summary>Après « **Ctrl** » ou « **Alt** » : les touches servent de raccourcis.</summary>
    public static string VirtualKeyboardPaper_ShortcutHeld =>
        T(" tenu : les touches servent de raccourcis, elles ne tapent aucun signe.",
          " held: keys act as shortcuts and type no character.");

    /// <summary>Touche morte en attente : « La touche morte **´** (accent aigu) attend la touche suivante… »</summary>
    public static string VirtualKeyboardPaper_DeadKeyLead => T("La touche morte ", "The dead key ");
    public static string VirtualKeyboardPaper_DeadKeyWaits(string name) =>
        T($" ({name}) attend la touche suivante : chaque touche montre ce qu’elle donnera.",
          $" ({name}) waits for the next key: each key shows what it will give.");
    /// <summary>Ce qu'écrit Espace après la touche morte : « Espace écrit ´ seul. »</summary>
    public static string VirtualKeyboardPaper_DeadKeySpace(string sign) =>
        T($" Espace écrit {sign} seul.", $" Space types {sign} on its own.");
}
