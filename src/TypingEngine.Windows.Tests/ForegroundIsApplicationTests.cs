using Xunit;

namespace TypingEngine.Windows.Tests;

/// <summary>
/// Signalé par Antoine le 2026-11-12 : après une recherche de caractères ouverte puis fermée,
/// « Rechercher un caractère » restait grisé dans le menu de la zone de notification alors que
/// l'application était active. La sonde du champ mot de passe répond « sécurisé » dès qu'aucun
/// contrôle n'a le focus : mesuré sur la barre des tâches (<c>GetGUIThreadInfo</c> rend
/// hwndFocus = 0), d'où s'ouvre le menu. <see cref="ForegroundMonitor.ForegroundIsApplication"/>
/// dit si ce « sécurisé » vient d'une vraie application ou d'un premier plan sans champ de texte.
/// </summary>
public class ForegroundIsApplicationTests
{
    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern IntPtr FindWindowW(string className, string? windowName);

    private static MockWin32Api Premier(string? processName, uint pid = 1234) => new()
    {
        ScriptedProcessName = processName,
        ScriptedFullPath = processName == null ? null : @"C:\Windows\" + processName,
        ScriptedPid = pid,
        ForegroundWindow = processName == null ? IntPtr.Zero : (IntPtr)0x1234,
        ScriptedSecureInput = true,
    };

    [Fact]
    public void ApplicationOrdinaire_EstUneApplication()
    {
        using var monitor = new ForegroundMonitor(Premier("notepad.exe"), IntPtr.Zero, new TestWindowsTypingHost());
        Assert.True(monitor.IsSecureInput);
        Assert.True(monitor.ForegroundIsApplication);
    }

    [Theory]
    [InlineData("explorer.exe")]
    [InlineData("SearchHost.exe")]
    [InlineData("StartMenuExperienceHost.exe")]
    public void SurfaceDuShell_NEstPasUneApplication(string process)
    {
        using var monitor = new ForegroundMonitor(Premier(process), IntPtr.Zero, new TestWindowsTypingHost());
        Assert.True(monitor.IsSecureInput); // la sonde reste prudente : rien ne change côté frappe
        Assert.False(monitor.ForegroundIsApplication);
    }

    [Fact]
    public void AucuneFenetre_NEstPasUneApplication()
    {
        using var monitor = new ForegroundMonitor(Premier(null), IntPtr.Zero, new TestWindowsTypingHost());
        Assert.True(monitor.IsSecureInput);
        Assert.False(monitor.ForegroundIsApplication);
    }

    [Fact]
    public void NosPropresFenetres_NeSontPasUneApplication()
    {
        using var monitor = new ForegroundMonitor(
            Premier("AZERTY Global.exe", (uint)Environment.ProcessId), IntPtr.Zero, new TestWindowsTypingHost());
        Assert.False(monitor.ForegroundIsApplication);
    }

    /// <summary>
    /// La mesure d'origine, sur le vrai Windows : la barre des tâches n'a aucun contrôle qui a le
    /// focus, et la vraie sonde la déclare « mot de passe ». Sans barre des tâches (session sans
    /// shell), le témoin se retire.
    /// </summary>
    [Fact]
    public void BarreDesTaches_LaVraieSondeLaDitSecurisee()
    {
        IntPtr taskbar = FindWindowW("Shell_TrayWnd", null);
        if (taskbar == IntPtr.Zero) return;
        Assert.True(new RealWin32Api().IsWindowPasswordField(taskbar));
    }

    /// <summary>Le scénario signalé : application, recherche ouverte, puis premier plan sans fenêtre.</summary>
    [Fact]
    public void ApplicationPuisPremierPlanVide_LeIndicateurSuitLePremierPlan()
    {
        var api = Premier("notepad.exe");
        using var monitor = new ForegroundMonitor(api, IntPtr.Zero, new TestWindowsTypingHost());
        Assert.True(monitor.ForegroundIsApplication);

        api.ScriptedProcessName = null;
        api.ForegroundWindow = IntPtr.Zero;
        monitor.Recompute();
        Assert.True(monitor.IsSecureInput);
        Assert.False(monitor.ForegroundIsApplication);

        api.ScriptedProcessName = "notepad.exe";
        api.ScriptedFullPath = @"C:\Windows\notepad.exe";
        api.ForegroundWindow = (IntPtr)0x1234;
        monitor.Recompute();
        Assert.True(monitor.ForegroundIsApplication);
    }
}
