// Porte unique des liens sortants — 1.3.0, stratégie OutboundLinksEnabled (décision d'Antoine
// du 2026-11-17).
//
// Chaque fenêtre masque déjà ses liens quand la stratégie les coupe. Cette porte en est le
// second rempart : tout ce qui ouvre le navigateur ou le Store passe par elle, et elle refuse
// quand la stratégie est à 0. Elle couvre ainsi ce qu'aucun masquage ne voit — un toast resté
// dans le Centre de notifications d'avant la stratégie, un chemin de repli comme celui de la
// notation Store. Un test lit les sources et refuse tout ShellExecuteW « open » qui
// contournerait la porte.

namespace AZERTYGlobal;

/// <summary>Signature de ShellExecuteW, pour que la barre des tâches garde son shell
/// injectable (<see cref="ITrayShell"/>) et que ses tests voient l'appel.</summary>
internal delegate IntPtr ShellExecuteFn(IntPtr hWnd, string operation, string file,
    string? parameters, string? directory, int showCommand);

static class OutboundLinks
{
    private const int SW_SHOWNORMAL = 1;

    /// <summary>Ouvre <paramref name="url"/> par le shell de Windows si la stratégie le
    /// permet. Rend false, sans rien ouvrir, quand elle coupe les liens sortants.</summary>
    internal static bool Open(string url) => Open(url, Win32.ShellExecuteW);

    /// <summary>Même porte, par un shell donné : celui de la barre des tâches, que ses tests
    /// remplacent.</summary>
    internal static bool Open(string url, ShellExecuteFn shellExecute) =>
        Open(url, shellExecute, PolicyManager.OutboundLinksEnabledNow);

    /// <summary>Décision pure, éprouvée par les tests sans toucher au cache des
    /// stratégies.</summary>
    internal static bool Open(string url, ShellExecuteFn shellExecute, bool allowed)
    {
        if (!allowed)
        {
            // Journalisé comme événement de compatibilité, pas comme erreur : une stratégie
            // appliquée n'est pas une panne, et Log armerait la garde de la sollicitation d'avis.
            ConfigManager.LogCompatEvent("OutboundLinkBlocked",
                $"{PolicyManager.ValueOutboundLinks}: link not opened");
            return false;
        }

        shellExecute(IntPtr.Zero, "open", url, null, null, SW_SHOWNORMAL);
        return true;
    }
}
