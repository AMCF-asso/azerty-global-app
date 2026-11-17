using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AZERTYGlobal;

internal enum LessonTypingMode
{
    Flexible,
    Strict
}

internal sealed class LessonCatalog
{
    public LessonCatalog(IReadOnlyList<LessonModule> modules)
    {
        Modules = modules;
        Exercises = modules
            .SelectMany(module => module.Lessons)
            .SelectMany(lesson => lesson.Exercises)
            .ToArray();
    }

    public IReadOnlyList<LessonModule> Modules { get; }
    public IReadOnlyList<LessonExercise> Exercises { get; }
    public int TotalExerciseCount => Exercises.Count;
    public int SiteModuleCount => Modules.Count(module => !module.IsSynthetic);
    public int SiteLessonCount => Modules.Where(module => !module.IsSynthetic).SelectMany(module => module.Lessons).Count();
    public int SiteExerciseCount => Modules.Where(module => !module.IsSynthetic).SelectMany(module => module.Lessons).SelectMany(lesson => lesson.Exercises).Count();
}

internal sealed class LessonModule
{
    public LessonModule(string id, string title, string description, string icon, bool isSynthetic, IReadOnlyList<LessonLesson> lessons)
    {
        Id = id;
        Title = title;
        Description = description;
        Icon = icon;
        IsSynthetic = isSynthetic;
        Lessons = lessons;
    }

    public string Id { get; }
    public string Title { get; }
    public string Description { get; }
    public string Icon { get; }
    public bool IsSynthetic { get; }
    public IReadOnlyList<LessonLesson> Lessons { get; }
}

/// <summary>
/// Nom affiché d'une leçon dans son module. Quand les deux portent le même titre (« Prise en main »,
/// « Majuscules accentuées »), le nom n'est écrit qu'une fois : avis global 1.3.0, C1. Les titres
/// ne sont pas modifiés (ils n'entrent pas dans l'empreinte de progression, décision « un seul nom »).
/// </summary>
internal static class LessonNames
{
    public static bool SameTitle(string module, string lesson) =>
        string.Equals(module?.Trim(), lesson?.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>« Module séparateur Leçon », ou le seul titre quand les deux sont identiques.</summary>
    public static string Join(string module, string lesson, string separator) =>
        SameTitle(module, lesson) ? lesson : module + separator + lesson;
}

internal sealed class LessonLesson
{
    public LessonLesson(string moduleId, string id, string title, string description, IReadOnlyList<string> characters, IReadOnlyList<LessonExercise> exercises)
    {
        ModuleId = moduleId;
        Id = id;
        Title = title;
        Description = description;
        Characters = characters;
        Exercises = exercises;
    }

    public string ModuleId { get; }
    public string Id { get; }
    public string Title { get; }
    public string Description { get; }
    public IReadOnlyList<string> Characters { get; }
    public IReadOnlyList<LessonExercise> Exercises { get; }
}

internal sealed class LessonExercise
{
    public LessonExercise(
        string moduleId,
        string lessonId,
        int exerciseIndex,
        string type,
        string instruction,
        string content,
        LessonTypingMode typingMode)
    {
        ModuleId = moduleId;
        LessonId = lessonId;
        ExerciseIndex = exerciseIndex;
        Type = type;
        Instruction = instruction;
        Content = content.Replace("\r\n", "\n");
        TypingMode = typingMode;
        // Audit UX du 09/11 (L1) : l'empreinte ne dépend que de ce qu'on tape, jamais de la
        // consigne traduite. Elle incluait la consigne : passer en anglais cachait toute la
        // progression française. Les clés d'avant se reconnaissent par LegacyHashes.
        Hash = LessonCatalogLoader.ComputeExerciseHash(type, Content);
        // Audit du 25/09 (L-06) : calculée une fois. Interpolée à chaque lecture, elle l'était
        // plus de 160 fois par repeint (IsCompleted, comptes de la barre latérale).
        KeyPrefix = $"{ModuleId}/{LessonId}/{ExerciseIndex}/";
        StableKey = KeyPrefix + Hash;
    }

    /// <summary>« module/leçon/rang/ » : la clé de progression sans l'empreinte.</summary>
    public string KeyPrefix { get; }

    /// <summary>
    /// Empreintes que les versions d'avant le 09/11/2026 donnaient à cet exercice : une par
    /// consigne publiée (FR et EN, table figée `lesson-instruction-history.json`) et par la
    /// consigne actuelle. Calculées avec le texte actuel : un texte modifié depuis se
    /// retrouve par <see cref="SupersededKeys"/>, s'il est dans la table des anciennes formes.
    /// </summary>
    public IReadOnlyList<string> LegacyHashes => _legacyHashes ??= LessonInstructionHistory
        .For(KeyPrefix.TrimEnd('/'))
        .Append(Instruction)
        .Distinct(StringComparer.Ordinal)
        .Select(instruction => LessonCatalogLoader.ComputeLegacyExerciseHash(Type, instruction, Content))
        .ToArray();

