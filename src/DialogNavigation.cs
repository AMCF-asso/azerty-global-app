namespace AZERTYGlobal;

/// <summary>
/// AG130-40 — la circulation au clavier entre contrôles, fenêtre par fenêtre.
///
/// La boucle de messages était nue : <c>GetMessage</c> → <c>TranslateMessage</c> →
/// <c>DispatchMessage</c>, sans <c>IsDialogMessageW</c>. Les <c>WS_TABSTOP</c> étaient donc
/// inertes partout où la fenêtre ne traitait pas <c>VK_TAB</c> à la main, et un
/// <c>BS_DEFPUSHBUTTON</c> ne répondait pas à Entrée. C'est un défaut d'accessibilité :
/// naviguer au clavier est un critère WCAG, pas une commodité.
///
/// ⛔ Mais la boucle est UNIQUE pour toute l'application, et l'application est un outil de
/// frappe. <c>LearningModule</c> et <c>LessonsWindow</c> sont des surfaces où Tab et Entrée
/// sont des caractères à taper, pas des ordres de navigation : leur passer les messages par
/// <c>IsDialogMessageW</c> les mangerait, et un exercice de frappe cesserait de recevoir ce
/// qu'on y tape. <c>OnboardingWindow</c> route déjà Entrée et les flèches lui-même.
///
/// D'où l'inscription volontaire : une fenêtre déclare qu'elle veut la navigation de
/// dialogue, et celles qui ne disent rien gardent leurs frappes. Le défaut est le silence,
/// ce qui rend l'oubli inoffensif — au pire une fenêtre garde le comportement d'avant.
///
/// Tout vit sur le thread d'interface : la boucle de messages, les créations de fenêtres et
/// leurs destructions. Pas de verrou, et il n'en faut pas un.
/// </summary>
static class DialogNavigation
{
    private static readonly HashSet<IntPtr> _dialogs = new();

    /// <summary>
    /// À appeler juste après <c>CreateWindowExW</c> de la fenêtre racine — jamais pour un
    /// contrôle enfant, que <c>GetAncestor</c> ramène de toute façon à sa racine.
    /// </summary>
    public static void Register(IntPtr hwnd)
    {
        if (hwnd != IntPtr.Zero) _dialogs.Add(hwnd);
    }

    /// <summary>
    /// À appeler juste avant <c>DestroyWindow</c>. Un HWND détruit est recyclé par Windows
    /// pour une autre fenêtre : le laisser inscrit ferait router les messages d'un inconnu.
    /// </summary>
    public static void Unregister(IntPtr hwnd) => _dialogs.Remove(hwnd);

    /// <summary>Fenêtres inscrites — pour les témoins, et pour vérifier qu'on désinscrit.</summary>
    public static int RegisteredCount => _dialogs.Count;

    internal static void ResetForTests() => _dialogs.Clear();

    /// <summary>
    /// La décision, sans Win32 : ce message appartient-il à une fenêtre qui a demandé la
    /// navigation de dialogue ?
    /// </summary>
    internal static bool ShouldRouteAsDialog(IntPtr root, IReadOnlySet<IntPtr> registered)
        => root != IntPtr.Zero && registered.Contains(root);

    /// <summary>
    /// Rend vrai quand le message a été consommé par la navigation de dialogue — l'appelant
    /// doit alors sauter <c>TranslateMessage</c> et <c>DispatchMessage</c>, sous peine de
    /// livrer deux fois la même frappe.
    /// </summary>
    public static bool TryRoute(ref Win32.MSG msg)
    {
        // Sortie immédiate tant qu'aucune fenêtre de dialogue n'est ouverte, c'est-à-dire
        // la quasi-totalité du temps : la boucle voit chaque message du processus, y
        // compris les timers de la sonde et du watchdog.
        if (_dialogs.Count == 0) return false;

        IntPtr root = Win32.GetAncestor(msg.hwnd, Win32.GA_ROOT);
        if (!ShouldRouteAsDialog(root, _dialogs)) return false;

        return Win32.IsDialogMessageW(root, ref msg);
    }
}
