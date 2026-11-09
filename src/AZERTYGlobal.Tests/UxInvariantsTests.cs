using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Invariants d'expérience, vérifiés sur toute la surface plutôt qu'écran par écran (audit UX
/// du 09/11 : Ctrl+A tapait « a » en mode Libre, « Suivant » restait mort à la fin du Défi).
/// </summary>
public class UxInvariantsTests : IDisposable
{
    private const uint VK_LSHIFT = 0xA0;
    private const uint VK_LCONTROL = 0xA2;
    private const uint VK_RCONTROL = 0xA3;
    private const uint VK_LMENU = 0xA4;
    private const uint VK_RMENU = 0xA5;
    private const uint VK_LWIN = 0x5B;
    private const uint VK_RWIN = 0x5C;
    private const uint LLKHF_EXTENDED = 0x01;

    private readonly string _tempDir;
    private readonly Layout _layout = LayoutLoader.LoadFromResource();

    public UxInvariantsTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "AZGUX_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        ConfigManager.OverrideConfigPathForTests(Path.Combine(_tempDir, "config.json"));
    }

    public void Dispose()
    {
        L.Language = "fr";
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    // ── Progression : indépendante de la langue (audit UX du 09/11, L1) ─────────────

    private static string[] AllStableKeys(string language)
    {
        L.Language = language;
        var catalog = LessonCatalogLoader.LoadFromResource();
        var challenge = DailyChallenge.BuildModule(new DateOnly(2026, 11, 9), 0);
        var exercises = catalog.Exercises.AsEnumerable();
        if (challenge != null)
            exercises = exercises.Concat(challenge.Lessons.SelectMany(lesson => lesson.Exercises));
        return exercises.Select(e => e.StableKey).ToArray();
    }

    [Fact]
    public void EveryExerciseKey_IsTheSameInFrenchAndEnglish()
    {
        string[] fr = AllStableKeys("fr");
        string[] en = AllStableKeys("en");

        Assert.True(fr.Length > 50, $"seulement {fr.Length} exercices");
        Assert.Equal(fr, en);
        // La table figée des consignes publiées est bien embarquée (82 exercices le 09/11).
        Assert.True(LessonInstructionHistory.Count >= 80, $"table des consignes : {LessonInstructionHistory.Count}");
    }

    [Fact]
    public void LegacyProgress_SurvivesTheLanguageSwitch_AndMergesBothLanguages()
    {
        L.Language = "fr";
        var frExercise = LessonCatalogLoader.LoadFromResource().Modules[1].Lessons[0].Exercises[0];
        L.Language = "en";
        var enExercise = LessonCatalogLoader.LoadFromResource().Modules[1].Lessons[0].Exercises[0];
        Assert.NotEqual(frExercise.Instruction, enExercise.Instruction);

        // Fichier écrit par une version d'avant le 09/11 : une clé par langue utilisée.
        string frHash = LessonCatalogLoader.ComputeLegacyExerciseHash(frExercise.Type, frExercise.Instruction, frExercise.Content);
        string enHash = LessonCatalogLoader.ComputeLegacyExerciseHash(enExercise.Type, enExercise.Instruction, enExercise.Content);
        string path = Path.Combine(_tempDir, "lessons-progress.json");
        File.WriteAllText(path,
            "{\n  \"version\": 1,\n  \"exercises\": {\n" +
            LegacyEntry(frExercise.KeyPrefix + frHash, frHash, completed: true, attempts: 1, wpm: 30, seconds: 12.5) + ",\n" +
            LegacyEntry(enExercise.KeyPrefix + enHash, enHash, completed: false, attempts: 2, wpm: 41, seconds: 20) + "\n" +
            "  }\n}\n");

        foreach (string language in new[] { "fr", "en" })
        {
            L.Language = language;
            var exercise = LessonCatalogLoader.LoadFromResource().Modules[1].Lessons[0].Exercises[0];
            var store = new LessonProgressStore(path);
            var progress = store.GetValidProgress(exercise);

            Assert.NotNull(progress);
            Assert.True(progress!.Completed, $"réussite perdue en {language}");
            Assert.Equal(3, progress.SuccessfulAttempts);
            Assert.Equal(41, progress.BestWpm);
            Assert.Equal(12.5, progress.BestSeconds);
        }

        // Première écriture : la progression fusionnée passe sous la nouvelle clé, les
        // anciennes restent pour une version précédente de l'app.
        var writer = new LessonProgressStore(path);
        writer.SetLastPosition(enExercise);
        writer.MarkCompletedNeutral(enExercise);
        var reread = new LessonProgressStore(path);
        Assert.True(reread.Exercises.ContainsKey(enExercise.StableKey));
        Assert.Equal(3, reread.Exercises[enExercise.StableKey].SuccessfulAttempts);
        Assert.True(reread.Exercises.ContainsKey(frExercise.KeyPrefix + frHash));
    }

    [Fact]
    public void LegacyProgress_OfChangedContent_StaysInvalid()
    {
        // Comme avant : un texte à taper modifié invalide sa progression.
        L.Language = "fr";
        var exercise = LessonCatalogLoader.LoadFromResource().Modules[1].Lessons[0].Exercises[0];
        string oldHash = LessonCatalogLoader.ComputeLegacyExerciseHash(exercise.Type, exercise.Instruction, exercise.Content + " ancien");
        string path = Path.Combine(_tempDir, "lessons-progress.json");
        File.WriteAllText(path,
            "{\n  \"version\": 1,\n  \"exercises\": {\n" +
            LegacyEntry(exercise.KeyPrefix + oldHash, oldHash, completed: true, attempts: 1, wpm: 30, seconds: 10) + "\n" +
            "  }\n}\n");

        Assert.Null(new LessonProgressStore(path).GetValidProgress(exercise));
    }

    private static string LegacyEntry(string key, string hash, bool completed, int attempts, int wpm, double seconds) =>
        $"    \"{key}\": {{ \"hash\": \"{hash}\", \"completed\": {(completed ? "true" : "false")}, " +
        $"\"successfulAttempts\": {attempts}, \"bestWpm\": {wpm}, \"bestAccuracyPercent\": 95, " +
        $"\"bestSeconds\": {seconds.ToString(System.Globalization.CultureInfo.InvariantCulture)}, " +
        "\"lastCompletedUtc\": null, \"hintsUsed\": 0 }";

    private static readonly (string Name, (uint Vk, uint Sc, uint Flags)[] Keys)[] ShortcutChords =
    {
        ("Ctrl gauche", new[] { (VK_LCONTROL, 0x1Du, 0u) }),
        ("Ctrl droit", new[] { (VK_RCONTROL, 0x1Du, LLKHF_EXTENDED) }),
        ("Ctrl+Maj", new[] { (VK_LCONTROL, 0x1Du, 0u), (VK_LSHIFT, 0x2Au, 0u) }),
        ("Alt gauche", new[] { (VK_LMENU, 0x38u, 0u) }),
        ("Alt+Maj", new[] { (VK_LMENU, 0x38u, 0u), (VK_LSHIFT, 0x2Au, 0u) }),
        ("Win gauche", new[] { (VK_LWIN, 0x5Bu, LLKHF_EXTENDED) }),
        ("Win droit", new[] { (VK_RWIN, 0x5Cu, LLKHF_EXTENDED) }),
        ("Ctrl gauche+Alt gauche", new[] { (VK_LCONTROL, 0x1Du, 0u), (VK_LMENU, 0x38u, 0u) }),
    };

    [Fact]
    public void NoShortcutChord_ExpectsTextFromAnyLayoutKey()
    {
        var failures = new List<string>();
        foreach (var (name, keys) in ShortcutChords)
        {
            var mapper = new KeyMapper(_layout, new MockWin32Api());
            foreach (var (vk, sc, flags) in keys)
                mapper.TrackModifiers(vk, sc, flags, true);

            foreach (uint scancode in _layout.Keys.Keys)
            {
                var buffer = new PhysicalTextInputBuffer(1000, () => 0);
                buffer.CaptureKey(_layout, scancode, mapper);
                if (buffer.HasPending)
                    failures.Add($"{name} + 0x{scancode:X2}");
            }
        }

        Assert.Empty(failures);
    }

    [Fact]
    public void AltGr_StillExpectsLayoutText()
    {
        // Alt Gr physique : Alt droit étendu, précédé du Ctrl gauche fantôme du pilote.
        var mapper = new KeyMapper(_layout, new MockWin32Api());
        mapper.TrackModifiers(VK_LCONTROL, 0x1D, 0, true);
        mapper.TrackModifiers(VK_RMENU, 0x38, LLKHF_EXTENDED, true);
        Assert.True(mapper.AltGrDown);

        int captured = 0;
        foreach (uint scancode in _layout.Keys.Keys)
        {
            string? expected = PhysicalTextInputBuffer.ExpectedText(_layout, scancode, false, true, false, null);
            if (string.IsNullOrEmpty(expected)) continue;
            var buffer = new PhysicalTextInputBuffer(1000, () => 0);
            buffer.CaptureKey(_layout, scancode, mapper);
            Assert.True(buffer.HasPending, $"Alt Gr + 0x{scancode:X2} n'attend plus « {expected} »");
            captured++;
        }

        Assert.True(captured > 20, $"seulement {captured} touches Alt Gr vérifiées");
    }

    [Theory]
    [InlineData('\u0001')] // Ctrl+A
    [InlineData('\u0003')] // Ctrl+C
    [InlineData('\u0016')] // Ctrl+V
    [InlineData('\u0018')] // Ctrl+X
    [InlineData('\u001A')] // Ctrl+Z
    [InlineData('\b')]     // Retour arrière
    [InlineData('\r')]     // Entrée
    [InlineData('\t')]     // Tab
    [InlineData('\u007F')] // Ctrl+Retour arrière
    public void ReceivedControlCharacter_IsNeverReplacedByPendingText(char control)
    {
        var buffer = new PhysicalTextInputBuffer(1000, () => 0);
        buffer.Enqueue("a");

        Assert.Equal(control, buffer.Resolve(control));
        // L'entrée attendue reste pour le WM_CHAR de sa propre frappe.
        Assert.Equal('a', buffer.Resolve('x'));
    }

    [Fact]
    public void ExpectedControlCharacter_IsConsumedByReceivedControl()
    {
        var buffer = new PhysicalTextInputBuffer(1000, () => 0);
        buffer.Enqueue("\r");
        buffer.Enqueue("b");

        Assert.Equal('\r', buffer.Resolve('\r'));
        Assert.Equal('b', buffer.Resolve('x'));
    }

    [Theory]
    [InlineData(true, false, false, false, true)]
    [InlineData(false, true, false, false, true)]
    [InlineData(false, false, true, false, true)]
    [InlineData(true, true, false, true, false)] // Alt Gr = Ctrl+Alt sous Windows
    [InlineData(true, false, false, true, false)]
    [InlineData(false, false, false, false, false)]
    public void ShortcutChord_ExcludesAltGr(bool ctrl, bool alt, bool win, bool altGr, bool expected)
    {
        Assert.Equal(expected, PhysicalTextInputBuffer.IsShortcutChord(ctrl, alt, win, altGr));
    }
}