    private string[]? _legacyHashes;

    /// <summary>
    /// Clés de progression complètes (« module/leçon/rang/empreinte ») que les versions
    /// publiées donnaient aux anciennes formes de cet exercice : un autre texte à taper, ou
    /// un autre rang dans la leçon (table `lesson-content-history.json`, audit de régression
    /// du 10/11, RA2). Une par consigne connue, plus l'empreinte sans consigne. Une réussite
    /// trouvée sous l'une d'elles est reprise sans ses scores
    /// (<see cref="LessonProgressStore.MergeLegacy"/>). Un texte absent de la table reste
    /// « à faire ».
    /// </summary>
    public IReadOnlyList<string> SupersededKeys => _supersededKeys ??= LessonContentHistory
        .For(KeyPrefix.TrimEnd('/'))
        .SelectMany(form => LessonInstructionHistory.For(form.Key)
            .Concat(LessonInstructionHistory.For(KeyPrefix.TrimEnd('/')))
            .Append(Instruction)
            .Distinct(StringComparer.Ordinal)
            .Select(instruction => LessonCatalogLoader.ComputeLegacyExerciseHash(Type, instruction, form.Content))
            .Append(LessonCatalogLoader.ComputeExerciseHash(Type, form.Content))
            .Select(hash => $"{form.Key}/{hash}"))
        .Distinct(StringComparer.Ordinal)
        .ToArray();

    private string[]? _supersededKeys;

    public string ModuleId { get; }
    public string LessonId { get; }
    public int ExerciseIndex { get; }
    public string Type { get; }
    public string Instruction { get; }
    public string Content { get; }
    public LessonTypingMode TypingMode { get; }
    public string Hash { get; }
    public string StableKey { get; }
    public string[] Lines => Content.Split('\n');
}

internal static class LessonCatalogLoader
{
    public const string LessonsResourceName = "lessons.json";
    public const string InitiationModuleId = "initiation";
    public const string InitiationLessonId = "premiers-pas";
    /// <summary>Séance de révision composée à la volée (progression par geste).</summary>
    public const string ReviewModuleId = "revision";
    /// <summary>Leçon tirée d'un texte collé par l'utilisateur.</summary>
    public const string CustomTextModuleId = "texte";

    /// <summary>Exercice composé à la volée : sa progression ne compte que par geste,
    /// jamais par exercice (L-13).</summary>
    public static bool IsComposed(string moduleId) =>
        StringComparer.Ordinal.Equals(moduleId, ReviewModuleId) ||
        StringComparer.Ordinal.Equals(moduleId, CustomTextModuleId);

    public static LessonCatalog LoadFromResource(string resourceName = LessonsResourceName)
    {
        using var stream = typeof(LessonCatalogLoader).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new FileNotFoundException($"Ressource '{resourceName}' introuvable dans l'assemblage.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return Parse(reader.ReadToEnd());
    }

    public static LessonCatalog Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var modules = new List<LessonModule> { BuildInitiationModule() };

        foreach (var moduleEl in doc.RootElement.GetProperty("modules").EnumerateArray())
        {
            string moduleId = RequiredString(moduleEl, "id");
            var lessons = new List<LessonLesson>();

            foreach (var lessonEl in moduleEl.GetProperty("lessons").EnumerateArray())
            {
                string lessonId = RequiredString(lessonEl, "id");
                var characters = new List<string>();
                if (lessonEl.TryGetProperty("characters", out var charsEl) && charsEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var ch in charsEl.EnumerateArray())
                        characters.Add(ch.GetString() ?? "");
                }

                var exercises = new List<LessonExercise>();
                int exerciseIndex = 0;
                foreach (var exerciseEl in lessonEl.GetProperty("exercises").EnumerateArray())
                {
                    string type = RequiredString(exerciseEl, "type");
                    if (!StringComparer.Ordinal.Equals(type, "practice"))
                        throw new NotSupportedException($"Type d'exercice non supporté en v1: {type}");

                    exercises.Add(new LessonExercise(
                        moduleId,
                        lessonId,
                        exerciseIndex,
                        type,
                        PickText(RequiredString(exerciseEl, "instruction"), OptionalString(exerciseEl, "instructionEn")),
                        RequiredString(exerciseEl, "content"),
                        LessonTypingMode.Flexible));
                    exerciseIndex++;
                }

                lessons.Add(new LessonLesson(
                    moduleId,
                    lessonId,
                    PickText(RequiredString(lessonEl, "title"), OptionalString(lessonEl, "titleEn")),
                    ExpandPlaceholders(PickText(OptionalString(lessonEl, "description"), OptionalString(lessonEl, "descriptionEn"))),
                    characters,
                    exercises));
            }

            modules.Add(new LessonModule(
                moduleId,
                PickText(RequiredString(moduleEl, "title"), OptionalString(moduleEl, "titleEn")),
                ExpandPlaceholders(PickText(OptionalString(moduleEl, "description"), OptionalString(moduleEl, "descriptionEn"))),
                OptionalString(moduleEl, "icon"),
                isSynthetic: false,
                lessons));
        }

