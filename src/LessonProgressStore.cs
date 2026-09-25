using System.Globalization;
using System.Text.Json;

namespace AZERTYGlobal;

internal sealed class LessonProgressStore
{
    public const int CurrentVersion = 1;
    public const string FileName = "lessons-progress.json";

    private readonly string _path;
    private readonly Dictionary<string, LessonExerciseProgress> _exercises = new(StringComparer.Ordinal);
    private bool _loadFailed;
    // Fichier corrompu mis de côté (audit du 25/09, L-14) : message à montrer une seule
    // fois. Statique : la fenêtre Leçons crée le magasin, le tray affiche la bulle.
    private static int _quarantineNoticePending;

    /// <summary>Copie mise de côté par le chargement, ou null.</summary>
    internal string? QuarantinedPath { get; private set; }

    /// <summary>Vrai une seule fois après une mise de côté : le tray dit alors que la
    /// progression a été remise à zéro et qu'une copie est gardée.</summary>
    internal static bool TakeQuarantineNotice() => Interlocked.Exchange(ref _quarantineNoticePending, 0) == 1;

    public LessonProgressStore(string? path = null)
    {
        _path = path ?? Path.Combine(ConfigManager.LogDirectory, FileName);
        Load();
    }

    public string? LastModuleId { get; private set; }
    public string? LastLessonId { get; private set; }
    public int LastExerciseIndex { get; private set; }
    public int OnboardingSyncedMaxStep { get; private set; }
    public IReadOnlyDictionary<string, LessonExerciseProgress> Exercises => _exercises;

    public LessonExerciseProgress? GetValidProgress(LessonExercise exercise)
    {
        return _exercises.TryGetValue(exercise.StableKey, out var progress) &&
               StringComparer.Ordinal.Equals(progress.Hash, exercise.Hash)
            ? progress
            : null;
    }

    public bool IsCompleted(LessonExercise exercise)
    {
        return GetValidProgress(exercise)?.Completed == true;
    }

    public int CountCompleted(LessonCatalog catalog)
    {
        int count = 0;
        foreach (var exercise in catalog.Exercises)
        {
            if (IsCompleted(exercise)) count++;
        }
        return count;
    }

    public void SetLastPosition(LessonExercise exercise)
    {
        var snapshot = CaptureSnapshot();
        LastModuleId = exercise.ModuleId;
        LastLessonId = exercise.LessonId;
        LastExerciseIndex = exercise.ExerciseIndex;
        CommitOrRollback(snapshot, "LessonProgressStore.SetLastPosition");
    }

    public void RecordSuccess(LessonExercise exercise, LessonAttemptStats stats)
    {
        var snapshot = CaptureSnapshot();
        var progress = GetOrReset(exercise);
        progress.Completed = true;
        progress.SuccessfulAttempts++;
        progress.LastCompletedUtc = DateTimeOffset.UtcNow;
        progress.HintsUsed += stats.HintCount;

        if (stats.Wpm.HasValue)
            progress.BestWpm = !progress.BestWpm.HasValue ? stats.Wpm : Math.Max(progress.BestWpm.Value, stats.Wpm.Value);
        if (stats.AccuracyPercent.HasValue)
            progress.BestAccuracyPercent = !progress.BestAccuracyPercent.HasValue
                ? stats.AccuracyPercent
                : Math.Max(progress.BestAccuracyPercent.Value, stats.AccuracyPercent.Value);
        if (stats.ElapsedSeconds > 0)
            progress.BestSeconds = !progress.BestSeconds.HasValue
                ? stats.ElapsedSeconds
                : Math.Min(progress.BestSeconds.Value, stats.ElapsedSeconds);

        SetLastPositionNoSave(exercise);
        CommitOrRollback(snapshot, "LessonProgressStore.RecordSuccess");
    }

    public void MarkCompletedNeutral(LessonExercise exercise)
    {
        var snapshot = CaptureSnapshot();
        MarkCompletedNeutralNoSave(exercise);
        CommitOrRollback(snapshot, "LessonProgressStore.MarkCompletedNeutral");
    }

