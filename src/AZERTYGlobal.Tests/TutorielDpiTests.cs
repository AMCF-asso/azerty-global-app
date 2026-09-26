using System.Reflection;
using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Témoin du correctif DPI du tutoriel (lot 9 du 26/09). La fenêtre naît au DPI du système,
/// puis passe au DPI de son écran quand ils diffèrent. Ce passage refaisait les polices et la
/// taille, mais pas la place des boutons : « Quitter les exercices » restait calculé pour
/// l'ancienne largeur et sortait du bord droit.
///
/// La fenêtre est créée sans jamais être affichée : rien ne s'ouvre à l'écran. Le hook est
/// construit, jamais installé.
/// </summary>
public class TutorielDpiTests : IDisposable
{
    private const BindingFlags Prive = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly string _tempDir;

    public TutorielDpiTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "AZGTutoDpi_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        ConfigManager.OverrideConfigPathForTests(Path.Combine(_tempDir, "config.json"));
        UsageStats.OverrideStatsPathForTests(Path.Combine(_tempDir, "usage-stats.json"));
    }

    public void Dispose()
    {
        while (UsageStats.IsTypingExcluded) UsageStats.EndExcludedTyping();
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    [Fact]
    public void Passer_au_DPI_de_l_ecran_ramene_Quitter_au_bord_droit()
    {
        var layout = LayoutLoader.LoadFromResource();
        var mapper = new KeyMapper(layout, new MockWin32Api());
        var hook = new KeyboardHook(mapper);
        using var module = new LearningModule(IntPtr.Zero, mapper, hook, layout);
        var hwnd = (IntPtr)typeof(LearningModule).GetField("_hWnd", Prive)!.GetValue(module)!;
        var quit = (IntPtr)typeof(LearningModule).GetField("_hWndBtnQuit", Prive)!.GetValue(module)!;
        Assert.NotEqual(IntPtr.Zero, hwnd);

        // Un DPI d'écran autre que celui de la naissance.
        int cible = Win32.GetDpiForWindow(hwnd) == 120 ? 168 : 120;
        module.AdoptWindowDpi(cible);

        Win32.GetClientRect(hwnd, out var client);
        Win32.GetWindowRect(quit, out var bouton);
        var coin = new Win32.POINT { x = bouton.right, y = bouton.top };
        Win32.ScreenToClient(hwnd, ref coin);

        // ⛔ Marge droite de 20 px à 96 DPI, à l'échelle de l'écran : c'est la règle de
        // RepositionControls. Un écart dit que les boutons n'ont pas suivi la nouvelle largeur.
        int marge = (int)(20 * (cible / 96f));
        Assert.Equal(client.right - marge, coin.x);
    }
}
