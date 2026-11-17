using System.Globalization;
using System.Text.Json;

namespace AZERTYGlobal;

internal sealed class LessonProgressStore
{
    public const int CurrentVersion = 1;
    public const string FileName = "lessons-progress.json";

    private readonly string _path;
    private readonly Dictionary<string, LessonExerciseProgress> _exercises = new(StringComparer.Ordinal);
    // Progression par geste (refonte 1.3.0) : par caractère, indépendante des exercices.
    // Champ « gestures » ajouté au format 1 : une version antérieure l'ignore à la lecture.
    private readonly Dictionary<string, GestureMasteryEntry> _gestures = new(StringComparer.Ordinal);
    private readonly Func<DateTimeOffset> _clock;
    private bool _loadFailed;
    // Date d'écriture du fichier lors de notre dernière lecture ou écriture.
    private DateTime? _knownWriteUtc;
    // Fichier corrompu mis de côté (audit du 25/09, L-14) : message à montrer une seule
    // fois. Statique : la fenêtre Leçons crée le magasin, le tray affiche la bulle.
    private static int _quarantineNoticePending;

    /// <summary>Copie mise de côté par le chargement, ou null.</summary>
    internal string? QuarantinedPath { get; private set; }

    /// <summary>Vrai une seule fois après une mise de côté : le tray dit alors que la
    /// progression a été remise à zéro et qu'une copie est gardée.</summary>
    internal static bool TakeQuarantineNotice() => Interlocked.Exchange(ref _quarantineNoticePending, 0) == 1;

    public LessonProgressStore(string? path = null, Func<DateTimeOffset>? clock = null)
    {
        _path = path ?? Path.Combine(ConfigManager.LogDirectory, FileName);
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
        Load();
    }

    public IReadOnlyDictionary<string, GestureMasteryEntry> Gestures => _gestures;

    /// <summary>Les caractères à revoir maintenant, les plus fragiles d'abord.</summary>
    public IReadOnlyList<GestureMasteryEntry> DueGestures(int maxCount)
    {
        ReloadIfChangedOnDisk(); // gestes versés par le tutoriel pendant que les Leçons sont ouvertes
        return GestureMastery.Due(_gestures.Values, _clock(), maxCount);
    }

    public string? LastModuleId { get; private set; }
    public string? LastLessonId { get; private set; }
    public int LastExerciseIndex { get; private set; }
    // Rangs de l'Initiation déjà importés du tutoriel. Le fichier garde le compteur
    // « onboardingSyncedMaxStep » (la suite depuis 0, lue par les versions précédentes) et,
    // depuis le parcours par usage, la liste complète « onboardingSyncedExercises ».
    private readonly SortedSet<int> _onboardingSynced = new();

    /// <summary>Longueur de la suite des rangs importés depuis 0.</summary>
    public int OnboardingSyncedMaxStep
    {
        get
        {
            int rank = 0;
            while (_onboardingSynced.Contains(rank)) rank++;
            return rank;
        }
    }

    public IReadOnlySet<int> OnboardingSyncedExercises => _onboardingSynced;
    public IReadOnlyDictionary<string, LessonExerciseProgress> Exercises => _exercises;

    /// <summary>
    /// Progression de l'exercice, en lecture : la clé actuelle, sinon la fusion des clés
    /// écrites sous l'ancienne empreinte (consigne FR ou EN incluse), sans rien modifier.
    /// </summary>
    public LessonExerciseProgress? GetValidProgress(LessonExercise exercise)
    {
        if (_exercises.TryGetValue(exercise.StableKey, out var progress) &&
            StringComparer.Ordinal.Equals(progress.Hash, exercise.Hash))
            return progress;
        return MergeLegacy(exercise);
    }

