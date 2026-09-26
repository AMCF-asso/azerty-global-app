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
/// qu'on y tape. L’accueil s’inscrit comme dialogue ; seules ses flèches restent locales.
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
    private static readonly Dictionary<IntPtr, Action<IntPtr>> _focusHandlers = new();

    /// <summary>
    /// À appeler juste après <c>CreateWindowExW</c> de la fenêtre racine — jamais pour un
    /// contrôle enfant, que <c>GetAncestor</c> ramène de toute façon à sa racine.
    /// </summary>
    public static void Register(IntPtr hwnd, Action<IntPtr>? ensureFocusVisible = null)
    {
        if (hwnd == IntPtr.Zero) return;
        _dialogs.Add(hwnd);
        if (ensureFocusVisible != null) _focusHandlers[hwnd] = ensureFocusVisible;
    }

    /// <summary>
    /// À appeler juste avant <c>DestroyWindow</c>. Un HWND détruit est recyclé par Windows
    /// pour une autre fenêtre : le laisser inscrit ferait router les messages d'un inconnu.
    /// </summary>
    public static void Unregister(IntPtr hwnd)
    {
        _dialogs.Remove(hwnd);
        _focusHandlers.Remove(hwnd);
    }

    /// <summary>Fenêtres inscrites — pour les témoins, et pour vérifier qu'on désinscrit.</summary>
    public static int RegisteredCount => _dialogs.Count;

    internal static void ResetForTests() { _dialogs.Clear(); _focusHandlers.Clear(); }

    /// <summary>Identifiant que <c>IsDialogMessageW</c> envoie en <c>WM_COMMAND</c> sur Entrée.</summary>
    public const int IDOK = 1;
    /// <summary>Identifiant que <c>IsDialogMessageW</c> envoie en <c>WM_COMMAND</c> sur Échap.</summary>
    public const int IDCANCEL = 2;

    /// <summary><c>WM_GETDLGCODE</c> : le contrôle veut toutes les touches — <c>IsDialogMessageW</c> ne les traite plus.</summary>
    public const long DLGC_WANTALLKEYS = 0x0004;
    private const int VK_TAB = 0x09;

    /// <summary>
    /// Revue du 2026-09-21, R2 — un contrôle qui répond <c>DLGC_WANTALLKEYS</c> sans exception
    /// piège le clavier : <c>DLGC_WANTALLKEYS</c> vaut <c>DLGC_WANTMESSAGE</c>, donc
    /// <c>IsDialogMessageW</c> renonce à Tab aussi, et le focus ne sort plus jamais du
    /// contrôle (WCAG 2.1.2). La réponse doit dépendre de la touche interrogée : Tab est
    /// rendu à la navigation, tout le reste est gardé par le contrôle.
    /// </summary>
    /// <param name="baseCode">Ce que le contrôle répondait déjà (<c>DefSubclassProc</c>).</param>
    /// <param name="inputMessage">Le message que <c>IsDialogMessageW</c> s'apprête à traiter, 0 quand il n'y en a pas.</param>
    /// <param name="inputVk">Sa touche virtuelle, 0 quand il n'y en a pas.</param>
    public static long DialogCodeKeepingTab(long baseCode, uint inputMessage, long inputVk)
    {
        bool isKeyDown = inputMessage == Win32.WM_KEYDOWN || inputMessage == Win32.WM_SYSKEYDOWN;
        if (isKeyDown && inputVk == VK_TAB) return baseCode;
        return baseCode | DLGC_WANTALLKEYS;
    }

    /// <summary>
    /// Audit du 25/09, F-10 : la même règle, en lisant le MSG que pointe le lParam de
    /// <c>WM_GETDLGCODE</c> ; nul quand <c>IsDialogMessageW</c> n'interroge pas pour une
    /// touche. Ce décodage était recopié dans chaque sous-classe.
    /// </summary>
    public static long DialogCodeKeepingTab(long baseCode, IntPtr lParam)
    {
        if (lParam == IntPtr.Zero) return DialogCodeKeepingTab(baseCode, 0, 0);
        var input = System.Runtime.InteropServices.Marshal.PtrToStructure<Win32.MSG>(lParam);
        return DialogCodeKeepingTab(baseCode, input.message, input.wParam.ToInt64());
    }

    /// <summary>
    /// Revue du 2026-09-21, R5 — <c>IsDialogMessageW</c> ne livre jamais Entrée ni Échap à la
    /// fenêtre : il les convertit en <c>WM_COMMAND</c> portant <c>IDOK</c> ou <c>IDCANCEL</c>, et
    /// retourne TRUE sans dispatcher. Les gestionnaires <c>WM_KEYDOWN</c>/<c>VK_ESCAPE</c> des
    /// fenêtres inscrites sont donc morts ; c'est ici que la touche se relit.
    /// </summary>
    public static bool IsEscapeCommand(int commandId) => commandId == IDCANCEL;

    /// <summary>
    /// Entrée sur un bouton poussoir doit presser ce bouton, pas le bouton par défaut. Sans
    /// <c>DefDlgProc</c>, rien ne fait du bouton focalisé le bouton par défaut temporaire :
    /// <c>IsDialogMessageW</c> envoie <c>IDOK</c> quoi qu'il arrive. Rend le bouton à presser,
    /// <c>IntPtr.Zero</c> si le focus n'est sur aucun des boutons connus.
    /// </summary>
    public static IntPtr ButtonToPressOnEnter(int commandId, IntPtr focused, IReadOnlyCollection<IntPtr> pushButtons)
    {
        if (commandId != IDOK || focused == IntPtr.Zero) return IntPtr.Zero;
        return pushButtons.Contains(focused) ? focused : IntPtr.Zero;
    }

    /// <summary>
    /// K3 (accessibilité 1.3.0) — arrêt de tabulation suivant dans une liste circulaire, pour
    /// une surface de frappe qui reste hors d'<c>IsDialogMessageW</c> et fait circuler son focus
    /// elle-même. Un départ hors de la liste entre par le premier arrêt, ou par le dernier à
    /// reculons ; rend -1 quand la liste est vide.
    /// </summary>
    public static int NextFocusStop(int current, int count, bool backwards)
    {
        if (count <= 0) return -1;
        if (current < 0 || current >= count) return backwards ? count - 1 : 0;
        return (current + (backwards ? count - 1 : 1)) % count;
    }

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

        bool consumed = Win32.IsDialogMessageW(root, ref msg);
        if (consumed && _focusHandlers.TryGetValue(root, out var ensureVisible))
            ensureVisible(Win32.GetFocus());
        return consumed;
    }
}
