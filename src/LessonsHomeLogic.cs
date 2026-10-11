// Accueil des Leçons (direction B, choisie par Antoine le 11/10) : la logique pure. Ce qui est
// échu, où un caractère se range sur la carte, les séances composées (révision, touche), le
// marquage d'un texte collé. Le dessin est dans LessonsWindow.Home*.cs ; rien ici ne touche
// une fenêtre, le disque ou l'horloge : tout arrive en paramètre.
using System.Globalization;
using System.Text;

namespace AZERTYGlobal;

/// <summary>Les quatre couches d'une touche sur la carte (les légendes du clavier papier).</summary>
internal enum HomeLayer { Base, Shift, AltGr, ShiftAltGr }

/// <summary>
/// Où un caractère à geste se range sur la carte : la touche et la couche de son premier temps
/// utile. Un caractère direct va à sa propre touche ; un caractère de touche morte à la touche
/// qui l'arme (ɓ à la touche de l'accent aigu, pas à B) ; Verr. Maj. n'est qu'un préambule
/// (« É » va à la légende de base de la touche 2). <see cref="Step"/> est ce premier temps.
/// </summary>
internal readonly record struct HomeKeyPlace(uint Scancode, HomeLayer Layer, GestureStep Step);

/// <summary>Un caractère suivi par geste, sa place sur la carte et son geste complet.</summary>
internal sealed record HomeCharacter(string Character, HomeKeyPlace Place, IReadOnlyList<GestureStep> Steps);

/// <summary>Les caractères à geste de l'index, rangés par touche. Construit une fois.</summary>
internal sealed class HomeKeyCatalog
{
    private readonly Dictionary<uint, List<HomeCharacter>> _byKey = new();
    private readonly Dictionary<string, HomeCharacter> _byCharacter = new(StringComparer.Ordinal);

    private HomeKeyCatalog() { }

    public IReadOnlyList<HomeCharacter> All { get; private set; } = Array.Empty<HomeCharacter>();

    public IEnumerable<uint> Scancodes => _byKey.Keys;

    public IReadOnlyList<HomeCharacter> OnKey(uint scancode) =>
        _byKey.TryGetValue(scancode, out var list) ? list : Array.Empty<HomeCharacter>();

    public bool TryGet(string character, out HomeCharacter value) => _byCharacter.TryGetValue(character, out value!);

    private static readonly Lazy<HomeKeyCatalog> SharedCatalog = new(() =>
        Build(CharacterIndex.Shared.Entries.Select(entry => entry.Character), Gesture.Of));

    /// <summary>Le catalogue de l'index des caractères.</summary>
    internal static HomeKeyCatalog Shared => SharedCatalog.Value;

    /// <summary>
    /// Les caractères suivis : un seul caractère UTF-16 (la séance tape caractère par
    /// caractère, comme <see cref="LessonProgressStore"/> les compte), au geste qui demande
    /// autre chose que la touche gravée (<see cref="GestureMastery.IsTracked(string, IReadOnlyList{GestureStep})"/>).
    /// L'ordre est celui de la source.
    /// </summary>
    internal static HomeKeyCatalog Build(IEnumerable<string> characters, Func<string, IReadOnlyList<GestureStep>> gestureOf)
    {
        var catalog = new HomeKeyCatalog();
        var all = new List<HomeCharacter>();
        foreach (string character in characters)
        {
            if (character.Length != 1 || catalog._byCharacter.ContainsKey(character)) continue;
            var steps = gestureOf(character);
            if (!GestureMastery.IsTracked(character, steps)) continue;
            if (LessonsHomeLogic.PlaceOf(steps) is not { } place) continue;
            var item = new HomeCharacter(character, place, steps);
            all.Add(item);
            catalog._byCharacter[character] = item;
            if (!catalog._byKey.TryGetValue(place.Scancode, out var list))
                catalog._byKey[place.Scancode] = list = new List<HomeCharacter>();
            list.Add(item);
        }
        catalog.All = all;
        return catalog;
    }
}

/// <summary>Ce que la carte montre d'une touche.</summary>
internal enum HomeKeyKind
{
    /// <summary>Rien à suivre : lettres seules, Espace, touches de fonction.</summary>
    Nothing,
    /// <summary>Des caractères à geste, aucun jamais tapé.</summary>
    Unseen,
    /// <summary>Au moins un caractère déjà tapé : la touche porte un niveau.</summary>
    Seen,
}