    /// <summary>
    /// Fusionne les progressions enregistrées sous les anciennes empreintes de l'exercice
    /// (une par langue au plus) : réussi si l'une l'est, meilleurs scores, tentatives et
    /// indices additionnés. S'y ajoute la réussite, sans scores, d'une ancienne forme de
    /// l'exercice (<see cref="LessonExercise.SupersededKeys"/>). Null si aucune. Les anciennes
    /// entrées restent dans le fichier : une version précédente de l'app les relit telles quelles.
    /// </summary>
    internal LessonExerciseProgress? MergeLegacy(LessonExercise exercise)
    {
        LessonExerciseProgress? merged = null;
        foreach (string legacyHash in exercise.LegacyHashes)
        {
            if (!_exercises.TryGetValue(exercise.KeyPrefix + legacyHash, out var old) ||
                !StringComparer.Ordinal.Equals(old.Hash, legacyHash))
                continue;
            merged ??= new LessonExerciseProgress(exercise.StableKey, exercise.Hash);
            merged.Completed |= old.Completed;
            merged.SuccessfulAttempts += old.SuccessfulAttempts;
            merged.HintsUsed += old.HintsUsed;
            merged.BestWpm = Max(merged.BestWpm, old.BestWpm);
            merged.BestAccuracyPercent = Max(merged.BestAccuracyPercent, old.BestAccuracyPercent);
            merged.BestSeconds = merged.BestSeconds is { } a && old.BestSeconds is { } b
                ? Math.Min(a, b)
                : merged.BestSeconds ?? old.BestSeconds;
            if (old.LastCompletedUtc > merged.LastCompletedUtc || merged.LastCompletedUtc == null)
                merged.LastCompletedUtc = old.LastCompletedUtc ?? merged.LastCompletedUtc;
        }

        // Anciennes formes de l'exercice (texte ou rang changé depuis une version publiée,
        // audit de régression du 10/11, RA2) : la réussite seule est reprise, sans scores.
        foreach (string key in exercise.SupersededKeys)
        {
            if (!_exercises.TryGetValue(key, out var old) || !old.Completed ||
                !key.EndsWith("/" + old.Hash, StringComparison.Ordinal))
                continue;
            merged ??= new LessonExerciseProgress(exercise.StableKey, exercise.Hash);
            merged.Completed = true;
            merged.SuccessfulAttempts = Math.Max(1, merged.SuccessfulAttempts);
        }
        return merged;

        static int? Max(int? a, int? b) => a is { } x && b is { } y ? Math.Max(x, y) : a ?? b;
    }

    public bool IsCompleted(LessonExercise exercise)
    {
        return GetValidProgress(exercise)?.Completed == true;
    }

    /// <summary>
    /// Meilleure vitesse enregistrée sur un extrait du Défi, tous jours confondus (audit UX du
    /// 09/11, L10) : les clés « defi/defi-aaaammjj/1/empreinte » des jours passés restent dans
    /// le fichier, y compris celles d'avant la 1.3.0. Null si aucun extrait n'a de vitesse.
    /// </summary>
    public int? BestChallengeWpm()
    {
        int? best = null;
        foreach (var (key, progress) in _exercises)
        {
            if (progress.BestWpm is not int wpm || !IsChallengeExtractKey(key)) continue;
            if (best is null || wpm > best) best = wpm;
        }
        return best;
    }

    /// <summary>Clé « module/leçon/rang/empreinte » d'un extrait du Défi.</summary>
    private static bool IsChallengeExtractKey(string key)
    {
        string[] parts = key.Split('/');
        return parts.Length == 4 &&
               StringComparer.Ordinal.Equals(parts[0], DailyChallenge.ModuleId) &&
               StringComparer.Ordinal.Equals(parts[2], DailyChallenge.ExtractExerciseIndex.ToString(CultureInfo.InvariantCulture));
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
        // Le Défi n'est jamais la place de « Reprendre » (ME-07, C6.2) : même garde que
        // RecordSuccess, pour tous les chemins (relance de séance après « Réinitialiser »).
        if (exercise.ModuleId == DailyChallenge.ModuleId) return;
        // Une position remplace la précédente : en attente, elle aussi (file bornée, revue d'AF-02).
        Mutate("LessonProgressStore.SetLastPosition", () =>
        {
            LastModuleId = exercise.ModuleId;
            LastLessonId = exercise.LessonId;
            LastExerciseIndex = exercise.ExerciseIndex;
        }, replacesPendingKind: "position");
    }

