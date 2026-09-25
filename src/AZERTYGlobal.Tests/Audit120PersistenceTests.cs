using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Les formats illisibles ou futurs restent récupérables après une tentative d'enregistrement.
/// Audit du 25/09 : un fichier illisible est mis de côté, intact, et les sauvegardes
/// reprennent ; un format de progression plus récent reste en place, sans écriture.
/// </summary>
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
    public void ConfigRacineInvalide_MiseDeCoteSansPerte_PuisReprise(string original)
    {
        var path = Path.Combine(_directory, "config.json");
        File.WriteAllText(path, original);
        Assert.Null(Record.Exception(() =>
        {
            _ = L.Language;
            ConfigManager.SetNotifications(false);
            ConfigManager.Flush();
        }));
        var copie = Assert.Single(Directory.GetFiles(_directory, "config.json" + FileQuarantine.Marker + "*"));
        Assert.Equal(original, File.ReadAllText(copie));
        Assert.Contains("\"notificationsEnabled\": false", File.ReadAllText(path));
    }

    [Theory]
    [InlineData("{\"version\":-1}")]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("{\"version\":\"2\"}")]
    public void ProgressionIllisible_MiseDeCoteSansPerte_PuisReprise(string original)
    {
        var path = Path.Combine(_directory, "lessons-progress.json");
        File.WriteAllText(path, original);
        var exercise = new LessonExercise("m", "l", 0, "practice", "Tape", "ab", LessonTypingMode.Flexible);
        var session = new LessonTypingSession(exercise);
        session.TypeChar('a');
        session.TypeChar('b');
        var store = new LessonProgressStore(path);
        store.RecordSuccess(exercise, session.Stats);
        var copie = Assert.Single(Directory.GetFiles(_directory, "lessons-progress.json" + FileQuarantine.Marker + "*"));
        Assert.Equal(original, File.ReadAllText(copie));
        Assert.True(store.IsCompleted(exercise));
        Assert.True(new LessonProgressStore(path).IsCompleted(exercise));
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Theory]
    [InlineData("{\"version\":2,\"donneeFuture\":\"à conserver\"}")]
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