    private void MarkCompletedNeutralNoSave(LessonExercise exercise)
    {
        var progress = GetOrReset(exercise);
        progress.Completed = true;
        progress.SuccessfulAttempts = Math.Max(1, progress.SuccessfulAttempts);
    }

    public void SyncOnboardingProgress(LessonCatalog catalog, int completedSteps)
    {
        if (completedSteps <= OnboardingSyncedMaxStep) return;
        var snapshot = CaptureSnapshot();
        int imported = 0;
        foreach (var exercise in catalog.Exercises)
        {
            if (!StringComparer.Ordinal.Equals(exercise.ModuleId, LessonCatalogLoader.InitiationModuleId))
                continue;
            if (imported >= completedSteps) break;
            if (imported >= OnboardingSyncedMaxStep)
                MarkCompletedNeutralNoSave(exercise);
            imported++;
        }
        OnboardingSyncedMaxStep = Math.Max(OnboardingSyncedMaxStep, completedSteps);
        CommitOrRollback(snapshot, "LessonProgressStore.SyncOnboardingProgress");
    }

    public void ResetAll(int? onboardingSyncedMaxStep = null)
    {
        var snapshot = CaptureSnapshot();
        _exercises.Clear();
        LastModuleId = null;
        LastLessonId = null;
        LastExerciseIndex = 0;
        OnboardingSyncedMaxStep = onboardingSyncedMaxStep ?? ConfigManager.LearningMaxStepCompleted;
        CommitOrRollback(snapshot, "LessonProgressStore.ResetAll");
    }

    private LessonExerciseProgress GetOrReset(LessonExercise exercise)
    {
        if (!_exercises.TryGetValue(exercise.StableKey, out var progress) ||
            !StringComparer.Ordinal.Equals(progress.Hash, exercise.Hash))
        {
            progress = new LessonExerciseProgress(exercise.StableKey, exercise.Hash);
            _exercises[exercise.StableKey] = progress;
        }
        return progress;
    }

    private void SetLastPositionNoSave(LessonExercise exercise)
    {
        LastModuleId = exercise.ModuleId;
        LastLessonId = exercise.LessonId;
        LastExerciseIndex = exercise.ExerciseIndex;
    }

    private ProgressStoreSnapshot CaptureSnapshot()
    {
        var exercises = new Dictionary<string, LessonExerciseProgress>(StringComparer.Ordinal);
        foreach (var (key, progress) in _exercises)
            exercises[key] = progress.Clone();

        return new ProgressStoreSnapshot(
            LastModuleId,
            LastLessonId,
            LastExerciseIndex,
            OnboardingSyncedMaxStep,
            exercises);
    }

    private void RestoreSnapshot(ProgressStoreSnapshot snapshot)
    {
        LastModuleId = snapshot.LastModuleId;
        LastLessonId = snapshot.LastLessonId;
        LastExerciseIndex = snapshot.LastExerciseIndex;
        OnboardingSyncedMaxStep = snapshot.OnboardingSyncedMaxStep;
        _exercises.Clear();
        foreach (var (key, progress) in snapshot.Exercises)
            _exercises[key] = progress.Clone();
    }

    private void CommitOrRollback(ProgressStoreSnapshot snapshot, string operation)
    {
        if (!Save(operation))
            RestoreSnapshot(snapshot);
    }

