// Défi du jour — sélection des séances d'entraînement (v1.2.0).
//
// Décisions du 2026-07-22 (+ arbitrages du 2026-07-29) :
// - progression hybride : séquence individuelle pendant le désapprentissage des
//   5 changements, puis « Défi du jour » commun (extrait seedé par la date, identique
//   pour tous, partageable) ;
// - pipeline inversé : l'échauffement (caractères individuels, 5 répétitions chacun)
//   est DÉRIVÉ de l'extrait choisi, jamais l'inverse ;
// - extraits en français uniquement pour la phase séquence ; l'international arrive
//   dans les défis communs ;
// - attribution : « d'après X » systématique quand l'extrait vient d'une œuvre
//   (domaine public uniquement) ; les extraits CC0 et maison n'en portent pas.
using System.Text.Json;

namespace AZERTYGlobal;

/// <summary>Un extrait de la banque defi-corpus.json.</summary>
sealed class ChallengeExtract
{
    public int Id { get; init; }
    public string Text { get; init; } = "";
    /// <summary>Attribution « Auteur, « Œuvre » » — null pour CC0 et extraits maison.</summary>
    public string? Credit { get; init; }
    public string Lang { get; init; } = "fr";
    public IReadOnlyList<string> Targets { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Cats { get; init; } = Array.Empty<string>();
}

/// <summary>Une séance prête à jouer : échauffement dérivé + extrait.</summary>
sealed class ChallengeSession
{
    /// <summary>Caractères de l'échauffement, chacun répété <see cref="WarmupRepetitions"/> fois.</summary>
    public IReadOnlyList<string> WarmupChars { get; init; } = Array.Empty<string>();
    public ChallengeExtract Extract { get; init; } = new();
    /// <summary>true = séance de la séquence de désapprentissage, false = défi commun.</summary>
    public bool IsSequencePhase { get; init; }
    /// <summary>Index 0..4 dans la séquence (n'a de sens que si IsSequencePhase).</summary>
    public int SequenceIndex { get; init; }
}

static class DailyChallenge
{
    public const int WarmupRepetitions = 5; // décision 2026-07-29
    public const int SequenceLength = 5;    // les 5 changements

    // Séquence de désapprentissage : une séance par changement, dans l'ordre produit
    // (cf. CONTEXT_AZERTY_GLOBAL « 5 Changements »). Chaque étape = catégorie d'extrait
    // + caractères d'échauffement prioritaires.
    private static readonly (string Cat, string[] Warmup)[] Sequence =
    {
        ("caps",    new[] { "É", "È", "Ç", "À" }),           // 1. Verrouillage Majuscule intelligent
        ("point",   new[] { ".", ";" }),                      // 2. Point en accès direct
        ("at",      new[] { "@", "#" }),                      // 3. @ et # sur la touche ²
        ("prog",    new[] { "{", "}", "[", "]", "|", "\\" }), // 4. Symboles de programmation
        ("accents", new[] { "´", "`", "ù" }),                 // 5. Accents internationaux sur ù
    };

    private static List<ChallengeExtract>? _extracts;
    private static readonly object _lock = new();

    private static List<ChallengeExtract> Extracts
    {
        get
        {
            lock (_lock)
            {
                _extracts ??= Load();
                return _extracts;
            }
        }
    }