/// <summary>
/// État d'une touche pour la carte de maîtrise (lecture « un niveau par touche »).
/// <see cref="Level"/> : boîte moyenne des caractères déjà tapés sur 5 ; <see cref="Due"/> :
/// combien sont à revoir (la corne du coin) ; <see cref="Followed"/> : combien la touche en suit.
/// </summary>
internal sealed record HomeKeyState(HomeKeyKind Kind, double Level, int Due, int Seen, int Followed);

/// <summary>Un caractère dans le détail d'une touche. <see cref="Box"/> vaut -1 quand il n'a jamais
/// été tapé. <see cref="Then"/> : le reste du geste après le premier temps (« B », « Maj O »).</summary>
internal sealed record HomeCharLine(string Character, int Box, bool Due, string Then);

/// <summary>Une couche d'une touche : ses caractères les plus utiles d'abord, et combien d'autres
/// restent. <see cref="Seen"/> et <see cref="Due"/> comptent sur la couche entière.</summary>
internal sealed record HomeLayerDetail(HomeLayer Layer, GestureStep Step, IReadOnlyList<HomeCharLine> Lines,
    int Hidden, int Seen, int Due, int Total);

/// <summary>Une séance composée : l'exercice (null si aucun mot ne convient), les caractères visés,
/// ceux que l'exercice fait vraiment passer, ceux qu'il ne couvre pas, le caractère nouveau d'une
/// séance de touche, et quelques mots pour le dire à l'écran.</summary>
internal sealed record HomeSession(LessonExercise? Exercise, IReadOnlyList<string> Characters,
    IReadOnlyList<string> Covered, IReadOnlyList<string> Missing, string? NewCharacter, IReadOnlyList<string> Words);

/// <summary>Un morceau d'un texte collé : ce que la leçon en fera.</summary>
internal enum HomeTextKind
{
    /// <summary>Tapable, sans geste à suivre (lettre, espace).</summary>
    Plain,
    /// <summary>Tapable, avec un geste à suivre : marqué dans le champ.</summary>
    Gesture,
    /// <summary>La disposition ne le tape pas : la leçon le saute.</summary>
    Skipped,
    /// <summary>Fin de ligne.</summary>
    Break,
}

internal readonly record struct HomeTextItem(string Element, HomeTextKind Kind);

/// <summary>Une ligne du bilan d'un texte collé : un caractère à geste, combien de fois il
/// revient, sa boîte (-1 : jamais tapé) et s'il est à revoir aujourd'hui.</summary>
internal sealed record HomeTextBilanLine(string Character, int Count, int Box, bool Due);

internal static class LessonsHomeLogic
{
    /// <summary>Nombre de caractères d'une séance de révision : les plus fragiles d'abord.</summary>
    internal const int ReviewCharacters = 8;
    /// <summary>Caractères déjà vus repris par une séance de touche, avant le nouveau.</summary>
    internal const int KeySessionSeen = 5;
    /// <summary>Lignes d'une couche dans le détail d'une touche.</summary>
    internal const int DetailLines = 3;
    /// <summary>Répétitions d'un caractère sans mot dans les leçons (ligne d'entraînement).</summary>
    internal const int DrillRepeats = 6;
    /// <summary>Un texte collé au-delà est coupé : la leçon n'en garde que douze lignes.</summary>
    internal const int MaxTextLength = 4000;

    // ── Rattachement d'un caractère à une touche ─────────────────────────

    /// <summary>La touche et la couche du premier temps utile d'un geste (Verr. Maj. écarté) ;
    /// null si le geste est vide ou si la touche n'est pas sur le clavier dessiné.</summary>
    internal static HomeKeyPlace? PlaceOf(IReadOnlyList<GestureStep> steps)
    {
        foreach (var step in steps)
        {
            if (step.KeyCode == Gesture.CapsLockCode) continue;
            if (!VirtualKeyboard.KeyCodeToScancode.TryGetValue(step.KeyCode, out uint scancode)) return null;
            var layer = step.AltGr
                ? (step.Shift ? HomeLayer.ShiftAltGr : HomeLayer.AltGr)
                : (step.Shift ? HomeLayer.Shift : HomeLayer.Base);
            return new HomeKeyPlace(scancode, layer, step);
        }
        return null;
    }