    private void Load()
    {
        _exercises.Clear();
        _loadFailed = false;
        if (!File.Exists(_path)) return;
        bool legacyErrorMatrixFound = false;

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(_path));
            var root = doc.RootElement;
            if (IsNewerFormat(root))
            {
                // Écrit par une version plus récente (retour arrière) : intact, mais pas pour
                // nous. Ni quarantaine ni écriture : la version récente le retrouvera tel quel.
                _loadFailed = true;
                ConfigManager.Log("LessonProgressStore.Load",
                    new IOException("Progression d'un format plus récent ; fichier conservé."));
                return;
            }
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("version", out var version) ||
                version.ValueKind != JsonValueKind.Number ||
                !version.TryGetInt32(out int formatVersion) || formatVersion != CurrentVersion)
                throw new JsonException("Format de progression non pris en charge ; fichier conservé.");

            LastModuleId = ReadString(root, "lastModuleId");
            LastLessonId = ReadString(root, "lastLessonId");
            LastExerciseIndex = ReadInt(root, "lastExerciseIndex");
            OnboardingSyncedMaxStep = ReadInt(root, "onboardingSyncedMaxStep");

            if (!root.TryGetProperty("exercises", out var exercisesEl) || exercisesEl.ValueKind != JsonValueKind.Object)
                return;

            foreach (var item in exercisesEl.EnumerateObject())
            {
                var el = item.Value;
                string hash = ReadString(el, "hash") ?? "";
                if (hash.Length == 0) continue;

                var progress = new LessonExerciseProgress(item.Name, hash)
                {
                    Completed = ReadBool(el, "completed"),
                    SuccessfulAttempts = ReadInt(el, "successfulAttempts"),
                    BestWpm = ReadNullableInt(el, "bestWpm"),
                    BestAccuracyPercent = ReadNullableInt(el, "bestAccuracyPercent"),
                    BestSeconds = ReadNullableDouble(el, "bestSeconds"),
                    HintsUsed = ReadInt(el, "hintsUsed")
                };

                string? lastCompleted = ReadString(el, "lastCompletedUtc");
                if (DateTimeOffset.TryParse(lastCompleted, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dto))
                    progress.LastCompletedUtc = dto;

                if (el.TryGetProperty("errorMatrix", out _))
                    legacyErrorMatrixFound = true;

                _exercises[item.Name] = progress;
            }

            if (legacyErrorMatrixFound)
                Save("LessonProgressStore.MigrateLegacyErrorMatrix");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException || FileQuarantine.IsCorruption(ex))
        {
            ConfigManager.Log("LessonProgressStore.Load", ex);
            _exercises.Clear();
            LastModuleId = null;
            LastLessonId = null;
            LastExerciseIndex = 0;
            OnboardingSyncedMaxStep = 0;

            if (FileQuarantine.IsReadFailure(ex))
            {
                // Lecture impossible pour l'instant (verrou, droits) : sans doute intact.
                // Aucune écriture pendant la session, le prochain lancement relit.
                _loadFailed = true;
            }
            else if (FileQuarantine.IsCorruption(ex))
            {
                // Contenu corrompu (L-14) : le refus d'écrire bloquait en silence toute
                // progression nouvelle. Fichier mis de côté, jamais supprimé ; on repart de
                // zéro et les sauvegardes reprennent. Renommage raté : même règle qu'une
                // lecture impossible.
                QuarantinedPath = FileQuarantine.TryMoveAside(_path, "LessonProgressStore.Quarantine");
                if (QuarantinedPath != null)
                    Interlocked.Exchange(ref _quarantineNoticePending, 1);
                else
                    _loadFailed = true;
            }
            // Sinon : fichier disparu entre File.Exists et la lecture, comme absent.
        }
    }

    /// <summary>Numéro de format supérieur au nôtre : fichier d'une version plus récente.</summary>
    private static bool IsNewerFormat(JsonElement root) =>
        root.ValueKind == JsonValueKind.Object &&
        root.TryGetProperty("version", out var version) &&
        version.ValueKind == JsonValueKind.Number &&
        version.TryGetDouble(out double value) && value > CurrentVersion;

    private bool Save(string operation)
    {
        // AG130-11 (b) : .tmp suffixe par le PID, deux instances du meme compte
        // (RDP + console) ecrivant sinon dans le meme fichier temporaire.
        string tempPath = $"{_path}.{Environment.ProcessId}.tmp";
        try
        {
            if (_loadFailed && File.Exists(_path))
            {
                ConfigManager.Log(operation, new IOException("Sauvegarde ignoree: lessons-progress.json existant non charge."));
                return false;
            }

            string? dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
            {
                writer.WriteStartObject();
                writer.WriteNumber("version", CurrentVersion);
                WriteNullableString(writer, "lastModuleId", LastModuleId);
                WriteNullableString(writer, "lastLessonId", LastLessonId);
                writer.WriteNumber("lastExerciseIndex", LastExerciseIndex);
                writer.WriteNumber("onboardingSyncedMaxStep", OnboardingSyncedMaxStep);
                writer.WriteStartObject("exercises");
                foreach (var (key, progress) in _exercises.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                {
                    writer.WriteStartObject(key);
                    writer.WriteString("hash", progress.Hash);
                    writer.WriteBoolean("completed", progress.Completed);
                    writer.WriteNumber("successfulAttempts", progress.SuccessfulAttempts);
                    WriteNullableNumber(writer, "bestWpm", progress.BestWpm);
                    WriteNullableNumber(writer, "bestAccuracyPercent", progress.BestAccuracyPercent);
                    WriteNullableNumber(writer, "bestSeconds", progress.BestSeconds);
                    if (progress.LastCompletedUtc.HasValue)
                        writer.WriteString("lastCompletedUtc", progress.LastCompletedUtc.Value.ToString("O", CultureInfo.InvariantCulture));
                    else
                        writer.WriteNull("lastCompletedUtc");
                    writer.WriteNumber("hintsUsed", progress.HintsUsed);
                    writer.WriteEndObject();
                }
                writer.WriteEndObject();
                writer.WriteEndObject();
                // L-14 : atomique, mais pas durable sans ceci — après une coupure de courant,
                // le fichier remplacé pouvait rester vide ou illisible.
                writer.Flush();
                stream.Flush(true);
            }

            if (File.Exists(_path))
                File.Replace(tempPath, _path, null, true);
            else
                File.Move(tempPath, _path);

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ConfigManager.Log(operation, ex);
            try
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
            catch
            {
            }
            return false;
        }
    }

    private static string? ReadString(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static bool ReadBool(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;
    }

    private static int ReadInt(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int result)
            ? result
            : 0;
    }

    private static int? ReadNullableInt(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int result)
            ? result
            : null;
    }

    private static double? ReadNullableDouble(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out double result)
            ? result
            : null;
    }

    private static void WriteNullableString(Utf8JsonWriter writer, string name, string? value)
    {
        if (value == null) writer.WriteNull(name);
        else writer.WriteString(name, value);
    }

    private static void WriteNullableNumber(Utf8JsonWriter writer, string name, int? value)
    {
        if (value.HasValue) writer.WriteNumber(name, value.Value);
        else writer.WriteNull(name);
    }

    private static void WriteNullableNumber(Utf8JsonWriter writer, string name, double? value)
    {
        if (value.HasValue) writer.WriteNumber(name, value.Value);
        else writer.WriteNull(name);
    }
}

