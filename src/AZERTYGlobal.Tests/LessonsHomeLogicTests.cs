using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>Accueil des Leçons (direction B) : la logique pure, sans fenêtre. Les caractères
/// viennent de l'index de la disposition, jamais d'une liste écrite ici.</summary>
public class LessonsHomeLogicTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);

    private static HomeKeyCatalog Keys => HomeKeyCatalog.Shared;

    private static GestureMasteryEntry Entry(string character, int box, int attempts = 3, DateTimeOffset? due = null) =>
        new(character) { Box = box, Attempts = attempts, DueUtc = due ?? T0.AddDays(2), LastReviewUtc = T0.AddDays(-1) };

    private static Dictionary<string, GestureMasteryEntry> Entries(params GestureMasteryEntry[] entries) =>
        entries.ToDictionary(entry => entry.Character);

    /// <summary>Une touche qui suit au moins <paramref name="minimum"/> caractères.</summary>
    private static uint KeyWith(int minimum) =>
        Keys.Scancodes.OrderBy(scancode => scancode).First(scancode => Keys.OnKey(scancode).Count >= minimum);

    [Fact]
    public void LeCatalogueDesTouches_RangeChaqueCaractereSurUneToucheDuClavierDessine()
    {
        Assert.NotEmpty(Keys.All);
        var drawn = KeyboardRenderer.VisualKeys.Where(key => key.Scancode != 0).Select(key => key.Scancode).ToHashSet();
        foreach (var character in Keys.All)
        {
            Assert.Equal(1, character.Character.Length);
            Assert.Contains(character.Place.Scancode, drawn);
            // Verr. Maj. n'est qu'un préambule : jamais la touche de rattachement.
            Assert.NotEqual(Gesture.CapsLockCode, character.Place.Step.KeyCode);
        }
    }

    [Fact]
    public void KeyStates_SansProgression_TouteToucheSuivieEstJamaisTapee()
    {
        var states = LessonsHomeLogic.KeyStates(Keys, Entries(), T0);

        Assert.NotEmpty(states);
        Assert.All(states.Values, state =>
        {
            Assert.Equal(HomeKeyKind.Unseen, state.Kind);
            Assert.Equal(0, state.Seen);
            Assert.True(state.Followed > 0);
        });
    }

    [Fact]
    public void KeyStates_LeNiveauEstLaBoiteMoyenneDesCaracteresTapes_EtLaCorneCompteLesEchus()
    {
        uint scancode = KeyWith(2);
        var onKey = Keys.OnKey(scancode);
        var entries = Entries(
            Entry(onKey[0].Character, box: 4, due: T0.AddDays(-1)), // échu
            Entry(onKey[1].Character, box: 2));                      // pas échu

        var state = LessonsHomeLogic.KeyStates(Keys, entries, T0)[scancode];

        Assert.Equal(HomeKeyKind.Seen, state.Kind);
        Assert.Equal(2, state.Seen);
        Assert.Equal(1, state.Due);
        Assert.Equal(onKey.Count, state.Followed);
        Assert.Equal((4 + 2) / 2.0 / GestureMastery.MaxBox, state.Level, 6);
    }

    [Fact]
    public void KeyStates_UnCaractereSansDemandeNeComptePasCommeTape()
    {
        uint scancode = KeyWith(1);
        var onKey = Keys.OnKey(scancode);

        var state = LessonsHomeLogic.KeyStates(Keys, Entries(Entry(onKey[0].Character, box: 3, attempts: 0)), T0)[scancode];

        Assert.Equal(HomeKeyKind.Unseen, state.Kind);
    }

    [Fact]
    public void DefaultKey_PrendLaToucheAvecLePlusDeCaracteresARevoir()
    {
        var keys = Keys.Scancodes.OrderBy(scancode => scancode).Take(3).ToArray();
        var due = Entry(Keys.OnKey(keys[2])[0].Character, box: 1, due: T0.AddDays(-1));
        var states = LessonsHomeLogic.KeyStates(Keys, Entries(due), T0);

        Assert.Equal(keys[2], LessonsHomeLogic.DefaultKey(states));
    }

    [Fact]
    public void DefaultKey_SansProgression_PrendLaPremiereToucheSuivie()
    {
        var states = LessonsHomeLogic.KeyStates(Keys, Entries(), T0);

        Assert.Equal(Keys.Scancodes.Min(), LessonsHomeLogic.DefaultKey(states));
        Assert.Null(LessonsHomeLogic.DefaultKey(new Dictionary<uint, HomeKeyState>()));
    }

    [Fact]
    public void KeyDetail_ARevoirDAbord_PuisTapes_PuisJamaisTapes()
    {
        uint scancode = KeyWith(3);
        var detail = LessonsHomeLogic.KeyDetail(Keys, Entries(), scancode, T0, maxLines: 99);
        var layer = detail.First(d => d.Lines.Count >= 3);
        var chars = Keys.OnKey(scancode).Where(c => c.Place.Layer == layer.Layer).Select(c => c.Character).ToArray();
        var entries = Entries(
            Entry(chars[1], box: 2),                               // tapé, pas échu
            Entry(chars[2], box: 1, due: T0.AddDays(-1)));         // échu

        var ordered = LessonsHomeLogic.KeyDetail(Keys, entries, scancode, T0, maxLines: 99)
            .First(d => d.Layer == layer.Layer);

        Assert.Equal(chars[2], ordered.Lines[0].Character);
        Assert.True(ordered.Lines[0].Due);
        Assert.Equal(chars[1], ordered.Lines[1].Character);
        Assert.Equal(2, ordered.Lines[1].Box);
        Assert.Equal(-1, ordered.Lines[2].Box);
        Assert.Equal(chars.Length, ordered.Total);
        Assert.Equal(2, ordered.Seen);
        Assert.Equal(1, ordered.Due);
    }

    [Fact]
    public void KeyDetail_LimiteLesLignesEtCompteLesAutres()
    {
        uint scancode = Keys.Scancodes.OrderBy(s => s).First(s =>
            Keys.OnKey(s).GroupBy(c => c.Place.Layer).Any(g => g.Count() > 1));

        var detail = LessonsHomeLogic.KeyDetail(Keys, Entries(), scancode, T0, maxLines: 1);

        Assert.All(detail, layer =>
        {
            Assert.True(layer.Lines.Count <= 1);
            Assert.Equal(layer.Total - layer.Lines.Count, layer.Hidden);
        });
        Assert.Contains(detail, layer => layer.Hidden > 0);
    }

    // ── Séances composées ────────────────────────────────────────────────

    /// <summary>Un caractère suivi que les textes des leçons portent : la révision peut en tirer un mot.</summary>
    private static string CharacterInLessons(LessonCatalog catalog, int skip = 0) =>
        Keys.All.Select(c => c.Character)
            .Where(c => LessonsHomeLogic.LessonCharacters(catalog).Contains(c[0]))
            .Skip(skip).First();

    [Fact]
    public void BuildReview_LesCaracteresEchusSontDansDesMotsDesLecons()
    {
        var catalog = LessonCatalogLoader.LoadFromResource();
        string character = CharacterInLessons(catalog);
        var due = new[] { Entry(character, box: 1, due: T0.AddDays(-1)) };

        var session = LessonsHomeLogic.BuildReview(catalog, due, "Révision du jour", seed: 20261012);

        Assert.NotNull(session.Exercise);
        Assert.Contains(character, session.Exercise!.Content);
        Assert.Equal(new[] { character }, session.Covered);
        Assert.Empty(session.Missing);
        Assert.Equal(LessonCatalogLoader.ReviewModuleId, session.Exercise.ModuleId);
        Assert.NotEmpty(session.Words);
    }

    [Fact]
    public void BuildReview_RienAReviser_PasDeSeance()
    {
        var catalog = LessonCatalogLoader.LoadFromResource();

        var session = LessonsHomeLogic.BuildReview(catalog, Array.Empty<GestureMasteryEntry>(), "Révision du jour", 1);

        Assert.Null(session.Exercise);
        Assert.Empty(session.Characters);
    }

    [Fact]
    public void BuildReview_AuPlusHuitCaracteres_LesPlusFragilesDAbord()
    {
        var catalog = LessonCatalogLoader.LoadFromResource();
        var due = Keys.All.Take(12).Select((c, i) => Entry(c.Character, box: i, due: T0.AddDays(-1))).ToArray();

        var session = LessonsHomeLogic.BuildReview(catalog, due, "Révision du jour", 1);

        Assert.Equal(LessonsHomeLogic.ReviewCharacters, session.Characters.Count);
        Assert.Equal(due.Take(LessonsHomeLogic.ReviewCharacters).Select(e => e.Character), session.Characters);
    }

    [Fact]
    public void BuildKeySession_LesCaracteresDejaVusEtUnNouveau_PresentsDansLeContenu()
    {
        var catalog = LessonCatalogLoader.LoadFromResource();
        uint scancode = KeyWith(2);
        var onKey = Keys.OnKey(scancode);
        var seen = onKey[0].Character;
        var entries = Entries(Entry(seen, box: 2));

        var session = LessonsHomeLogic.BuildKeySession(catalog, Keys, entries, scancode, T0, "Séance", 20261012);

        Assert.NotNull(session.Exercise);
        Assert.Contains(seen, session.Characters);
        Assert.NotNull(session.NewCharacter);
        Assert.NotEqual(seen, session.NewCharacter);
        Assert.Contains(session.NewCharacter!, onKey.Select(c => c.Character)); // le nouveau est de cette touche
        Assert.Contains(session.NewCharacter!, session.Exercise!.Content);     // et la séance le fait taper
        Assert.Contains(seen, session.Exercise.Content);
        Assert.DoesNotContain(session.NewCharacter!, entries.Keys);
        Assert.Equal(LessonCatalogLoader.ReviewModuleId, session.Exercise.ModuleId);
        Assert.True(LessonCatalogLoader.IsComposed(session.Exercise.ModuleId));
    }

    [Fact]
    public void BuildKeySession_SansCaractereVu_LeNouveauSeulAvecUneLigneDEntrainementSiBesoin()
    {
        var catalog = LessonCatalogLoader.LoadFromResource();
        uint scancode = KeyWith(1);

        var session = LessonsHomeLogic.BuildKeySession(catalog, Keys, Entries(), scancode, T0, "Séance", 1);

        Assert.NotNull(session.NewCharacter);
        Assert.NotNull(session.Exercise);
        Assert.Contains(session.NewCharacter!, session.Exercise!.Content);
    }

    [Fact]
    public void BuildKeySession_AuPlusCinqVus()
    {
        var catalog = LessonCatalogLoader.LoadFromResource();
        uint scancode = Keys.Scancodes.OrderBy(s => s).First(s => Keys.OnKey(s).Count >= LessonsHomeLogic.KeySessionSeen + 2);
        var onKey = Keys.OnKey(scancode);
        var entries = Entries(onKey.Take(LessonsHomeLogic.KeySessionSeen + 1).Select(c => Entry(c.Character, box: 1)).ToArray());

        var session = LessonsHomeLogic.BuildKeySession(catalog, Keys, entries, scancode, T0, "Séance", 1);

        Assert.Equal(LessonsHomeLogic.KeySessionSeen + 1, session.Characters.Count); // cinq vus + un nouveau
    }

    [Fact]
    public void BuildKeySession_ToucheSansCaractereSuivi_PasDeSeance()
    {
        var catalog = LessonCatalogLoader.LoadFromResource();
        uint unused = Enumerable.Range(1, 0x80).Select(i => (uint)i).First(s => Keys.OnKey(s).Count == 0);

        var session = LessonsHomeLogic.BuildKeySession(catalog, Keys, Entries(), unused, T0, "Séance", 1);

        Assert.Null(session.Exercise);
        Assert.Null(session.NewCharacter);
    }

    [Fact]
    public void DaySeed_EstLeJourEnChiffres()
    {
        Assert.Equal(20261012, LessonsHomeLogic.DaySeed(new DateTimeOffset(2026, 10, 12, 23, 59, 0, TimeSpan.Zero)));
    }

    // ── Texte collé ──────────────────────────────────────────────────────

    [Fact]
    public void AnalyzeText_MarqueLesGestes_LesSautes_LesFinsDeLigne()
    {
        string gesture = Keys.All[0].Character;
        string text = $"ab {gesture}\n☃";

        var items = LessonsHomeLogic.AnalyzeText(text, c => c != "☃", c => c.ToString() == gesture);

        Assert.Equal(HomeTextKind.Plain, items[0].Kind);
        Assert.Equal(HomeTextKind.Gesture, items.First(i => i.Element == gesture).Kind);
        Assert.Contains(items, i => i.Kind == HomeTextKind.Break);
        Assert.Equal(HomeTextKind.Skipped, items[^1].Kind);
    }

    [Fact]
    public void SkippedElements_EgalLesRetiresDeLaLecon()
    {
        string text = "a☃b😀c☃ \te\r\nd";
        Func<string, bool> typable = c => c is "a" or "b" or "c" or "d" or "e" or " ";

        var items = LessonsHomeLogic.AnalyzeText(text, typable, _ => false);
        var built = LessonCustomText.Build(text, "Tape", typable);

        Assert.Equal(built.Removed, LessonsHomeLogic.SkippedElements(items));
    }

    [Fact]
    public void AnalyzeText_LeTexteSansNormalisation_SeLitCommeLaLecon()
    {
        // « e » + accent combinant = « é » : un seul morceau, comme la leçon le recompose.
        var items = LessonsHomeLogic.AnalyzeText("café", _ => true, _ => false);

        Assert.Equal("café", string.Concat(items.Select(i => i.Element)));
    }

    [Fact]
    public void TextBilan_CompteLesPassagesEtDitCeQuiEstARevoir()
    {
        string a = Keys.All[0].Character, b = Keys.All[1].Character;
        string content = $"{a}{b}{a} {a}";
        var entries = Entries(Entry(b, box: 3, due: T0.AddDays(-1)));

        var bilan = LessonsHomeLogic.TextBilan(content, entries, T0, c => c.ToString() == a || c.ToString() == b);

        Assert.Equal(new[] { a, b }, bilan.Select(line => line.Character));
        Assert.Equal(3, bilan[0].Count);
        Assert.Equal(-1, bilan[0].Box);
        Assert.False(bilan[0].Due);
        Assert.Equal(1, bilan[1].Count);
        Assert.Equal(3, bilan[1].Box);
        Assert.True(bilan[1].Due);
    }
}
