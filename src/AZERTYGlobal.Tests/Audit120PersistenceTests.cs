using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>Les formats illisibles ou futurs restent récupérables après une tentative d'enregistrement.</summary>
public class Audit120PersistenceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "azerty-audit120-" + Guid.NewGuid());
    public Audit120PersistenceTests()
    {
        Directory.CreateDirectory(_directory);
        ConfigManager.OverrideConfigPathForTests(Path.Combine(_directory, "config.json"));
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("42")]
    [InlineData("true")]
    [InlineData("\"texte\"")]
    public void ConfigRacineInvalide_DemarreSansEcraser(string original)
    {
        var path = Path.Combine(_directory, "config.json");
        File.WriteAllText(path, original);
        Assert.Null(Record.Exception(() =>
        {
            _ = L.Language;
            ConfigManager.SetNotifications(false);
        }));
        Assert.Equal(original, File.ReadAllText(path));
    }

    [Theory]
    [InlineData("{\"version\":2,\"donneeFuture\":\"à conserver\"}")]
    [InlineData("{\"version\":-1}")]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("{\"version\":\"2\"}")]
    [InlineData("{\"version\":999999999999}")]
    public void ProgressionVersionInconnue_ConserveOriginalEtAnnuleLaMutation(string original)
    {
        var path = Path.Combine(_directory, "lessons-progress.json");
        File.WriteAllText(path, original);
        var exercise = new LessonExercise("m", "l", 0, "practice", "Tape", "ab", LessonTypingMode.Flexible);
        var session = new LessonTypingSession(exercise);
        session.TypeChar('a');
        session.TypeChar('b');
        var store = new LessonProgressStore(path);
        store.RecordSuccess(exercise, session.Stats);
        store.SetLastPosition(exercise);
        Assert.Equal(original, File.ReadAllText(path));
        Assert.False(store.IsCompleted(exercise));
        Assert.Null(store.LastModuleId);
        Assert.False(File.Exists(path + ".tmp"));
    }

    public void Dispose()
    {
        // Le journal asynchrone peut terminer une écriture pendant le nettoyage.
        for (int attempt = 0; attempt < 20; attempt++)
        {
            try { Directory.Delete(_directory, true); return; }
            catch (IOException) { Thread.Sleep(10); }
        }
    }
}
