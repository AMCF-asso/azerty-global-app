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

        // Le tutoriel s'ouvre sur l'écran des usages ; « Continuer » pose le parcours.
        Assert.Equal("Usages", Lire(module, "_page")!.ToString());
        module.StartRoute(new[] { TutorialSteps.UsageMessages });
        Assert.Equal("Exercise", Lire(module, "_page")!.ToString());
        Assert.Equal(new[] { TutorialSteps.UsageMessages }, ConfigManager.LearningUsages);
        saisir.Invoke(module, new object[] { 'É' });

        Assert.Equal("Choice", Lire(module, "_page")!.ToString());
        Assert.Equal(new[] { 0 }, ConfigManager.LearningCompletedExercises);
        var recommencer = (IntPtr)Lire(module, "_hWndBtnRetry")!;
        var quitter = (IntPtr)Lire(module, "_hWndBtnQuit")!;
        const int GWL_STYLE = -16;
        const long WS_VISIBLE = 0x10000000;
        Assert.NotEqual(0, GetWindowLongPtrW(recommencer, GWL_STYLE) & WS_VISIBLE);
        Assert.Equal(0, GetWindowLongPtrW(quitter, GWL_STYLE) & WS_VISIBLE);
    }

    [Fact]
    public void Les_reussites_hors_suite_sont_gardees_et_le_compteur_reste_continu()
    {
        ConfigManager.RecordLearningExerciseCompleted(TutorialSteps.Messages);
        Assert.Equal(0, ConfigManager.LearningMaxStepCompleted);
        Assert.Equal(new[] { TutorialSteps.Messages }, ConfigManager.LearningCompletedExercises);

        ConfigManager.RecordLearningExerciseCompleted(0);
        ConfigManager.RecordLearningExerciseCompleted(2);
        Assert.Equal(1, ConfigManager.LearningMaxStepCompleted); // le rang 1 manque encore
        ConfigManager.RecordLearningExerciseCompleted(1);
        Assert.Equal(3, ConfigManager.LearningMaxStepCompleted);
        Assert.Equal(new[] { 0, 1, 2, TutorialSteps.Messages }, ConfigManager.LearningCompletedExercises);

        ConfigManager.RecordLearningExerciseCompleted(99); // hors table : ignoré
        Assert.Equal(4, ConfigManager.LearningCompletedExercises.Count);
    }

    [Fact]
    public void Les_usages_sont_normalises_et_relus()
    {
        ConfigManager.SetLearningUsages(new[] { "afrique", "inconnu", "documents", "afrique" });
        Assert.Equal(new[] { TutorialSteps.UsageDocuments, TutorialSteps.UsageAfrica }, ConfigManager.LearningUsages);
        ConfigManager.SetLearningUsages(Array.Empty<string>());
        Assert.Empty(ConfigManager.LearningUsages);
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern long GetWindowLongPtrW(IntPtr hWnd, int nIndex);
}
