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

/// <summary>
/// Onglet « Leçons » de Paramètres (11/10) : le groupe « Touches à taper ». Fenêtre jamais
/// montrée (aucun Show) et masquée par <see cref="FenetresInvisibles"/> au cas où : les
/// contrôles sont lus et pressés par leurs vrais messages (BM_CLICK, WM_COMMAND). Les
/// positions décrites ici sont celles que les captures CI confirmeront.
/// </summary>
public class NotationSettingsWindowTests : IDisposable
{
    private const uint WS_VISIBLE = 0x10000000;
    private const uint WS_GROUP = 0x00020000;
    private const int GWL_STYLE = -16;

    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int index);

    private readonly string _tempDir;
    private readonly FenetresInvisibles _invisibles = new();

    public NotationSettingsWindowTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "AZGNOTWIN_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        ConfigManager.OverrideConfigPathForTests(Path.Combine(_tempDir, "config.json"));
        L.Language = "fr";
    }

    public void Dispose()
    {
        L.Language = "fr";
        _invisibles.Dispose();
        try { Directory.Delete(_tempDir, true); } catch { }
        GC.SuppressFinalize(this);
    }

    private static IntPtr[] Radios(SettingsWindow window, bool withKeyboard) =>
        BancCapture.Field<IntPtr[]>(window, withKeyboard ? "_hWndNotationWith" : "_hWndNotationWithout");

    private static bool Checked(IntPtr hWnd) =>
        Win32.SendMessageW(hWnd, Win32.BM_GETCHECK, IntPtr.Zero, IntPtr.Zero) == (IntPtr)Win32.BST_CHECKED;

    private static string Text(IntPtr hWnd)
    {
        var buffer = new System.Text.StringBuilder(256);
        Win32.GetWindowTextW(hWnd, buffer, buffer.Capacity);
        return buffer.ToString();
    }

    private static bool Shown(IntPtr hWnd) => ((long)GetWindowLongPtr(hWnd, GWL_STYLE) & WS_VISIBLE) != 0;

    private static object TabValue(int tab) =>
        Enum.ToObject(typeof(SettingsWindow).GetNestedType("SettingsTab", System.Reflection.BindingFlags.NonPublic)!, tab);

    private const int Lessons = 3;

    /// <summary>Un vrai clic sur le contrôle : BM_CLICK, qui prévient la fenêtre par WM_COMMAND.</summary>
    private static void Click(IntPtr hWnd) => Win32.SendMessageW(hWnd, Win32.BM_CLICK, IntPtr.Zero, IntPtr.Zero);

    private static SettingsWindow WindowOnLessonsTab()
    {
        var window = new SettingsWindow();
        BancCapture.Call(window, "SetActiveTab", TabValue(Lessons));
        return window;
    }

    // ═══════════════════════════════════════════════════════════════
    // Taille fixe, bande d'onglets
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("fr")]
    [InlineData("en")]
    public void LOngletLecons_NeFaitPasGrandirLaFenetre(string langue)
    {
        L.Language = langue;
        using var window = new SettingsWindow();
        int Height(int tab) => (int)BancCapture.Call(window, "MeasureContentHeight", TabValue(tab))!;

        int reference = Math.Max(Height(0), Math.Max(Height(1), Height(2)));
        // La fenêtre garde la hauteur des trois onglets d'avant, et « Leçons » y tient sans défiler.
        Assert.True(Height(Lessons) <= reference, $"Leçons {Height(Lessons)} px > référence {reference} px");
        Assert.Equal(reference, BancCapture.Field<int>(window, "_viewportHeight"));
        // Contrôle négatif : l'onglet n'est pas vide, il a bien un contenu mesuré.
        Assert.True(Height(Lessons) > 300);
    }

    [Theory]
    [InlineData("fr")]
    [InlineData("en")]
    public void LaBandeAQuatreOnglets_TousDansSaLargeur(string langue)
    {
        L.Language = langue;
        using var window = new SettingsWindow();
        IntPtr strip = BancCapture.Field<IntPtr>(window, "_hWndTabStrip");

        const uint TCM_GETITEMCOUNT = 0x1300 + 4;
        Assert.Equal(4, (int)Win32.SendMessageW(strip, TCM_GETITEMCOUNT, IntPtr.Zero, IntPtr.Zero));
        Win32.GetClientRect(strip, out var client);
        var last = (Win32.RECT)BancCapture.Call(window, "TabItemRect", 3)!;
        Assert.True(last.right > last.left, "le quatrième onglet n'a pas de rectangle");
        Assert.True(last.right <= client.right, $"« Leçons » déborde de la bande : {last.right} > {client.right}");
    }

    // ═══════════════════════════════════════════════════════════════
    // Les contrôles
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void LesListes_ontLesNotationsDeLeurEtat_etLeNomDeLaDecision()
    {
        using var window = new SettingsWindow();
        var with = Radios(window, true);
        var without = Radios(window, false);

        Assert.Equal(LessonNotations.WithKeyboard.Select(L.Notation_Name), with.Select(Text));
        Assert.Equal(LessonNotations.WithoutKeyboard.Select(L.Notation_Name), without.Select(Text));
        Assert.Equal(3, with.Length);
        Assert.Equal(4, without.Length);
        Assert.Equal(L.Notation_KeyboardUnderText, Text(BancCapture.Field<IntPtr>(window, "_hWndChkLessonKeyboard")));
        Assert.Equal(L.Notation_HideMastered, Text(BancCapture.Field<IntPtr>(window, "_hWndChkHideMastered")));
    }

    [Fact]
    public void ParDefaut_LesMotsSontCoches_etLesDeuxCasesAussi()
    {
        using var window = new SettingsWindow();
        Assert.Equal(new[] { true, false, false }, Radios(window, true).Select(Checked));
        Assert.Equal(new[] { true, false, false, false }, Radios(window, false).Select(Checked));
        Assert.True(Checked(BancCapture.Field<IntPtr>(window, "_hWndChkLessonKeyboard")));
        Assert.True(Checked(BancCapture.Field<IntPtr>(window, "_hWndChkHideMastered")));
    }

    [Fact]
    public void LaFenetreRelitLaConfig_aLOuverture()
    {
        using var window = new SettingsWindow();
        // Le menu « Affichage » de la fenêtre Leçons change les réglages pendant que Paramètres
        // est fermé : l'ouverture (Show) rappelle RefreshLessonControls.
        ConfigManager.SetLessonNotationFor(true, LessonNotation.NumberedKeyboard);
        ConfigManager.SetLessonNotationFor(false, LessonNotation.Frames);
        ConfigManager.SetLessonKeyboardVisible(false);
        ConfigManager.SetLessonHideMastered(false);
        BancCapture.Call(window, "RefreshLessonControls");

        Assert.Equal(new[] { false, false, true }, Radios(window, true).Select(Checked));
        Assert.Equal(new[] { false, false, false, true }, Radios(window, false).Select(Checked));
        Assert.False(Checked(BancCapture.Field<IntPtr>(window, "_hWndChkLessonKeyboard")));
        Assert.False(Checked(BancCapture.Field<IntPtr>(window, "_hWndChkHideMastered")));
    }

    [Fact]
    public void UneNotationIllisibleEnConfig_CocheLesMotsDansLaListe()
    {
        File.WriteAllText(Path.Combine(_tempDir, "config.json"),
            """{"lessonNotationWithKeyboard":"Frames","lessonNotationWithoutKeyboard":"Inconnue"}""");
        ConfigManager.OverrideConfigPathForTests(Path.Combine(_tempDir, "config.json"));
        using var window = new SettingsWindow();

        Assert.Equal(new[] { true, false, false }, Radios(window, true).Select(Checked));
        Assert.Equal(new[] { true, false, false, false }, Radios(window, false).Select(Checked));
    }

    [Fact]
    public void CliquerUnRadio_ReglePourSonEtatSeulement_etPrevientLaFenetreLecons()
    {
        using var window = WindowOnLessonsTab();
        int prevenue = 0;
        window.LessonDisplayChanged = () => prevenue++;
        var with = Radios(window, true);
        var without = Radios(window, false);

        Click(with[1]); // Recette écrite, avec le clavier
        Assert.Equal(LessonNotation.Recipe, ConfigManager.LessonNotationWithKeyboard);
        Assert.Equal(LessonNotation.Words, ConfigManager.LessonNotationWithoutKeyboard);
        Assert.Equal(new[] { false, true, false }, with.Select(Checked));
        // WS_GROUP sépare les listes : l'autre liste n'a pas bougé.
        Assert.Equal(new[] { true, false, false, false }, without.Select(Checked));
        Assert.Equal(1, prevenue);

        Click(without[3]); // Une vignette par temps, sans le clavier
        Assert.Equal(LessonNotation.Frames, ConfigManager.LessonNotationWithoutKeyboard);
        Assert.Equal(LessonNotation.Recipe, ConfigManager.LessonNotationWithKeyboard);
        Assert.Equal(new[] { false, true, false }, with.Select(Checked));
        Assert.Equal(new[] { false, false, false, true }, without.Select(Checked));
        Assert.Equal(2, prevenue);

        // Le réglage est écrit à la volée (pas d'OK) : le fichier le porte après enregistrement.
        ConfigManager.Flush();
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(_tempDir, "config.json")));
        Assert.Equal("Recipe", doc.RootElement.GetProperty("lessonNotationWithKeyboard").GetString());
        Assert.Equal("Frames", doc.RootElement.GetProperty("lessonNotationWithoutKeyboard").GetString());
    }

    [Fact]
    public void CliquerUneCase_BasculeLeReglage_sansToucherAuxNotations()
    {
        using var window = WindowOnLessonsTab();
        int prevenue = 0;
        window.LessonDisplayChanged = () => prevenue++;
        IntPtr keyboard = BancCapture.Field<IntPtr>(window, "_hWndChkLessonKeyboard");
        IntPtr hide = BancCapture.Field<IntPtr>(window, "_hWndChkHideMastered");
        Click(Radios(window, false)[2]); // Mini-clavier, sans le clavier
        prevenue = 0;

        Click(keyboard);
        Assert.False(ConfigManager.LessonKeyboardVisible);
        Assert.True(ConfigManager.LessonHideMastered);
        Click(hide);
        Assert.False(ConfigManager.LessonHideMastered);
        Assert.Equal(2, prevenue);

        // Masquer ou afficher le clavier bascule seul : aucune notation n'a changé (QCM du 11/10).
        Assert.Equal(LessonNotation.MiniKeyboard, ConfigManager.LessonNotationWithoutKeyboard);
        Assert.Equal(LessonNotation.Words, ConfigManager.LessonNotationWithKeyboard);

        Click(keyboard);
        Assert.True(ConfigManager.LessonKeyboardVisible);
    }

    [Fact]
    public void UnClicSurUnAutreContrôle_NeDeclenchePasLaFenetreLecons()
    {
        using var window = new SettingsWindow();
        int prevenue = 0;
        window.LessonDisplayChanged = () => prevenue++;
        Click(BancCapture.Field<IntPtr>(window, "_hWndChkNotifications"));
        Assert.Equal(0, prevenue);
    }

    [Fact]
    public void LesPremiersControlesDeChaqueGroupe_ontWsGroup()
    {
        using var window = new SettingsWindow();
        var with = Radios(window, true);
        var without = Radios(window, false);
        IntPtr keyboard = BancCapture.Field<IntPtr>(window, "_hWndChkLessonKeyboard");

        bool Group(IntPtr hWnd) => ((long)GetWindowLongPtr(hWnd, GWL_STYLE) & WS_GROUP) != 0;
        Assert.True(Group(with[0]));
        Assert.True(Group(without[0]));
        Assert.True(Group(keyboard)); // termine le groupe des radios, sinon la case y entrerait
        // Contrôle négatif : les suivants n'ouvrent pas de groupe.
        Assert.All(with.Skip(1), r => Assert.False(Group(r)));
        Assert.All(without.Skip(1), r => Assert.False(Group(r)));
    }

    [Fact]
    public void LesIdentifiants_sontDistinctsEntreEux()
    {
        using var window = new SettingsWindow();
        var ids = Radios(window, true).Concat(Radios(window, false))
            .Append(BancCapture.Field<IntPtr>(window, "_hWndChkLessonKeyboard"))
            .Append(BancCapture.Field<IntPtr>(window, "_hWndChkHideMastered"))
            .Select(Win32.GetDlgCtrlID).ToList();
        Assert.Equal(9, ids.Count);
        Assert.Equal(9, ids.Distinct().Count());
        Assert.DoesNotContain(0, ids);
    }

    [Fact]
    public void ChangerDeLangue_RetraduitLesLibelles()
    {
        using var window = new SettingsWindow();
        L.Language = "en";
        BancCapture.Call(window, "RefreshLanguageTexts");

        Assert.Equal(LessonNotations.WithoutKeyboard.Select(L.Notation_Name), Radios(window, false).Select(Text));
        Assert.Equal("Mini keyboard under the character", Text(Radios(window, false)[2]));
        Assert.Equal("Hide the gesture of mastered characters", Text(BancCapture.Field<IntPtr>(window, "_hWndChkHideMastered")));
    }

    // ═══════════════════════════════════════════════════════════════
    // Visibilité par onglet, mise en page
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void LesControles_nApparaissentQueSurLOngletLecons()
    {
        using var window = new SettingsWindow();
        var all = Radios(window, true).Concat(Radios(window, false))
            .Append(BancCapture.Field<IntPtr>(window, "_hWndChkLessonKeyboard"))
            .Append(BancCapture.Field<IntPtr>(window, "_hWndChkHideMastered")).ToList();

        Assert.All(all, c => Assert.False(Shown(c))); // onglet Général à l'ouverture

        BancCapture.Call(window, "SetActiveTab", TabValue(Lessons));
        Assert.All(all, c => Assert.True(Shown(c)));
        // Les contrôles des autres onglets, eux, sont masqués : un seul onglet à la fois.
        Assert.False(Shown(BancCapture.Field<IntPtr>(window, "_hWndChkAutoStart")));
        Assert.False(Shown(BancCapture.Field<IntPtr>(window, "_hWndRadioLangFr")));

        BancCapture.Call(window, "SetActiveTab", TabValue(0));
        Assert.All(all, c => Assert.False(Shown(c)));
    }

    [Theory]
    [InlineData("fr")]
    [InlineData("en")]
    public void LaMiseEnPage_EmpileTitre_Listes_Cases_sansChevauchement_etSansDebordement(string langue)
    {
        L.Language = langue;
        using var window = new SettingsWindow();
        BancCapture.Call(window, "SetActiveTab", TabValue(Lessons));
        float scale = BancCapture.Field<float>(window, "_dpiScale");
        int width = (int)(380 * scale);
        object layout = BancCapture.Call(window, "GetLayout", width)!;
        T Get<T>(string name) => (T)layout.GetType().GetField(name)!.GetValue(layout)!;

        var title = Get<Win32.RECT>("NotationTitleRect");
        var withLabel = Get<Win32.RECT>("NotationWithLabelRect");
        var with = Get<Win32.RECT[]>("NotationWithRects");
        var withoutLabel = Get<Win32.RECT>("NotationWithoutLabelRect");
        var without = Get<Win32.RECT[]>("NotationWithoutRects");
        var keyboard = Get<Win32.RECT>("LessonKeyboardRect");
        var hide = Get<Win32.RECT>("HideMasteredRect");
        Assert.Equal(3, with.Length);
        Assert.Equal(4, without.Length);

        var vertical = new List<Win32.RECT> { title, withLabel };
        vertical.AddRange(with);
        vertical.Add(withoutLabel);
        vertical.AddRange(without);
        vertical.Add(keyboard);
        vertical.Add(hide);
        for (int i = 0; i < vertical.Count; i++)
        {
            Assert.True(vertical[i].bottom > vertical[i].top, $"rectangle {i} vide");
            Assert.True(vertical[i].left >= 0 && vertical[i].right <= width, $"rectangle {i} hors de la fenêtre");
            if (i > 0)
                Assert.True(vertical[i].top >= vertical[i - 1].bottom, $"rectangle {i} chevauche le précédent");
        }
        // Chaque ligne de choix a 32 px de haut (à 100 %), comme dans les autres onglets.
        Assert.All(with.Concat(without).Append(keyboard).Append(hide),
            r => Assert.Equal((int)(32 * scale), r.bottom - r.top));
    }

    [Theory]
    [InlineData("fr")]
    [InlineData("en")]
    public void LesLibelles_tiennentSurUneLigne_dansLeurCadre(string langue)
    {
        // DrawChoice coupe sans passer à la ligne : un libellé trop long serait rogné en silence.
        L.Language = langue;
        using var window = new SettingsWindow();
        BancCapture.Call(window, "SetActiveTab", TabValue(Lessons));
        float scale = BancCapture.Field<float>(window, "_dpiScale");
        object layout = BancCapture.Call(window, "GetLayout", (int)(380 * scale))!;
        var rects = new List<(string Text, Win32.RECT Rect)>();
        var with = (Win32.RECT[])layout.GetType().GetField("NotationWithRects")!.GetValue(layout)!;
        var without = (Win32.RECT[])layout.GetType().GetField("NotationWithoutRects")!.GetValue(layout)!;
        for (int i = 0; i < with.Length; i++) rects.Add((L.Notation_Name(LessonNotations.WithKeyboard[i]), with[i]));
        for (int i = 0; i < without.Length; i++) rects.Add((L.Notation_Name(LessonNotations.WithoutKeyboard[i]), without[i]));
        rects.Add((L.Notation_KeyboardUnderText, (Win32.RECT)layout.GetType().GetField("LessonKeyboardRect")!.GetValue(layout)!));
        rects.Add((L.Notation_HideMastered, (Win32.RECT)layout.GetType().GetField("HideMasteredRect")!.GetValue(layout)!));

        IntPtr hdc = Win32.GetDC(IntPtr.Zero);
        try
        {
            var font = FontSpec.Of(AppFonts.Text, 15, TypeRamp.Regular, scale);
            foreach (var (text, rect) in rects)
            {
                // DrawChoice : indicateur de 18 px posé à 4 px du bord, libellé à 10 px de lui.
                int available = rect.right - rect.left - ((int)(4 * scale) + (int)(18 * scale) + (int)(10 * scale));
                int width = PaperText.Width(hdc, text, font);
                Assert.True(width <= available, $"« {text} » : {width} px pour {available} px disponibles");
            }
        }
        finally { Win32.ReleaseDC(IntPtr.Zero, hdc); }
    }
}
