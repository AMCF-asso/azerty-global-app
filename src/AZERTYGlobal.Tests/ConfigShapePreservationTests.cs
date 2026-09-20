using System.Text.Json;
using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// AG130-11 — configuration : réécriture destructrice et concurrence.
///
/// (a) <c>ConfigManager.Save()</c> repassait tout nombre par <c>double</c> et sérialisait
/// en <b>chaîne</b> tout objet, tableau ou <c>null</c> qu'il ne connaissait pas :
/// <c>{"x":{"a":1}}</c> ressortait en <c>"x":"{\"a\":1}"</c>. La clé survivait, pas sa
/// forme — donc la version qui l'avait écrite ne la relisait plus. C'est le scénario du
/// retour arrière : l'utilisateur installe une version récente, revient à l'ancienne, et
/// l'ancienne détruit en silence les réglages de la récente au premier enregistrement.
///
/// (b) Le <c>.tmp</c> de sauvegarde était un chemin fixe, et le mutex d'instance unique
/// était en <c>Local\</c>, c'est-à-dire par session Windows.
///
/// Chaque témoin positif est suivi de son contrôle négatif.
/// </summary>
public class ConfigShapePreservationTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _configPath;

    public ConfigShapePreservationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "AZGSHAPE_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _configPath = Path.Combine(_tempDir, "config.json");
        ConfigManager.OverrideConfigPathForTests(_configPath);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
        GC.SuppressFinalize(this);
    }

    /// <summary>Écrit un config.json, puis force un cycle lecture → écriture complet.</summary>
    private JsonElement RoundTrip(string json)
    {
        File.WriteAllText(_configPath, json);
        ConfigManager.OverrideConfigPathForTests(_configPath); // vide le cache, force EnsureLoaded
        ConfigManager.SetCompatibilityOverride("temoin.exe", "forceOn"); // déclenche Save()
        using var doc = JsonDocument.Parse(File.ReadAllText(_configPath));
        return doc.RootElement.Clone();
    }

    // ═══════════════════════════════════════════════════════════════
    // (a) La forme des clés inconnues survit à un enregistrement
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void UnObjetInconnu_ResteUnObjet()
    {
        var apres = RoundTrip("""{"futurReglage":{"mode":"auto","seuil":3}}""");

        var valeur = apres.GetProperty("futurReglage");
        Assert.Equal(JsonValueKind.Object, valeur.ValueKind);
        Assert.Equal("auto", valeur.GetProperty("mode").GetString());
        Assert.Equal(3, valeur.GetProperty("seuil").GetInt32());
    }

    [Fact]
    public void UnTableauInconnu_ResteUnTableau()
    {
        var apres = RoundTrip("""{"profilsRecents":["fr","en","de"]}""");

        var valeur = apres.GetProperty("profilsRecents");
        Assert.Equal(JsonValueKind.Array, valeur.ValueKind);
        Assert.Equal(3, valeur.GetArrayLength());
        Assert.Equal("de", valeur[2].GetString());
    }

    [Fact]
    public void UnNullInconnu_ResteUnNull()
    {
        var apres = RoundTrip("""{"derniereMigration":null}""");

        Assert.Equal(JsonValueKind.Null, apres.GetProperty("derniereMigration").ValueKind);
    }

    /// <summary>
    /// Le nombre passait par <c>double</c>, qui n'a que 53 bits de mantisse : un identifiant
    /// long y perdait ses derniers chiffres sans que rien ne le signale. C'est la moitié
    /// silencieuse du défaut — l'autre est visible à l'œil dans le fichier.
    /// </summary>
    [Fact]
    public void UnEntierLong_NePerdPasSesDerniersChiffres()
    {
        var apres = RoundTrip("""{"identifiantLong":9007199254740993}""");

        Assert.Equal(9007199254740993L, apres.GetProperty("identifiantLong").GetInt64());
    }

    /// <summary>
    /// Contrôle négatif de la série : les clés que l'application connaît traversent le même
    /// chemin sans changer de forme. Sans ce témoin, une écriture devenue inerte — qui
    /// n'écrirait plus rien du tout — passerait les quatre tests ci-dessus par accident,
    /// puisque le fichier d'origine porte déjà les bonnes formes.
    /// </summary>
    [Fact]
    public void LesClésConnues_TraversentAussi()
    {
        var apres = RoundTrip("""{"appLanguage":"en","futurReglage":{"a":1}}""");

        Assert.Equal("en", apres.GetProperty("appLanguage").GetString());
        // La preuve que Save() a bien écrit : la clé posée par RoundTrip est là.
        Assert.Equal("forceOn", apres.GetProperty("compatibility").GetProperty("temoin.exe").GetString());
    }

    /// <summary>
    /// Second contrôle négatif : une chaîne qui <em>ressemble</em> à du JSON reste une
    /// chaîne. Sans lui, une correction qui re-parserait les chaînes passerait pour bonne.
    /// </summary>
    [Fact]
    public void UneChaîneQuiRessembleÀDuJson_ResteUneChaîne()
    {
        var apres = RoundTrip("""{"note":"{\"a\":1}"}""");

        var valeur = apres.GetProperty("note");
        Assert.Equal(JsonValueKind.String, valeur.ValueKind);
        Assert.Equal("""{"a":1}""", valeur.GetString());
    }

    // ═══════════════════════════════════════════════════════════════
    // (b) Le fichier temporaire est propre au processus
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Deux instances du même compte — console et Bureau à distance — écrivaient dans le
    /// même <c>config.json.tmp</c>, et l'<c>IOException</c> de collision était avalée :
    /// dernier écrivain gagnant, sans trace. Le PID suffit à les séparer.
    /// </summary>
    [Fact]
    public void DeuxProcessus_NécessitentDeuxFichiersTemporaires()
    {
        Assert.NotEqual(ConfigManager.BuildTempPath(_configPath, 1234),
                        ConfigManager.BuildTempPath(_configPath, 5678));
        Assert.EndsWith(".1234.tmp", ConfigManager.BuildTempPath(_configPath, 1234));
    }

    /// <summary>
    /// Que le chemin soit bien construit ne dit pas que <c>Save()</c> s'en sert. Le seul
    /// témoin honnête rend ce chemin <b>occupé</b> : un dossier au chemin attendu fait
    /// échouer l'ouverture du flux, et la sauvegarde n'aboutit pas. Asserter l'absence des
    /// deux fichiers après une sauvegarde réussie ne prouvait rien — c'est vrai quel que
    /// soit le chemin choisi, et la mutation « retour au chemin fixe » restait verte
    /// (mesuré le 2026-09-20).
    /// </summary>
    [Fact]
    public void Save_ÉcritAuCheminParPid()
    {
        Directory.CreateDirectory(ConfigManager.BuildTempPath(_configPath, Environment.ProcessId));

        ConfigManager.SetCompatibilityOverride("temoin.exe", "forceOn");

        Assert.False(File.Exists(_configPath)); // le chemin par PID était barré, rien n'a pu s'écrire
    }

    /// <summary>
    /// Contrôle négatif du témoin ci-dessus : l'ancien chemin fixe barré ne gêne plus rien.
    /// Sans lui, un <c>Save()</c> devenu incapable d'écrire quoi que ce soit passerait.
    /// </summary>
    [Fact]
    public void Save_NUtilisePlusLAncienCheminFixe()
    {
        Directory.CreateDirectory(_configPath + ".tmp");

        ConfigManager.SetCompatibilityOverride("temoin.exe", "forceOn");

        Assert.True(File.Exists(_configPath));
    }

    // ═══════════════════════════════════════════════════════════════
    // (b) Les deux noms d'objet d'instance unique
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void LesDeuxMutex_SontLocalEtGlobal()
    {
        var (local, global) = Program.BuildSingleInstanceMutexNames("S-1-5-21-42");

        Assert.StartsWith("Local\\", local);
        Assert.StartsWith("Global\\", global);
        Assert.EndsWith(".S-1-5-21-42", local);
        Assert.EndsWith(".S-1-5-21-42", global);
    }

    /// <summary>
    /// Le SID reste dans les deux noms : sans lui, un autre compte de la machine squatterait
    /// le nom et empêcherait le démarrage (audit sécu 2026-05, SEV-A2-03). C'est aussi ce qui
    /// borne la portée à un compte : deux comptes distincts ont des réglages distincts et
    /// doivent pouvoir tourner ensemble.
    /// </summary>
    [Fact]
    public void DeuxSidDifférents_DonnentDesNomsDifférents()
    {
        var (_, globalA) = Program.BuildSingleInstanceMutexNames("S-1-5-21-42");
        var (_, globalB) = Program.BuildSingleInstanceMutexNames("S-1-5-21-43");

        Assert.NotEqual(globalA, globalB);
    }

    [Fact]
    public void SidIntrouvable_RetombeSurUnQualifiantStable()
    {
        var (localNull, globalNull) = Program.BuildSingleInstanceMutexNames(null);
        var (localVide, globalVide) = Program.BuildSingleInstanceMutexNames("");

        Assert.EndsWith(".anon", localNull);
        Assert.EndsWith(".anon", globalNull);
        Assert.Equal(localNull, localVide);
        Assert.Equal(globalNull, globalVide);
    }
}