internal sealed class LessonExerciseProgress
{
    public LessonExerciseProgress(string stableKey, string hash)
    {
        StableKey = stableKey;
        Hash = hash;
    }

    public string StableKey { get; }
    public string Hash { get; }
    public bool Completed { get; set; }
    public int SuccessfulAttempts { get; set; }
    public int? BestWpm { get; set; }
    public int? BestAccuracyPercent { get; set; }
    public double? BestSeconds { get; set; }
    public DateTimeOffset? LastCompletedUtc { get; set; }
    public int HintsUsed { get; set; }

    public LessonExerciseProgress Clone()
    {
        var clone = new LessonExerciseProgress(StableKey, Hash)
        {
            Completed = Completed,
            SuccessfulAttempts = SuccessfulAttempts,
            BestWpm = BestWpm,
            BestAccuracyPercent = BestAccuracyPercent,
            BestSeconds = BestSeconds,
            LastCompletedUtc = LastCompletedUtc,
            HintsUsed = HintsUsed
        };
        return clone;
    }
}

internal sealed record ProgressStoreSnapshot(
    string? LastModuleId,
    string? LastLessonId,
    int LastExerciseIndex,
    int OnboardingSyncedMaxStep,
    IReadOnlyDictionary<string, LessonExerciseProgress> Exercises);