        return new LessonCatalog(modules);
    }

    /// <summary>Empreinte d'un exercice : type et texte à taper, indépendante de la langue
    /// et de la formulation de la consigne (audit UX du 09/11, L1). Le préfixe « v2 » la
    /// distingue des empreintes d'avant.</summary>
    internal static string ComputeExerciseHash(string type, string content) =>
        Sha256Hex(string.Join('\u001F', "v2", type, content.Replace("\r\n", "\n")));

    /// <summary>Empreinte des versions d'avant le 09/11/2026, consigne traduite incluse :
    /// sert seulement à reconnaître les progressions déjà enregistrées.</summary>
    internal static string ComputeLegacyExerciseHash(string type, string instruction, string content) =>
        Sha256Hex(string.Join('\u001F', type, instruction, content.Replace("\r\n", "\n")));

    private static string Sha256Hex(string normalized) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized))).ToLowerInvariant();

    private static LessonModule BuildInitiationModule()
    {
        var exercises = new List<LessonExercise>
        {
            // Les huit exercices du tutoriel (TutorialSteps), avec les consignes des Leçons.
            InitiationExercise(0, L.Lessons_Init0, TutorialSteps.All[0].Target),
            InitiationExercise(1, L.Lessons_Init1, TutorialSteps.All[1].Target),
            InitiationExercise(2, L.Lessons_Init2, TutorialSteps.All[2].Target),
            InitiationExercise(3, L.Lessons_Init3, TutorialSteps.All[3].Target),
            InitiationExercise(4, L.Lessons_Init4, TutorialSteps.All[4].Target),
            InitiationExercise(5, L.Lessons_Init5, TutorialSteps.All[5].Target),
            InitiationExercise(6, L.Lessons_Init6, TutorialSteps.All[6].Target),
            InitiationExercise(7, L.Lessons_Init7, TutorialSteps.All[7].Target)
        };

        var lesson = new LessonLesson(
            InitiationModuleId,
            InitiationLessonId,
            L.Lessons_InitiationLessonTitle,
            L.Lessons_InitiationLessonDesc,
            Array.Empty<string>(),
            exercises);

        return new LessonModule(
            InitiationModuleId,
            L.Lessons_InitiationModuleTitle,
            L.Lessons_InitiationModuleDesc,
            "🚀",
            isSynthetic: true,
            new[] { lesson });
    }

    private static LessonExercise InitiationExercise(int index, string instruction, string content)
    {
        return new LessonExercise(
            InitiationModuleId,
            InitiationLessonId,
            index,
            "practice",
            instruction,
            content,
            LessonTypingMode.Strict);
    }

    private static string RequiredString(JsonElement element, string property)
    {
        if (element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String)
            return value.GetString() ?? "";
        throw new JsonException($"Propriété string requise manquante: {property}");
    }

    private static string OptionalString(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";
    }

    /// <summary>
    /// Choisit le texte anglais si l'UI est en anglais ET que le champ EN existe, sinon
    /// retombe sur le français (fallback silencieux — jamais de crash sur champ manquant).
    /// </summary>
    private static string PickText(string fr, string en)
    {
        return L.IsEnglish && !string.IsNullOrEmpty(en) ? en : fr;
    }

    /// <summary>Texte affiché d'une consigne ou d'une description : {ALTGR}, {SHIFT} et {CAPS}
    /// deviennent le nom des touches dans la langue de l'app (« Alt Gr » : <see cref="L.AltGrKey"/>).
    /// La consigne brute reste dans <see cref="LessonExercise.Instruction"/> : les empreintes
    /// d'avant le 09/11/2026 l'incluaient.</summary>
    internal static string ExpandPlaceholders(string text) => text
        .Replace("{ALTGR}", L.AltGrKey, StringComparison.OrdinalIgnoreCase)
        .Replace("{SHIFT}", L.LessonsWin_PlaceholderShift, StringComparison.OrdinalIgnoreCase)
        .Replace("{CAPS}", L.LessonsWin_PlaceholderCaps, StringComparison.OrdinalIgnoreCase);
}