    /// <summary>Le reste du geste après le premier temps utile : « B » (ɓ : touche morte, puis B),
    /// « Maj O » ; vide pour un geste d'un seul temps.</summary>
    internal static string ThenLabel(HomeCharacter character)
    {
        int home = -1;
        for (int i = 0; i < character.Steps.Count; i++)
            if (character.Steps[i].KeyCode != Gesture.CapsLockCode) { home = i; break; }
        if (home < 0 || home == character.Steps.Count - 1) return "";
        var parts = new List<string>();
        for (int i = home + 1; i < character.Steps.Count; i++)
        {
            var step = character.Steps[i];
            string label = step.ShortLabel;
            if (step.AltGr) label = "AltGr " + label;
            if (step.Shift) label = L.Gesture_LineShift + " " + label;
            parts.Add(label);
        }
        return string.Join(' ', parts);
    }

    // ── Carte de maîtrise ────────────────────────────────────────────────

    /// <summary>L'état de chaque touche qui suit au moins un caractère ; les autres n'ont pas
    /// d'entrée (rien à suivre). Un caractère compte comme tapé quand le magasin en a gardé une
    /// entrée avec au moins une demande.</summary>
    internal static IReadOnlyDictionary<uint, HomeKeyState> KeyStates(HomeKeyCatalog catalog,
        IReadOnlyDictionary<string, GestureMasteryEntry> entries, DateTimeOffset now)
    {
        var states = new Dictionary<uint, HomeKeyState>();
        foreach (uint scancode in catalog.Scancodes)
        {
            var followed = catalog.OnKey(scancode);
            int seen = 0, due = 0;
            double boxes = 0;
            foreach (var character in followed)
            {
                if (!entries.TryGetValue(character.Character, out var entry) || entry.Attempts <= 0) continue;
                seen++;
                boxes += entry.Box;
                if (entry.IsDue(now)) due++;
            }
            states[scancode] = seen == 0
                ? new HomeKeyState(HomeKeyKind.Unseen, 0, 0, 0, followed.Count)
                : new HomeKeyState(HomeKeyKind.Seen, Math.Clamp(boxes / seen / GestureMastery.MaxBox, 0, 1), due, seen, followed.Count);
        }
        return states;
    }

    /// <summary>La touche à montrer d'abord : celle qui a le plus de caractères à revoir, sinon
    /// celle qui en a le plus de tapés, sinon la première qui suit quelque chose (ordre des
    /// scancodes, stable). Null si la carte ne suit rien.</summary>
    internal static uint? DefaultKey(IReadOnlyDictionary<uint, HomeKeyState> states)
    {
        uint? best = null;
        foreach (var (scancode, state) in states.OrderBy(pair => pair.Key))
        {
            if (best is not { } current) { best = scancode; continue; }
            var other = states[current];
            if (state.Due > other.Due || (state.Due == other.Due && state.Seen > other.Seen))
                best = scancode;
        }
        return best;
    }

    /// <summary>Le détail d'une touche, une entrée par couche qui suit quelque chose, dans
    /// l'ordre Base, Maj, AltGr, Maj+AltGr. Dans chaque couche : à revoir d'abord (boîte la plus
    /// basse), puis déjà tapés, puis jamais tapés dans l'ordre de l'index.</summary>
    internal static IReadOnlyList<HomeLayerDetail> KeyDetail(HomeKeyCatalog catalog,
        IReadOnlyDictionary<string, GestureMasteryEntry> entries, uint scancode, DateTimeOffset now,
        int maxLines = DetailLines)
    {
        var result = new List<HomeLayerDetail>();
        foreach (var layer in Enum.GetValues<HomeLayer>())
        {
            var inLayer = catalog.OnKey(scancode).Where(character => character.Place.Layer == layer).ToList();
            if (inLayer.Count == 0) continue;
            var ordered = inLayer
                .Select((character, index) =>
                {
                    entries.TryGetValue(character.Character, out var entry);
                    bool seen = entry is { Attempts: > 0 };
                    bool due = seen && entry!.IsDue(now);
                    return (character, index, entry: seen ? entry : null, due);
                })
                .OrderBy(item => item.entry == null ? 2 : item.due ? 0 : 1)
                .ThenBy(item => item.entry?.Box ?? 0)
                .ThenBy(item => item.index)
                .ToList();
            var lines = ordered.Take(maxLines)
                .Select(item => new HomeCharLine(item.character.Character, item.entry?.Box ?? -1, item.due,
                    ThenLabel(item.character)))
                .ToArray();
            result.Add(new HomeLayerDetail(layer, inLayer[0].Place.Step, lines, ordered.Count - lines.Length,
                ordered.Count(item => item.entry != null), ordered.Count(item => item.due), ordered.Count));
        }
        return result;
    }

    // ── Séances composées ────────────────────────────────────────────────

