// Copie de texte dans le presse-papiers — logique partagée (v1.2.0).
//
// La séquence OpenClipboard / EmptyClipboard / SetClipboardData a un piège de propriété :
// après un SetClipboardData réussi, le handle appartient au presse-papiers et le libérer
// provoque une double libération ; avant, il appartient encore à l'appelant et l'oublier
// fuit. CharacterSearch et UsageStatsWindow portaient chacun leur copie de ce code depuis
// la v1.1, et le partage du défi en aurait fait une troisième : les trois appellent
// désormais cette implémentation unique.
//
// Les primitives d'allocation, de lecture et de restauration restent dans CharacterSearch,
// où vivent les constantes de format du presse-papiers.
using System.Runtime.InteropServices;

namespace AZERTYGlobal;

static class ClipboardText
{
    private const uint CF_UNICODETEXT = 13;
    internal const int OpenAttempts = 4;
    internal const int OpenRetryDelayMs = 25;

    internal static bool OpenWithRetry(Func<IntPtr, bool> open, IntPtr owner, Action<int> sleep,
        int attempts = OpenAttempts, int delayMs = OpenRetryDelayMs)
    {
        // Le presse-papiers est tenu un instant par les gestionnaires et les sessions distantes :
        // quelques essais rapprochés suffisent presque toujours, et restent brefs sur le fil
        // d'interface (au plus (attempts - 1) x delayMs).
        for (int attempt = 1; ; attempt++)
        {
            if (open(owner)) return true;
            if (attempt >= attempts) return false;
            sleep(delayMs);
        }
    }

    /// <summary>
    /// Place <paramref name="text"/> dans le presse-papiers. Retourne false sans rien
    /// détruire si le presse-papiers est verrouillé par une autre application — cas courant,
    /// pas une erreur : l'ancien contenu est restauré si l'écriture échoue après le vidage.
    /// </summary>
    public static bool TrySet(IntPtr owner, string text)
    {
        IntPtr hMem = IntPtr.Zero;
        bool ownershipTransferred = false;
        try
        {
            hMem = CharacterSearch.AllocateClipboardText(text);
            if (hMem == IntPtr.Zero) return false;

            if (!OpenWithRetry(Win32.OpenClipboard, owner, Thread.Sleep)) return false;
            try
            {
                string? previousText = CharacterSearch.ReadClipboardText();
                if (!Win32.EmptyClipboard()) return false;

                if (Win32.SetClipboardData(CF_UNICODETEXT, hMem) == IntPtr.Zero)
                {
                    ConfigManager.Log("ClipboardText.TrySet",
                        new ExternalException("SetClipboardData a échoué."));
                    CharacterSearch.RestoreClipboardText(previousText);
                    return false;
                }
                ownershipTransferred = true; // propriété transférée — ne pas libérer
                return true;
            }
            finally
            {
                Win32.CloseClipboard();
            }
        }
        catch (Exception ex) when (ex is ExternalException or ArgumentException or OutOfMemoryException)
        {
            ConfigManager.Log("ClipboardText.TrySet", ex);
            return false;
        }
        finally
        {
            if (!ownershipTransferred && hMem != IntPtr.Zero)
                Win32.GlobalFree(hMem);
        }
    }
}