    public void RecordSuccess(LessonExercise exercise, LessonAttemptStats stats) =>
        RecordSuccess(exercise, stats, GestureMastery.IsTracked);

    /// <param name="isTracked">Caractères suivis par geste ; les tests en passent un sans
    /// index de caractères.</param>
    internal void RecordSuccess(LessonExercise exercise, LessonAttemptStats stats, Func<char, bool> isTracked)
    {
        // Révision ou texte collé : contenu composé, rien à retenir par exercice (L-13).
        if (LessonCatalogLoader.IsComposed(exercise.ModuleId))
        {
            var reviewedAt = _clock();
            Mutate("LessonProgressStore.RecordReview", () => RecordGesturesNoSave(stats, isTracked, reviewedAt));
            return;
        }
        var completedUtc = DateTimeOffset.UtcNow;
        var gestureNow = _clock();
        Mutate("LessonProgressStore.RecordSuccess", () =>
        {
            RecordGesturesNoSave(stats, isTracked, gestureNow);
            RecordSuccessNoSave(exercise, stats, completedUtc);
        });
    }

    private void RecordSuccessNoSave(LessonExercise exercise, LessonAttemptStats stats, DateTimeOffset completedUtc)
    {
        var progress = GetOrReset(exercise);
        progress.Completed = true;
        progress.SuccessfulAttempts++;
        progress.LastCompletedUtc = completedUtc;
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

        // Le Défi ne s'ouvre que par ShowChallenge, reconstruit au jour et à l'étape : ce n'est
        // jamais la place de « Reprendre » (audit 09/11, L5).
        if (exercise.ModuleId != DailyChallenge.ModuleId)
            SetLastPositionNoSave(exercise);
    }

    /// <summary>
    /// Verse un exercice réussi du tutoriel d'accueil dans la progression par geste
    /// (décision d'Antoine du 07/10). Les exercices du tutoriel n'ont pas d'entrée propre ici :
    /// leur rang va toujours au compteur de la config, importé par <see cref="SyncOnboardingProgress(LessonCatalog, IReadOnlySet{int})"/>.
    /// Une fenêtre Leçons ouverte en même temps relit le fichier avant d'écrire
    /// (<see cref="ReloadIfChangedOnDisk"/>) : ces gestes ne sont pas écrasés.
    /// </summary>
    public static void RecordTutorialGestures(LessonAttemptStats stats)
    {
        try { Shared.RecordGestures(stats, GestureMastery.IsTracked); }
        catch (Exception ex) { ConfigManager.Log("LessonProgressStore.RecordTutorialGestures", ex); }
    }

    private static LessonProgressStore? _shared;

    /// <summary>
    /// Magasin unique du fichier de l'utilisateur, pour tout le processus (revue d'AF-02,
    /// 16/11) : un changement gardé après un échec d'écriture survit ainsi à la fenêtre des
    /// Leçons recréée (changement de langue) et aux gestes du tutoriel, au lieu de mourir avec
    /// un magasin jetable. Relit le fichier s'il a changé depuis. Fil de l'interface seulement.
    /// </summary>
    internal static LessonProgressStore Shared
    {
        get
        {
            string path = Path.Combine(ConfigManager.LogDirectory, FileName);
            if (_shared == null || !string.Equals(_shared._path, path, StringComparison.OrdinalIgnoreCase))
                _shared = new LessonProgressStore(path);
            else
                _shared.ReloadIfChangedOnDisk();
            return _shared;
        }
    }

    internal void RecordGestures(LessonAttemptStats stats, Func<char, bool> isTracked)
    {
        var now = _clock();
        Mutate("LessonProgressStore.RecordGestures", () => RecordGesturesNoSave(stats, isTracked, now));
    }