    private static List<ChallengeExtract> Load()
    {
        var list = new List<ChallengeExtract>();
        try
        {
            using var stream = typeof(DailyChallenge).Assembly.GetManifestResourceStream("defi-corpus.json");
            if (stream == null) return list;
            using var reader = new StreamReader(stream);
            using var doc = JsonDocument.Parse(reader.ReadToEnd());
            foreach (var e in doc.RootElement.GetProperty("extracts").EnumerateArray())
            {
                list.Add(new ChallengeExtract
                {
                    Id = e.GetProperty("id").GetInt32(),
                    Text = e.GetProperty("text").GetString() ?? "",
                    Credit = e.TryGetProperty("credit", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() : null,
                    Lang = e.TryGetProperty("lang", out var l) ? l.GetString() ?? "fr" : "fr",
                    Targets = ReadStrings(e, "targets"),
                    Cats = ReadStrings(e, "cats"),
                });
            }
        }
        catch (Exception ex)
        {
            ConfigManager.Log("DailyChallenge.Load", ex);
        }
        return list;
    }

    private static string[] ReadStrings(JsonElement e, string prop)
    {
        if (!e.TryGetProperty(prop, out var arr) || arr.ValueKind != JsonValueKind.Array)
            return Array.Empty<string>();
        return arr.EnumerateArray()
            .Select(x => x.GetString())
            .Where(s => !string.IsNullOrEmpty(s))
            .Select(s => s!)
            .ToArray();
    }

    /// <summary>Nombre d'extraits chargés (0 si la ressource manque — pas de séance possible).</summary>
    public static int ExtractCount => Extracts.Count;

    /// <summary>
    /// Construit la séance du jour : étape de la séquence de désapprentissage tant que
    /// les 5 changements ne sont pas parcourus (ConfigManager.TrainingSequenceIndex),
    /// puis défi commun seedé par la date (identique pour tous — partageable).
    /// Retourne null si la banque est vide.
    /// </summary>
    public static ChallengeSession? SessionFor(DateOnly date, uint sequenceIndex)
    {
        var extracts = Extracts;
        if (extracts.Count == 0) return null;

        if (sequenceIndex < SequenceLength)
        {
            var (cat, warmup) = Sequence[(int)sequenceIndex];
            // Extrait FR de la catégorie, choisi de façon stable par (étape, date) —
            // rejouer la même séance le même jour retombe sur le même extrait.
            var pool = extracts.Where(e => e.Lang == "fr" && e.Cats.Contains(cat)).ToList();
            if (pool.Count == 0)
                pool = extracts.Where(e => e.Lang == "fr").ToList();
            var extract = pool[StableIndex(date, (int)sequenceIndex + 1, pool.Count)];
            return new ChallengeSession
            {
                WarmupChars = DeriveWarmup(extract, warmup),
                Extract = extract,
                IsSequencePhase = true,
                SequenceIndex = (int)sequenceIndex,
            };
        }

        // Défi commun : extrait du jour, identique pour tous les utilisateurs.
        var daily = extracts[StableIndex(date, 0, extracts.Count)];
        return new ChallengeSession
        {
            WarmupChars = DeriveWarmup(daily, preferred: null),
            Extract = daily,
            IsSequencePhase = false,
            SequenceIndex = (int)Math.Min(sequenceIndex, int.MaxValue),
        };
    }

    /// <summary>
    /// Échauffement dérivé de l'extrait (pipeline inversé) : les caractères cibles
    /// annotés de l'extrait, l'éventuelle liste préférée de l'étape d'abord (dans
    /// l'ordre pédagogique), 3 à 6 caractères au total.
    /// </summary>
    internal static IReadOnlyList<string> DeriveWarmup(ChallengeExtract extract, string[]? preferred)
    {
        var result = new List<string>();
        if (preferred != null)
            foreach (var c in preferred)
                if (extract.Targets.Contains(c) && !result.Contains(c))
                    result.Add(c);
        foreach (var c in extract.Targets)
        {
            if (result.Count >= 6) break;
            if (!result.Contains(c)) result.Add(c);
        }
        // Extrait sans cible annotée (libre) : échauffement sur les caractères accentués
        // français les plus fréquents de l'extrait, à défaut é/è.
        if (result.Count == 0)
        {
            foreach (var c in extract.Text.Where(ch => ch is 'é' or 'è' or 'ê' or 'à' or 'ç' or 'ù').Distinct().Take(4))
                result.Add(c.ToString());
            if (result.Count == 0) { result.Add("é"); result.Add("è"); }
        }
        return result;
    }

    /// <summary>Identifiant du module synthétique dans le catalogue des leçons.</summary>
    public const string ModuleId = "defi";

    /// <summary>
    /// Construit le module « Défi du jour » du catalogue des leçons : une leçon du jour,
    /// deux exercices — échauffement (5 répétitions par caractère cible, dérivées de
    /// l'extrait) puis l'extrait lui-même. Attribution « d'après X » portée dans la
    /// consigne quand l'extrait vient d'une œuvre. Retourne null si la banque est vide.
    /// </summary>
    public static LessonModule? BuildModule(DateOnly date, uint sequenceIndex)
    {
        var session = SessionFor(date, sequenceIndex);
        if (session == null) return null;

        string lessonId = $"defi-{date:yyyyMMdd}";
        // Échauffement : une ligne par caractère cible, 5 répétitions espacées.
        string warmupContent = string.Join("\n",
            session.WarmupChars.Select(c => string.Join(" ", Enumerable.Repeat(c, WarmupRepetitions))));

        string extractInstruction = session.Extract.Credit != null
            ? L.Challenge_ExtractInstructionCredited(session.Extract.Credit)
            : L.Challenge_ExtractInstruction;

        var exercises = new List<LessonExercise>
        {
            new(ModuleId, lessonId, 0, "warmup",
                L.Challenge_WarmupInstruction, warmupContent, LessonTypingMode.Strict),
            new(ModuleId, lessonId, 1, "extract",
                extractInstruction, WrapText(session.Extract.Text, 62), LessonTypingMode.Flexible),
        };

        string lessonTitle = session.IsSequencePhase
            ? L.Challenge_SequenceLessonTitle(session.SequenceIndex + 1, SequenceLength)
            : L.Challenge_DailyLessonTitle(date);

        var lesson = new LessonLesson(ModuleId, lessonId, lessonTitle,
            L.Challenge_LessonDescription, session.WarmupChars.ToArray(), exercises);

        return new LessonModule(ModuleId, L.Challenge_ModuleTitle,
            L.Challenge_ModuleDescription, "🎯", isSynthetic: true,
            new[] { lesson });
    }

    /// <summary>Coupe un extrait en lignes ≤ maxLen aux frontières de mots (affichage leçon).</summary>
    internal static string WrapText(string text, int maxLen)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var lines = new List<string>();
        var current = "";
        foreach (var w in words)
        {
            if (current.Length == 0) current = w;
            else if (current.Length + 1 + w.Length <= maxLen) current += " " + w;
            else { lines.Add(current); current = w; }
        }
        if (current.Length > 0) lines.Add(current);
        return string.Join("\n", lines);
    }

    /// <summary>
    /// Index stable dans [0, count) dérivé de (date, salt) — même valeur pour tous,
    /// aucune horloge aléatoire (défi commun partageable, séances rejouables).
    /// </summary>
    internal static int StableIndex(DateOnly date, int salt, int count)
    {
        if (count <= 0) return 0;
        uint h = (uint)(date.Year * 372 + date.Month * 31 + date.Day) * 2654435761u;
        h ^= (uint)salt * 40503u;
        h ^= h >> 15; h *= 2246822519u; h ^= h >> 13;
        return (int)(h % (uint)count);
    }
}