    /// <summary>Les caractères qui figurent dans les textes des leçons : ceux dont
    /// <see cref="LessonReviewBuilder"/> peut tirer un mot.</summary>
    internal static HashSet<char> LessonCharacters(LessonCatalog catalog)
    {
        var characters = new HashSet<char>();
        foreach (var exercise in catalog.Exercises)
        {
            if (LessonCatalogLoader.IsComposed(exercise.ModuleId)) continue;
            foreach (char c in exercise.Content) characters.Add(c);
        }
        return characters;
    }

    /// <summary>
    /// La révision du jour : les caractères échus (les plus fragiles d'abord, au plus
    /// <see cref="ReviewCharacters"/>) dans des mots des leçons. Ceux qu'aucun mot ne porte sont
    /// dits dans <see cref="HomeSession.Missing"/> : ils viendront avec de nouveaux mots.
    /// </summary>
    internal static HomeSession BuildReview(LessonCatalog catalog, IReadOnlyList<GestureMasteryEntry> due,
        string instruction, int seed)
    {
        var characters = due.Take(ReviewCharacters).Select(entry => entry.Character).ToArray();
        var exercise = LessonReviewBuilder.Build(catalog, characters, instruction, seed);
        var covered = exercise == null
            ? Array.Empty<string>()
            : characters.Where(c => exercise.Content.Contains(c, StringComparison.Ordinal)).ToArray();
        var missing = characters.Where(c => !covered.Contains(c)).ToArray();
        return new HomeSession(exercise, characters, covered, missing, null, SampleWords(exercise, covered));
    }

    /// <summary>
    /// La séance d'une touche : « les caractères déjà vus + un nouveau » (décision du 08/10). Les
    /// vus : ceux de la touche déjà tapés, à revoir d'abord, au plus <see cref="KeySessionSeen"/>.
    /// Le nouveau : le premier caractère de la touche jamais tapé, de préférence un que les
    /// textes des leçons portent. Un caractère que ces textes ne portent pas reçoit une ligne
    /// d'entraînement (lui seul, <see cref="DrillRepeats"/> fois, séparé d'espaces) : sans elle
    /// la plupart des signes nouveaux n'auraient aucune séance.
    /// </summary>
    internal static HomeSession BuildKeySession(LessonCatalog catalog, HomeKeyCatalog keys,
        IReadOnlyDictionary<string, GestureMasteryEntry> entries, uint scancode, DateTimeOffset now,
        string instruction, int seed)
    {
        var onKey = keys.OnKey(scancode);
        var seen = onKey
            .Select((character, index) =>
            {
                entries.TryGetValue(character.Character, out var entry);
                return (character, index, entry: entry is { Attempts: > 0 } ? entry : null);
            })
            .Where(item => item.entry != null)
            .OrderBy(item => item.entry!.IsDue(now) ? 0 : 1)
            .ThenBy(item => item.entry!.Box)
            .ThenBy(item => item.index)
            .Take(KeySessionSeen)
            .Select(item => item.character.Character)
            .ToList();

        var lessonCharacters = LessonCharacters(catalog);
        var unseen = onKey
            .Where(character => !entries.TryGetValue(character.Character, out var entry) || entry.Attempts <= 0)
            .Select(character => character.Character)
            .ToList();
        string? newCharacter = unseen.FirstOrDefault(c => lessonCharacters.Contains(c[0])) ?? unseen.FirstOrDefault();

        var characters = new List<string>(seen);
        if (newCharacter != null) characters.Add(newCharacter);
        if (characters.Count == 0)
            return new HomeSession(null, characters, Array.Empty<string>(), Array.Empty<string>(), null, Array.Empty<string>());

        var built = LessonReviewBuilder.Build(catalog, characters, instruction, seed);
        var covered = built == null
            ? Array.Empty<string>()
            : characters.Where(c => built.Content.Contains(c, StringComparison.Ordinal)).ToArray();
        // Le nouveau d'abord, puis les vus que les mots n'ont pas portés.
        var drilled = characters.Where(c => !covered.Contains(c))
            .OrderBy(c => c == newCharacter ? 0 : 1)
            .Take(3)
            .ToArray();
        var lines = built?.Lines.ToList() ?? new List<string>();
        foreach (string c in drilled)
            lines.Add(string.Join(' ', Enumerable.Repeat(c, DrillRepeats)));
        if (lines.Count == 0)
            return new HomeSession(null, characters, covered, characters, newCharacter, Array.Empty<string>());

        var exercise = new LessonExercise(LessonCatalogLoader.ReviewModuleId, "seance", 0, "practice", instruction,
            string.Join('\n', lines), LessonTypingMode.Flexible);
        var missing = characters.Where(c => !covered.Contains(c) && !drilled.Contains(c)).ToArray();
        return new HomeSession(exercise, characters, covered.Concat(drilled).ToArray(), missing, newCharacter,
            SampleWords(built, covered));
    }

