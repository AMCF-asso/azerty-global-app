using System.Reflection;
using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Témoin de la page du tutoriel, une seule valeur depuis le lot 9 (audit du 25/09, L-09) au
/// lieu de deux booléens croisés : un exercice réussi mène à la page de choix, la réussite est
/// enregistrée, et les boutons de choix remplacent « Quitter ». La fenêtre est créée sans jamais
/// être affichée, et rien ici ne prend le premier plan.
/// </summary>
public class TutorielPagesTests : IDisposable
{
    private const BindingFlags Prive = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly string _tempDir;

    public TutorielPagesTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "AZGTutoPages_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        ConfigManager.OverrideConfigPathForTests(Path.Combine(_tempDir, "config.json"));
        UsageStats.OverrideStatsPathForTests(Path.Combine(_tempDir, "usage-stats.json"));
    }

    public void Dispose()
    {
        while (UsageStats.IsTypingExcluded) UsageStats.EndExcludedTyping();
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    private static object? Lire(LearningModule module, string champ) =>
        typeof(LearningModule).GetField(champ, Prive)!.GetValue(module);

    [Fact]
    public void Un_exercice_reussi_mene_a_la_page_de_choix()
    {
        var layout = LayoutLoader.LoadFromResource();
        var mapper = new KeyMapper(layout, new MockWin32Api());
        var hook = new KeyboardHook(mapper);
        using var module = new LearningModule(IntPtr.Zero, mapper, hook, layout);
        var saisir = typeof(LearningModule).GetMethod("OnChar", Prive)!;

        Assert.Equal("Exercise", Lire(module, "_page")!.ToString());
        saisir.Invoke(module, new object[] { 'É' });

        Assert.Equal("Choice", Lire(module, "_page")!.ToString());
        Assert.Equal(1, ConfigManager.LearningMaxStepCompleted);
        var recommencer = (IntPtr)Lire(module, "_hWndBtnRetry")!;
        var quitter = (IntPtr)Lire(module, "_hWndBtnQuit")!;
        const int GWL_STYLE = -16;
        const long WS_VISIBLE = 0x10000000;
        Assert.NotEqual(0, GetWindowLongPtrW(recommencer, GWL_STYLE) & WS_VISIBLE);
        Assert.Equal(0, GetWindowLongPtrW(quitter, GWL_STYLE) & WS_VISIBLE);
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern long GetWindowLongPtrW(IntPtr hWnd, int nIndex);
}
