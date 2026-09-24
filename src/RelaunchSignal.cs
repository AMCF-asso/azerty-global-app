// Relance depuis Démarrer d'une instance déjà vivante — audit 24/09 (report 1.3.1 promu).
using System.Runtime.InteropServices;

namespace AZERTYGlobal;

/// <summary>
/// En version packagée, une seconde instance de la même session sortait sans rien faire
/// (Program.Main). C'est juste pour une activation système, qui ne doit rien montrer ;
/// c'est un bug pour l'utilisateur qui relance l'application depuis Démarrer : sans
/// accord d'activation, l'instance vivante est inerte, son icône souvent cachée dans le
/// débordement, et la relance ne donnait rien à voir.
///
/// La seconde instance meurt toujours, mais signale d'abord le geste : elle cède son droit
/// au premier plan (AllowSetForegroundWindow) puis poste un message enregistré à la fenêtre
/// de la zone de notification de l'instance vivante. <c>TrayApplication</c> décide de la
/// suite. Rien ne change hors package ni pour le mutex inter-sessions.
/// </summary>
internal static class RelaunchSignal
{
    /// <summary>Nom du message enregistré, propre au produit.</summary>
    internal const string MessageName = ProductIdentity.Namespace + "_RelaunchRequested";

    /// <summary>Classe de la fenêtre de la zone de notification (TrayApplication), fenêtre
    /// de premier niveau masquée, donc visible de FindWindowW dans la session.</summary>
    internal static string TargetWindowClass => ProductIdentity.WindowClass("Wnd");

    private const uint ASFW_ANY = 0xFFFFFFFF;

    /// <summary>Identifiant du message, identique dans les deux processus.</summary>
    internal static uint RegisterMessage() => Win32.RegisterWindowMessageW(MessageName);

    /// <summary>
    /// Vrai si la ligne de commande est celle d'une activation par Windows, qui doit mourir
    /// en silence : serveur COM de l'activateur de toast (« -Embedding », forme standard
    /// d'un ExeServer lancé par COM, le manifeste ne déclarant aucun Arguments) ou
    /// « -ToastActivated », forme documentée pour les toasts de bureau. Le premier élément,
    /// le chemin de l'exécutable, est ignoré.
    /// </summary>
    internal static bool IsSystemActivation(IReadOnlyList<string> args)
    {
        for (int i = 1; i < args.Count; i++)
        {
            var arg = args[i].TrimStart('-', '/');
            if (arg.Equals("Embedding", StringComparison.OrdinalIgnoreCase) ||
                arg.StartsWith("ToastActivated", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Poste le signal à l'instance vivante. Faux si sa fenêtre n'existe pas encore (elle
    /// démarre à peine) ou si l'envoi échoue : la seconde instance sort alors comme avant.
    /// </summary>
    internal static bool TrySignalLiveInstance()
    {
        IntPtr target = FindWindowW(TargetWindowClass, null);
        if (target == IntPtr.Zero) return false;
        uint message = RegisterMessage();
        if (message == 0) return false;
        // Le processus lancé depuis Démarrer détient le droit au premier plan ; l'instance
        // vivante, non. Sans cette cession, l'accueil s'ouvrirait derrière la fenêtre active.
        AllowSetForegroundWindow(ASFW_ANY);
        return Win32.PostMessageW(target, message, IntPtr.Zero, IntPtr.Zero);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr FindWindowW(string lpClassName, string? lpWindowName);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool AllowSetForegroundWindow(uint dwProcessId);
}