    public void MarkCompletedNeutral(LessonExercise exercise) =>
        Mutate("LessonProgressStore.MarkCompletedNeutral", () => MarkCompletedNeutralNoSave(exercise));

    /// <param name="now">Heure du geste, figée par l'appelant : un rejeu après un échec
    /// d'écriture (AF-02) garde l'heure réelle, pas celle du rejeu.</param>
    private void RecordGesturesNoSave(LessonAttemptStats stats, Func<char, bool> isTracked, DateTimeOffset now)
    {
        foreach (var (expected, attempts, errors) in stats.GetCharacterCounts())
        {
            if (!isTracked(expected)) continue;
            string key = expected.ToString();
            if (!_gestures.TryGetValue(key, out var entry))
                _gestures[key] = entry = new GestureMasteryEntry(key);
            GestureMastery.Apply(entry, attempts, errors, now);
        }
    }

    private void MarkCompletedNeutralNoSave(LessonExercise exercise)
    {
        var progress = GetOrReset(exercise);
        progress.Completed = true;
        progress.SuccessfulAttempts = Math.Max(1, progress.SuccessfulAttempts);
    }

    /// <summary>Import d'un compteur seul : les <paramref name="completedSteps"/> premiers
    /// exercices de l'Initiation (sens des configs d'avant le parcours par usage).</summary>
    public void SyncOnboardingProgress(LessonCatalog catalog, int completedSteps) =>
        SyncOnboardingProgress(catalog, Enumerable.Range(0, Math.Max(0, completedSteps)).ToHashSet());

    /// <summary>
    /// Importe les réussites du tutoriel dans l'Initiation, par rang fixe
    /// (<see cref="ConfigManager.LearningCompletedExercises"/>) : le parcours du tutoriel varie
    /// avec les usages, l'exercice de rang i de l'Initiation reste le même. Un rang déjà
    /// importé ne l'est plus, même après une remise à zéro des Leçons.
    /// </summary>
    public void SyncOnboardingProgress(LessonCatalog catalog, IReadOnlySet<int> completedExercises)
    {
        if (completedExercises.All(_onboardingSynced.Contains)) return;
        Mutate("LessonProgressStore.SyncOnboardingProgress", () =>
        {
            foreach (var exercise in catalog.Exercises)
            {
                if (!StringComparer.Ordinal.Equals(exercise.ModuleId, LessonCatalogLoader.InitiationModuleId))
                    continue;
                if (completedExercises.Contains(exercise.ExerciseIndex) && !_onboardingSynced.Contains(exercise.ExerciseIndex))
                    MarkCompletedNeutralNoSave(exercise);
            }
            _onboardingSynced.UnionWith(completedExercises);
        });
    }

    public void ResetAll(int? onboardingSyncedMaxStep = null)
    {
        var synced = onboardingSyncedMaxStep is int maxStep
            ? Enumerable.Range(0, Math.Max(0, maxStep)).ToArray()
            : ConfigManager.LearningCompletedExercises.ToArray();
        Mutate("LessonProgressStore.ResetAll", () =>
        {
            _exercises.Clear();
            _gestures.Clear();
            LastModuleId = null;
            LastLessonId = null;
            LastExerciseIndex = 0;
            _onboardingSynced.Clear();
            _onboardingSynced.UnionWith(synced);
        });
    }

