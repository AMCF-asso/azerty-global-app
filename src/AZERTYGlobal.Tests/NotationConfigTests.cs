using System.Text.Json;
using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Réglages de la notation des touches à taper (QCM du 11/10) : une notation mémorisée par
/// état du clavier, le masquage des gestes maîtrisés. Valeur absente, inconnue ou illisible :
/// les Mots, sans rien perdre des autres clés du fichier (même doctrine que
/// ConfigShapePreservationTests : le retour arrière ne détruit pas ce qu'il ne connaît pas).
/// Chaque témoin positif est suivi de son contrôle négatif.
/// </summary>
public class NotationConfigTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _configPath;

    public NotationConfigTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "AZGNOTATION_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _configPath = Path.Combine(_tempDir, "config.json");
        ConfigManager.OverrideConfigPathForTests(_configPath);
        L.Language = "fr";
    }

    public void Dispose()
    {
        L.Language = "fr";
        try { Directory.Delete(_tempDir, true); } catch { }
        GC.SuppressFinalize(this);
    }

    /// <summary>Écrit un config.json puis le fait relire : cache vidé, rien d'hérité d'un test.</summary>
    private void Load(string json)
    {
        File.WriteAllText(_configPath, json);
        ConfigManager.OverrideConfigPathForTests(_configPath);
    }

    /// <summary>Enregistre, relit le fichier et rend sa racine JSON.</summary>
    private JsonElement FlushAndRead()
    {
        ConfigManager.Flush();
        using var doc = JsonDocument.Parse(File.ReadAllText(_configPath));
        return doc.RootElement.Clone();
    }

    /// <summary>Recharge le fichier tel qu'il est sur le disque, comme au prochain lancement.</summary>
    private void Reload() => ConfigManager.OverrideConfigPathForTests(_configPath);

    // ═══════════════════════════════════════════════════════════════
    // Valeurs par défaut
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void SansFichier_LesDeuxEtatsDonnentLesMots_etLeMasquageEstActif()
    {
        Assert.Equal(LessonNotation.Words, ConfigManager.LessonNotationWithKeyboard);
        Assert.Equal(LessonNotation.Words, ConfigManager.LessonNotationWithoutKeyboard);
        Assert.Equal(LessonNotation.Words, ConfigManager.LessonNotationFor(true));
        Assert.Equal(LessonNotation.Words, ConfigManager.LessonNotationFor(false));
        Assert.True(ConfigManager.LessonHideMastered);
        // Le clavier sous le texte garde son défaut d'avant : affiché.
        Assert.True(ConfigManager.LessonKeyboardVisible);
    }

    [Fact]
    public void LireUnDefaut_NEcritRienDansLeFichier()
    {
        _ = ConfigManager.LessonNotationWithKeyboard;
        _ = ConfigManager.LessonNotationWithoutKeyboard;
        _ = ConfigManager.LessonHideMastered;
        var racine = FlushAndRead();

        Assert.False(racine.TryGetProperty("lessonNotationWithKeyboard", out _));
        Assert.False(racine.TryGetProperty("lessonNotationWithoutKeyboard", out _));
        Assert.False(racine.TryGetProperty("lessonHideMastered", out _));
    }

    // ═══════════════════════════════════════════════════════════════
    // Aller-retour
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void AllerRetour_AvecLeClavier_ChaqueNotationDeLEtat()
    {
        foreach (var notation in LessonNotations.WithKeyboard)
        {
            ConfigManager.SetLessonNotationWithKeyboard(notation);
            var racine = FlushAndRead();
            // Stockée par nom d'enum, pas par numéro : l'ordre de l'enum peut changer.
            Assert.Equal(JsonValueKind.String, racine.GetProperty("lessonNotationWithKeyboard").ValueKind);
            Assert.Equal(notation.ToString(), racine.GetProperty("lessonNotationWithKeyboard").GetString());
            Reload();
            Assert.Equal(notation, ConfigManager.LessonNotationWithKeyboard);
        }
    }

    [Fact]
    public void AllerRetour_SansLeClavier_ChaqueNotationDeLEtat()
    {
        foreach (var notation in LessonNotations.WithoutKeyboard)
        {
            ConfigManager.SetLessonNotationWithoutKeyboard(notation);
            var racine = FlushAndRead();
            Assert.Equal(notation.ToString(), racine.GetProperty("lessonNotationWithoutKeyboard").GetString());
            Reload();
            Assert.Equal(notation, ConfigManager.LessonNotationWithoutKeyboard);
        }
    }

    [Fact]
    public void LesDeuxEtatsSontIndependants_Controle()
    {
        ConfigManager.SetLessonNotationFor(true, LessonNotation.NumberedKeyboard);
        ConfigManager.SetLessonNotationFor(false, LessonNotation.Frames);
        FlushAndRead();
        Reload();

        Assert.Equal(LessonNotation.NumberedKeyboard, ConfigManager.LessonNotationFor(true));
        Assert.Equal(LessonNotation.Frames, ConfigManager.LessonNotationFor(false));
        Assert.Equal(LessonNotation.NumberedKeyboard, ConfigManager.LessonNotationWithKeyboard);
        Assert.Equal(LessonNotation.Frames, ConfigManager.LessonNotationWithoutKeyboard);

        // Contrôle négatif : changer l'un ne bouge pas l'autre.
        ConfigManager.SetLessonNotationFor(false, LessonNotation.Recipe);
        Assert.Equal(LessonNotation.Recipe, ConfigManager.LessonNotationFor(false));
        Assert.Equal(LessonNotation.NumberedKeyboard, ConfigManager.LessonNotationFor(true));
    }

    [Fact]
    public void LeMasquageDesGestesMaitrises_AllerRetour()
    {
        ConfigManager.SetLessonHideMastered(false);
        var racine = FlushAndRead();
        Assert.Equal(JsonValueKind.False, racine.GetProperty("lessonHideMastered").ValueKind);
        Reload();
        Assert.False(ConfigManager.LessonHideMastered);

        ConfigManager.SetLessonHideMastered(true);
        FlushAndRead();
        Reload();
        Assert.True(ConfigManager.LessonHideMastered);
    }

    // ═══════════════════════════════════════════════════════════════
    // Valeur invalide pour l'état, inconnue ou illisible → Mots
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void UneNotationQuiNExistePasDansLEtat_RevientAuxMots()
    {
        // Grand clavier numéroté n'a pas de sens sans le clavier ; Mini-clavier et Vignettes
        // n'en ont pas avec lui (liste par état de LessonNotations).
        Load("""{"lessonNotationWithKeyboard":"MiniKeyboard","lessonNotationWithoutKeyboard":"NumberedKeyboard"}""");
        Assert.Equal(LessonNotation.Words, ConfigManager.LessonNotationWithKeyboard);
        Assert.Equal(LessonNotation.Words, ConfigManager.LessonNotationWithoutKeyboard);

        Load("""{"lessonNotationWithKeyboard":"Frames"}""");
        Assert.Equal(LessonNotation.Words, ConfigManager.LessonNotationWithKeyboard);

        // Contrôle négatif : les mêmes noms, dans l'état qui les connaît, sont conservés.
        Load("""{"lessonNotationWithKeyboard":"NumberedKeyboard","lessonNotationWithoutKeyboard":"MiniKeyboard"}""");
        Assert.Equal(LessonNotation.NumberedKeyboard, ConfigManager.LessonNotationWithKeyboard);
        Assert.Equal(LessonNotation.MiniKeyboard, ConfigManager.LessonNotationWithoutKeyboard);
    }

    [Theory]
    [InlineData("\"Inconnue\"")]       // nom qui n'existe pas (version future)
    [InlineData("\"recipe\"")]         // casse différente : le nom exact est exigé
    [InlineData("\"1\"")]              // numéro d'enum écrit en chaîne
    [InlineData("\"Words, Recipe\"")]  // combinaison de drapeaux que TryParse accepterait
    [InlineData("\"\"")]
    [InlineData("1")]                  // nombre
    [InlineData("true")]               // booléen
    [InlineData("null")]
    [InlineData("{\"a\":1}")]          // objet
    [InlineData("[\"Recipe\"]")]       // tableau
    public void UneValeurIllisible_DonneLesMots_dansLesDeuxEtats(string json)
    {
        Load("{\"lessonNotationWithKeyboard\":" + json + ",\"lessonNotationWithoutKeyboard\":" + json + "}");
        Assert.Equal(LessonNotation.Words, ConfigManager.LessonNotationWithKeyboard);
        Assert.Equal(LessonNotation.Words, ConfigManager.LessonNotationWithoutKeyboard);
    }

    [Fact]
    public void UnSetterRecoitUneNotationInvalide_LaConfigGardeLesMots()
    {
        ConfigManager.SetLessonNotationWithKeyboard(LessonNotation.Frames);
        ConfigManager.SetLessonNotationWithoutKeyboard(LessonNotation.NumberedKeyboard);
        var racine = FlushAndRead();

        Assert.Equal("Words", racine.GetProperty("lessonNotationWithKeyboard").GetString());
        Assert.Equal("Words", racine.GetProperty("lessonNotationWithoutKeyboard").GetString());
        Reload();
        Assert.Equal(LessonNotation.Words, ConfigManager.LessonNotationWithKeyboard);
        Assert.Equal(LessonNotation.Words, ConfigManager.LessonNotationWithoutKeyboard);
    }

    [Fact]
    public void UnMasquageIllisible_DonneLeDefautActif()
    {
        Load("""{"lessonHideMastered":"non"}""");
        Assert.True(ConfigManager.LessonHideMastered);
        Load("""{"lessonHideMastered":0}""");
        Assert.True(ConfigManager.LessonHideMastered);
        // Contrôle négatif : un faux écrit comme tel est bien lu.
        Load("""{"lessonHideMastered":false}""");
        Assert.False(ConfigManager.LessonHideMastered);
    }

    // ═══════════════════════════════════════════════════════════════
    // Le reste du fichier survit
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void UneCleInconnue_EstPreserveeApresUneEcritureDeNotation()
    {
        Load("""{"futurReglage":{"mode":"auto","seuil":3},"profilsRecents":["fr","en"],"lessonKeyboardVisible":false}""");

        ConfigManager.SetLessonNotationFor(false, LessonNotation.Recipe);
        ConfigManager.SetLessonHideMastered(false);
        var racine = FlushAndRead();

        Assert.Equal("Recipe", racine.GetProperty("lessonNotationWithoutKeyboard").GetString());
        // La forme des clés inconnues survit, pas seulement leur nom.
        var futur = racine.GetProperty("futurReglage");
        Assert.Equal(JsonValueKind.Object, futur.ValueKind);
        Assert.Equal("auto", futur.GetProperty("mode").GetString());
        Assert.Equal(3, futur.GetProperty("seuil").GetInt32());
        Assert.Equal(JsonValueKind.Array, racine.GetProperty("profilsRecents").ValueKind);
        Assert.Equal(JsonValueKind.False, racine.GetProperty("lessonKeyboardVisible").ValueKind);
    }

    [Fact]
    public void UneValeurIllisible_NePerdAucuneAutreCle()
    {
        // Le fichier d'une version future porte un nom que celle-ci ne connaît pas : il se
        // lit comme les Mots sans qu'aucune autre clé disparaisse à l'enregistrement suivant.
        Load("""{"lessonNotationWithKeyboard":"NotationDuFutur","futurReglage":{"a":[1,2]},"lessonHideMastered":false}""");
        Assert.Equal(LessonNotation.Words, ConfigManager.LessonNotationWithKeyboard);

        ConfigManager.SetLessonNotationFor(false, LessonNotation.Frames);
        var racine = FlushAndRead();

        Assert.Equal("Frames", racine.GetProperty("lessonNotationWithoutKeyboard").GetString());
        Assert.Equal(2, racine.GetProperty("futurReglage").GetProperty("a").GetArrayLength());
        Assert.Equal(JsonValueKind.False, racine.GetProperty("lessonHideMastered").ValueKind);
        // Le nom inconnu, lui, reste tel quel tant que l'utilisateur ne change pas ce réglage.
        Assert.Equal("NotationDuFutur", racine.GetProperty("lessonNotationWithKeyboard").GetString());
    }

    // ═══════════════════════════════════════════════════════════════
    // Libellés partagés
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void LesNomsDeNotation_DisentCeQueLaDecisionDit_enFrancaisEtEnAnglais()
    {
        var attendu = new (LessonNotation Notation, string Fr, string En)[]
        {
            (LessonNotation.Words, "Mots", "Words"),
            (LessonNotation.Recipe, "Recette écrite", "Written recipe"),
            (LessonNotation.NumberedKeyboard, "Grand clavier numéroté", "Numbered keyboard"),
            (LessonNotation.MiniKeyboard, "Mini-clavier sous le caractère", "Mini keyboard under the character"),
            (LessonNotation.Frames, "Une vignette par temps", "One frame per step"),
        };
        foreach (var (notation, fr, en) in attendu)
        {
            L.Language = "fr";
            Assert.Equal(fr, L.Notation_Name(notation));
            L.Language = "en";
            Assert.Equal(en, L.Notation_Name(notation));
        }
        L.Language = "fr";
        Assert.Equal("Touches à taper", L.Notation_GroupTitle);
        Assert.Equal("Avec le clavier", L.Notation_WithKeyboard);
        Assert.Equal("Sans le clavier", L.Notation_WithoutKeyboard);
        Assert.Equal("Clavier sous le texte", L.Notation_KeyboardUnderText);
        Assert.Equal("Masquer le geste des caractères maîtrisés", L.Notation_HideMastered);
        L.Language = "en";
        Assert.Equal("Keys to type", L.Notation_GroupTitle);
        Assert.Equal("With the keyboard", L.Notation_WithKeyboard);
        Assert.Equal("Without the keyboard", L.Notation_WithoutKeyboard);
        Assert.Equal("Keyboard under the text", L.Notation_KeyboardUnderText);
        Assert.Equal("Hide the gesture of mastered characters", L.Notation_HideMastered);
    }

    [Fact]
    public void ChaqueNotationDeChaqueEtat_PorteUnNomPropre()
    {
        // Contrôle négatif de la fonction : aucune notation listée ne retombe sur « Mots »
        // par le cas par défaut, et deux notations n'ont jamais le même nom.
        var noms = LessonNotations.WithKeyboard.Concat(LessonNotations.WithoutKeyboard)
            .Distinct().Select(L.Notation_Name).ToList();
        Assert.Equal(5, noms.Count);
        Assert.Equal(noms.Count, noms.Distinct().Count());
    }
}
