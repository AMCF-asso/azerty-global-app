namespace TypingEngine.Windows.Tests;

/// <summary>
/// Audit des chemins d'erreur (rapport-D K109) : un abonné de ForegroundChanged qui lève ne doit ni
/// remplacer le snapshot calculé par le repli « premier plan inconnu / champ sécurisé », ni faire
/// remonter l'exception hors de Recompute (appelé depuis un rappel natif WinEvent).
/// </summary>
public class CheminsErreurMoniteurTests
{
    // rapport-D K109
    [Fact]
    [Trait("Audit", "chemins-erreur")]
    public void AbonneQuiLeve_GardeLeSnapshotCalculeEtNeLaisseRienRemonter()
    {
        var api = new MockWin32Api
        {
            ScriptedProcessName = "notepad.exe",
            ScriptedFullPath = @"C:\Windows\notepad.exe",
            ScriptedModules = new[] { "kernel32.dll", "user32.dll" },
        };
        using var monitor = new ForegroundMonitor(api, IntPtr.Zero, new TestWindowsTypingHost());
        Assert.Equal(CompatibilityMode.Default, monitor.CurrentMode);

        monitor.ForegroundChanged += () => throw new InvalidOperationException("abonné défaillant");
        var erreur = Record.Exception(() => monitor.Recompute());

        Assert.Null(erreur);
        Assert.Equal(CompatibilityMode.Default, monitor.CurrentMode);
        Assert.Equal(CompatibilitySuspendReason.None, monitor.CurrentSuspendReason);
        Assert.False(monitor.IsSecureInput);
        Assert.Equal("notepad.exe", monitor.CurrentProcessName);
    }
}
