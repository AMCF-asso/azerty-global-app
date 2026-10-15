// Relance après une mise à jour — audit final 1.3.0, A08-02.
using System.Runtime.InteropServices;

namespace AZERTYGlobal;

/// <summary>
/// Une mise à jour du Store ferme l'app ouverte. Sans inscription, Windows ne la relançait
/// pas : le clavier repassait en silence sur la disposition de Windows jusqu'à ce que
/// l'utilisateur rouvre l'app (A08-01, A08-02). L'instance qui a obtenu le mutex s'inscrit
/// donc auprès de Windows pour être relancée après une mise à jour, et seulement là :
/// ni après un plantage, ni après un blocage, ni au redémarrage du système.
///
/// La relance porte <see cref="RelaunchSignal.AfterUpdateArgument"/>. Si une instance tourne
/// déjà quand elle arrive (démarrage auto plus rapide), la seconde meurt en silence comme une
/// activation système, sans bulle « Actif » en double (<see cref="RelaunchSignal.IsSystemActivation"/>).
///
/// ⚠️ Limite : la relance elle-même est un geste de Windows, prouvé par la recette (paquet de
/// numéro supérieur installé pendant que l'app tourne). Windows ne relance qu'une app ouverte
/// depuis au moins 60 s.
/// </summary>
internal static class RestartRegistration
{
    private const int RESTART_NO_CRASH = 1;
    private const int RESTART_NO_HANG = 2;
    private const int RESTART_NO_PATCH = 4;
    private const int RESTART_NO_REBOOT = 8;

    /// <summary>Relance après une mise à jour seulement. ⛔ Jamais <c>RESTART_NO_PATCH</c> :
    /// il couperait précisément le cas voulu.</summary>
    internal const int Flags = RESTART_NO_CRASH | RESTART_NO_HANG | RESTART_NO_REBOOT;

    /// <summary>Bit à garder éteint (témoin des tests).</summary>
    internal const int NoPatchFlag = RESTART_NO_PATCH;

    /// <summary>Inscrit l'instance courante ; ne lève jamais. Un échec est journalisé et
    /// laisse l'app tourner comme avant.</summary>
    internal static void Register()
    {
        try
        {
            int hr = RegisterApplicationRestart(RelaunchSignal.AfterUpdateArgument, Flags);
            if (hr != 0)
                ConfigManager.LogCompatEvent("RestartRegistration", $"hr=0x{hr:X8}");
        }
        catch (Exception ex)
        {
            ConfigManager.LogCompatEvent("RestartRegistration", ex.GetType().Name);
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int RegisterApplicationRestart(string pwzCommandline, int dwFlags);
}