    /// <summary>Jusqu'à trois mots de la séance, un par caractère couvert, pour dire à l'écran
    /// ce qu'on va taper.</summary>
    private static IReadOnlyList<string> SampleWords(LessonExercise? exercise, IReadOnlyList<string> covered)
    {
        if (exercise == null) return Array.Empty<string>();
        var words = exercise.Content.Split(new[] { ' ', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var picked = new List<string>();
        foreach (string c in covered)
        {
            string? word = words.FirstOrDefault(w => w.Contains(c, StringComparison.Ordinal) && !picked.Contains(w));
            if (word != null) picked.Add(word);
            if (picked.Count == 3) break;
        }
        return picked;
    }

    /// <summary>Le jour de la séance : la même révision tout le jour (<see cref="LessonReviewBuilder"/>
    /// tire ses mots d'une graine).</summary>
    internal static int DaySeed(DateTimeOffset now) => now.Year * 10000 + now.Month * 100 + now.Day;

    // ── Texte collé ──────────────────────────────────────────────────────

    /// <summary>La forme que la leçon donnera au texte avant de le découper : les mêmes
    /// normalisations que <see cref="LessonCustomText.Build(string, string, Func{string, bool})"/>
    /// (forme composée, fins de ligne, tabulations).</summary>
    internal static string NormalizeText(string? text) =>
        (text ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Normalize(NormalizationForm.FormC);

    /// <summary>
    /// Le texte morceau par morceau, comme la leçon le lira : chaque grappe (lettre et marques
    /// combinantes) est tapable si <paramref name="isTypable"/> accepte chacun de ses caractères
    /// (et aucune paire de substitution), et porte un geste si l'un d'eux est suivi. C'est
    /// exactement le prédicat de <see cref="LessonCustomText"/> : ce que le champ marque comme
    /// sauté est ce que la leçon retire.
    /// </summary>
    internal static IReadOnlyList<HomeTextItem> AnalyzeText(string? text, Func<string, bool> isTypable,
        Func<char, bool> isTracked)
    {
        var items = new List<HomeTextItem>();
        var elements = StringInfo.GetTextElementEnumerator(NormalizeText(text));
        while (elements.MoveNext())
        {
            string element = elements.GetTextElement();
            if (element == "\n") { items.Add(new HomeTextItem(element, HomeTextKind.Break)); continue; }
            if (element == "\t") { items.Add(new HomeTextItem(" ", HomeTextKind.Plain)); continue; }
            bool typable = !element.Any(char.IsSurrogate) && element.All(c => isTypable(c.ToString()));
            items.Add(new HomeTextItem(element, !typable
                ? HomeTextKind.Skipped
                : element.Any(isTracked) ? HomeTextKind.Gesture : HomeTextKind.Plain));
        }
        return items;
    }

    /// <summary>Les caractères sautés, une fois chacun, dans l'ordre : ce que <see cref="LessonCustomTextResult.Removed"/>
    /// dira après la préparation.</summary>
    internal static IReadOnlyList<string> SkippedElements(IReadOnlyList<HomeTextItem> items) =>
        items.Where(item => item.Kind == HomeTextKind.Skipped).Select(item => item.Element)
            .Distinct(StringComparer.Ordinal).ToArray();

    /// <summary>Le bilan de ce qu'on va taper : chaque caractère à geste de <paramref name="content"/>
    /// (le texte retenu par la leçon), dans l'ordre où il apparaît, avec son nombre de passages,
    /// sa boîte et s'il est à revoir.</summary>
    internal static IReadOnlyList<HomeTextBilanLine> TextBilan(string content,
        IReadOnlyDictionary<string, GestureMasteryEntry> entries, DateTimeOffset now, Func<char, bool> isTracked)
    {
        var counts = new Dictionary<char, int>();
        var order = new List<char>();
        foreach (char c in content)
        {
            if (!isTracked(c)) continue;
            if (counts.TryGetValue(c, out int n)) counts[c] = n + 1;
            else { counts[c] = 1; order.Add(c); }
        }
        return order.Select(c =>
        {
            entries.TryGetValue(c.ToString(), out var entry);
            bool seen = entry is { Attempts: > 0 };
            return new HomeTextBilanLine(c.ToString(), counts[c], seen ? entry!.Box : -1, seen && entry!.IsDue(now));
        }).ToArray();
    }
}