    private LessonExerciseProgress GetOrReset(LessonExercise exercise)
    {
        if (!_exercises.TryGetValue(exercise.StableKey, out var progress) ||
            !StringComparer.Ordinal.Equals(progress.Hash, exercise.Hash))
        {
            // Première écriture sous la nouvelle empreinte : reprend la progression des
            // anciennes clés (audit UX du 09/11, L1) au lieu de repartir de zéro.
            progress = MergeLegacy(exercise) ?? new LessonExerciseProgress(exercise.StableKey, exercise.Hash);
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

        var gestures = new Dictionary<string, GestureMasteryEntry>(StringComparer.Ordinal);
        foreach (var (key, entry) in _gestures)
            gestures[key] = entry.Clone();

        return new ProgressStoreSnapshot(
            LastModuleId,
            LastLessonId,
            LastExerciseIndex,
            _onboardingSynced.ToArray(),
            exercises,
            gestures);
    }

    private void RestoreSnapshot(ProgressStoreSnapshot snapshot)
    {
        LastModuleId = snapshot.LastModuleId;
        LastLessonId = snapshot.LastLessonId;
        LastExerciseIndex = snapshot.LastExerciseIndex;
        _onboardingSynced.Clear();
        _onboardingSynced.UnionWith(snapshot.OnboardingSynced);
        _exercises.Clear();
        foreach (var (key, progress) in snapshot.Exercises)
            _exercises[key] = progress.Clone();
        _gestures.Clear();
        foreach (var (key, entry) in snapshot.Gestures)
            _gestures[key] = entry.Clone();
    }

    /// <summary>
    /// Relit le fichier quand un autre magasin l'a réécrit depuis notre dernière lecture ou
    /// écriture : le tutoriel d'accueil verse ses gestes pendant que la fenêtre Leçons garde
    /// le sien ouvert. Chaque écrivain part ainsi de l'état sur disque, rien n'est écrasé.
    /// Toutes les écritures passent avant par ici ; un fichier non chargé reste intouché.
    /// </summary>
    private void ReloadIfChangedOnDisk()
    {
        if (_loadFailed) return;
        try
        {
            DateTime? current = File.Exists(_path) ? File.GetLastWriteTimeUtc(_path) : null;
            if (current != _knownWriteUtc)
            {
                // Revue d'AF-02 : une relecture qui tombe sur un verrou passager ne doit pas
                // emporter les changements en attente. On garde alors la mémoire, la file et la
                // date connue ; aucune écriture tant que la relecture n'a pas réussi (elle
                // écraserait ce que l'autre écrivain a mis dans le fichier).
                var keep = _pendingChanges.Count > 0 ? CaptureSnapshot() : null;
                var keepUnknown = keep != null ? _unknownRootFields.ToArray() : null;
                var knownBefore = _knownWriteUtc;
                Load();
                if (keep != null && _loadFailed && _lastLoadWasReadFailure)
                {
                    RestoreSnapshot(keep);
                    _unknownRootFields.Clear();
                    _unknownRootFields.AddRange(keepUnknown!);
                    _knownWriteUtc = knownBefore;
                    _loadFailed = false;
                    _reloadDeferred = true;
                    return;
                }
                _reloadDeferred = false;
                // AF-02 : les changements pas encore écrits sont rejoués sur l'état relu, comme
                // s'ils venaient d'être faits : ni ceux de l'autre écrivain ni les nôtres ne
                // sont perdus. Un fichier d'un format plus récent ou mis de côté les abandonne
                // (la mémoire suit alors le disque).
                if (_loadFailed) _pendingChanges.Clear();
                else foreach (var (_, change) in _pendingChanges) change();
            }
            else
                _reloadDeferred = false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ConfigManager.Log("LessonProgressStore.ReloadIfChangedOnDisk", ex);
        }
    }

    /// <summary>
    /// Changements gardés en mémoire après un échec d'écriture (AF-02, audit des fichiers
    /// 1.3.0). Avant, la mémoire revenait à l'état d'avant : un fichier tenu un instant par un
    /// antivirus ou une sauvegarde, ou un disque plein, faisait perdre la réussite sans un mot,
    /// même après une écriture suivante réussie. Ils s'écrivent avec le changement suivant, à la
    /// fermeture de la fenêtre des Leçons ou de l'application (<see cref="RetryPendingSave"/>).
    /// </summary>
    private readonly List<(string? Kind, Action Change)> _pendingChanges = new();

    /// <summary>Relecture repoussée par un verrou passager : rien ne s'écrit avant elle.</summary>
    private bool _reloadDeferred;

    /// <summary>Le dernier <see cref="Load"/> a échoué sur une lecture impossible (verrou,
    /// droits), et non sur un format plus récent ou un contenu corrompu.</summary>
    private bool _lastLoadWasReadFailure;

    /// <summary>Vrai tant qu'un changement attend d'être écrit.</summary>
    internal bool HasPendingSave => _pendingChanges.Count > 0;

    /// <summary>Changements en attente (témoins de la file bornée).</summary>
    internal int PendingChangeCount => _pendingChanges.Count;

    /// <summary>
    /// Toute écriture de la progression : relit le fichier s'il a changé, applique le
    /// changement, écrit. Sur un échec, un fichier chargé garde le changement en mémoire pour
    /// le prochain essai (AF-02) ; un fichier non chargé (format plus récent, lecture
    /// impossible) n'est jamais écrit, et la mémoire revient à son état pour ne pas diverger.
    /// </summary>
    /// <param name="replacesPendingKind">Genre d'un changement qui remplace le précédent du
    /// même genre (la position de reprise) : la file reste bornée par les vrais changements
    /// sur un échec durable (fichier en lecture seule, disque plein).</param>
    private void Mutate(string operation, Action change, string? replacesPendingKind = null)
    {
        ReloadIfChangedOnDisk();
        var snapshot = CaptureSnapshot();
        change();
        if (!_reloadDeferred && Save(operation))
            _pendingChanges.Clear();
        else if (_loadFailed)
            RestoreSnapshot(snapshot);
        else
        {
            if (replacesPendingKind != null)
                _pendingChanges.RemoveAll(p => p.Kind == replacesPendingKind);
            _pendingChanges.Add((replacesPendingKind, change));
        }
    }

    /// <summary>Réessaie d'écrire les changements gardés après un échec (AF-02). Sans effet
    /// quand rien n'attend. Rend faux si des changements restent non écrits.</summary>
    public bool RetryPendingSave()
    {
        if (_pendingChanges.Count == 0) return true;
        ReloadIfChangedOnDisk();
        if (_pendingChanges.Count > 0 && !_reloadDeferred && Save("LessonProgressStore.RetryPendingSave"))
            _pendingChanges.Clear();
        return _pendingChanges.Count == 0;
    }

    /// <summary>Champs de la racine que cette version lit et écrit.</summary>
    private static readonly HashSet<string> KnownRootFields = new(StringComparer.Ordinal)
    {
        "version", "lastModuleId", "lastLessonId", "lastExerciseIndex", "onboardingSyncedMaxStep",
        "onboardingSyncedExercises", "exercises", "gestures",
    };

    /// <summary>
    /// Champs inconnus de la racine, gardés tels quels et réécrits (AF-03, audit des fichiers
    /// 1.3.0) : le format 1 s'enrichit sans changer de numéro (« gestures » y est né), et une
    /// version plus récente qui ajoute un champ le perdait au premier passage par celle-ci.
    /// </summary>
    private readonly List<KeyValuePair<string, JsonElement>> _unknownRootFields = new();

    private void Load()
    {
        _exercises.Clear();
        _gestures.Clear();
        _unknownRootFields.Clear();
        _loadFailed = false;
        _knownWriteUtc = null;
        _lastLoadWasReadFailure = false;
        if (!File.Exists(_path)) return;
        bool legacyErrorMatrixFound = false;
        try { _knownWriteUtc = File.GetLastWriteTimeUtc(_path); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }

        try
        {
            using var doc = FileQuarantine.ParseUserFile(File.ReadAllText(_path)); // AF-01
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

            foreach (var prop in root.EnumerateObject())
                if (!KnownRootFields.Contains(prop.Name))
                    _unknownRootFields.Add(new(prop.Name, prop.Value.Clone()));
            FileQuarantine.RemoveOrphanTemps(_path); // AF-06

            LastModuleId = ReadString(root, "lastModuleId");
            LastLessonId = ReadString(root, "lastLessonId");
            LastExerciseIndex = ReadInt(root, "lastExerciseIndex");
            _onboardingSynced.Clear();
            _onboardingSynced.UnionWith(Enumerable.Range(0, Math.Max(0, ReadInt(root, "onboardingSyncedMaxStep"))));
            if (root.TryGetProperty("onboardingSyncedExercises", out var syncedEl) && syncedEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in syncedEl.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.Number && item.TryGetInt32(out int index) && index >= 0)
                        _onboardingSynced.Add(index);
                }
            }

            if (root.TryGetProperty("gestures", out var gesturesEl) && gesturesEl.ValueKind == JsonValueKind.Object)
            {
                foreach (var item in gesturesEl.EnumerateObject())
                {
                    var el = item.Value;
                    if (item.Name.Length == 0 || el.ValueKind != JsonValueKind.Object) continue;
                    _gestures[item.Name] = new GestureMasteryEntry(item.Name)
                    {
                        Attempts = Math.Max(0, ReadInt(el, "attempts")),
                        Errors = Math.Max(0, ReadInt(el, "errors")),
                        Box = Math.Clamp(ReadInt(el, "box"), 0, GestureMastery.MaxBox),
                        LastReviewUtc = ReadDate(el, "lastReviewUtc"),
                        DueUtc = ReadDate(el, "dueUtc"),
                    };
                }
            }

            if (!root.TryGetProperty("exercises", out var exercisesEl) || exercisesEl.ValueKind != JsonValueKind.Object)
                return;

            foreach (var item in exercisesEl.EnumerateObject())
            {
                var el = item.Value;
                // Une entree qui n'est pas un objet (nombre, null, tableau, chaine) est ignoree, comme
                // pour "gestures" : TryGetProperty leverait InvalidOperationException, hors du filtre
                // ci-dessous, et la fenetre des Lecons ne s'ouvrirait plus (ME-02).
                if (el.ValueKind != JsonValueKind.Object) continue;
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
            _gestures.Clear();
            LastModuleId = null;
            LastLessonId = null;
            LastExerciseIndex = 0;
            _onboardingSynced.Clear();

            if (FileQuarantine.IsReadFailure(ex))
            {
                // Lecture impossible pour l'instant (verrou, droits) : sans doute intact.
                // Aucune écriture pendant la session, le prochain lancement relit.
                _loadFailed = true;
                _lastLoadWasReadFailure = true;
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
                writer.WriteStartArray("onboardingSyncedExercises");
                foreach (int index in _onboardingSynced)
                    writer.WriteNumberValue(index);
                writer.WriteEndArray();
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
                writer.WriteStartObject("gestures");
                foreach (var (key, entry) in _gestures.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                {
                    writer.WriteStartObject(key);
                    writer.WriteNumber("attempts", entry.Attempts);
                    writer.WriteNumber("errors", entry.Errors);
                    writer.WriteNumber("box", entry.Box);
                    WriteNullableDate(writer, "lastReviewUtc", entry.LastReviewUtc);
                    WriteNullableDate(writer, "dueUtc", entry.DueUtc);
                    writer.WriteEndObject();
                }
                writer.WriteEndObject();
                foreach (var (name, value) in _unknownRootFields) // AF-03
                {
                    writer.WritePropertyName(name);
                    value.WriteTo(writer);
                }
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

            _knownWriteUtc = File.GetLastWriteTimeUtc(_path);
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

    private static DateTimeOffset? ReadDate(JsonElement element, string name) =>
        DateTimeOffset.TryParse(ReadString(element, name), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dto)
            ? dto
            : null;

    private static void WriteNullableDate(Utf8JsonWriter writer, string name, DateTimeOffset? value)
    {
        if (value.HasValue) writer.WriteString(name, value.Value.ToString("O", CultureInfo.InvariantCulture));
        else writer.WriteNull(name);
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
    IReadOnlyList<int> OnboardingSynced,
    IReadOnlyDictionary<string, LessonExerciseProgress> Exercises,
    IReadOnlyDictionary<string, GestureMasteryEntry> Gestures);
