using Xunit;

namespace TypingEngine.Windows.Tests;

/// <summary>
/// Lot 4 de l'audit 1.3.0, A21 (CMP-2) : la suspension d'un jeu anti-triche ou d'un client
/// distant se décide par le processus et la liste, avant toute sonde du champ mot de passe.
/// Côté réel, cette sonde est une requête UIA envoyée au processus du premier plan : le
/// compteur du mock tient lieu de « aucun appel UIA ».
/// </summary>
public class Audit130MoteurTests
{
    private static MockWin32Api Premier(string processName, string fullPath) => new()
    {
        ScriptedProcessName = processName,
        ScriptedFullPath = fullPath,
        ForegroundWindow = (IntPtr)0x1234,
        ScriptedSecureInput = true,
    };

    [Theory]
    [InlineData("VALORANT-Win64-Shipping.exe", @"C:\Riot\Valorant\VALORANT-Win64-Shipping.exe", "AntiCheat")]
    [InlineData("parsecd.exe", @"C:\Program Files\Parsec\parsecd.exe", "RemoteAccess")]
    public void SuspensionImposee_AucuneSondeDuChampMotDePasse(string process, string chemin, string motif)
    {
        var api = Premier(process, chemin);
        using var monitor = new ForegroundMonitor(api, IntPtr.Zero, new TestWindowsTypingHost());
        Assert.Equal(CompatibilityMode.DisabledAntiCheat, monitor.CurrentMode);
        Assert.Equal(motif, monitor.CurrentSuspendReason.ToString());
        Assert.Equal(0, api.PasswordFieldProbeCount);
        // false : sinon IsSnapshotStale relancerait un Recompute chaque seconde en jeu.
        Assert.False(monitor.IsSecureInput);

        monitor.Recompute();
        Assert.Equal(0, api.PasswordFieldProbeCount);
    }

    /// <summary>Témoin négatif : une application ordinaire est sondée, et le champ reconnu.</summary>
    [Fact]
    public void ApplicationOrdinaire_EstSondee()
    {
        var api = Premier("notepad.exe", @"C:\Windows\notepad.exe");
        using var monitor = new ForegroundMonitor(api, IntPtr.Zero, new TestWindowsTypingHost());
        Assert.True(api.PasswordFieldProbeCount >= 1);
        Assert.True(monitor.IsSecureInput);
    }

    /// <summary>
    /// « Forcer la désactivation » reste sondé : la recherche y est servie (R3), et la sonde
    /// empêche d'insérer un caractère dans un champ mot de passe.
    /// </summary>
    [Fact]
    public void OverrideUtilisateur_ResteSonde()
    {
        var api = Premier("editor.exe", @"C:\editor.exe");
        var host = new TestWindowsTypingHost();
        host.SetCompatibilityOverride("editor.exe", "forceOff");
        using var monitor = new ForegroundMonitor(api, IntPtr.Zero, host);
        Assert.Equal(CompatibilitySuspendReason.UserOverride, monitor.CurrentSuspendReason);
        Assert.True(api.PasswordFieldProbeCount >= 1);
        Assert.True(monitor.IsSecureInput);
    }

    /// <summary>Suivi des fenêtres indisponible : tout est suspendu, aucune sonde ne part.</summary>
    [Fact]
    public void SansSuiviDesFenetres_AucuneSonde()
    {
        var api = Premier("VALORANT-Win64-Shipping.exe", @"C:\Riot\Valorant\VALORANT-Win64-Shipping.exe");
        api.ShouldFailSetWinEventHook = true;
        using var monitor = new ForegroundMonitor(api, IntPtr.Zero, new TestWindowsTypingHost());
        Assert.Equal(CompatibilitySuspendReason.UnknownForeground, monitor.CurrentSuspendReason);
        Assert.Equal(0, api.PasswordFieldProbeCount);
    }

    [Theory]
    [InlineData(CompatibilitySuspendReason.AntiCheat, true)]
    [InlineData(CompatibilitySuspendReason.RemoteAccess, true)]
    [InlineData(CompatibilitySuspendReason.UserOverride, false)]
    [InlineData(CompatibilitySuspendReason.UnknownForeground, false)]
    [InlineData(CompatibilitySuspendReason.None, false)]
    public void SkipsPasswordProbe_SeulementPourLesSuspensionsImposeesParLaListe(
        CompatibilitySuspendReason motif, bool attendu)
    {
        Assert.Equal(attendu, ForegroundMonitor.SkipsPasswordProbe(motif));
    }
}
