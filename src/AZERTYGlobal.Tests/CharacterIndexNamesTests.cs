using System.Text.Json;
using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Audit 1.3.0, A31 (LOC-3) : cent caractères de character-index.json avaient pour nom leur
/// propre point de code (« U+0495 »). La Recherche affichait « u+0495 » et ne les trouvait ni
/// par « ghe » ni par « cyrillique ». Ce témoin relit la ressource embarquée et refuse qu'un
/// nom d'index, anglais ou français, redevienne un point de code brut.
/// </summary>
public class CharacterIndexNamesTests
{
    private static JsonDocument LireRessource()
    {
        using var flux = typeof(CharacterIndex).Assembly.GetManifestResourceStream("character-index.json");
        Assert.NotNull(flux);
        return JsonDocument.Parse(flux!);
    }

    private static string Lire(JsonElement e, string nom) =>
        e.TryGetProperty(nom, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()! : "";

    [Fact]
    public void Index_AucunNomNeCommenceParUPlus()
    {
        using var doc = LireRessource();
        var fautifs = new List<string>();
        foreach (var entree in doc.RootElement.GetProperty("characters").EnumerateObject())
        {
            var nomEn = Lire(entree.Value, "unicodeName");
            var nomFr = Lire(entree.Value, "unicodeNameFr");
            if (nomEn.StartsWith("U+", StringComparison.Ordinal) || nomFr.StartsWith("U+", StringComparison.Ordinal))
                fautifs.Add(Lire(entree.Value, "codePoint"));
        }

        Assert.True(fautifs.Count == 0,
            $"{fautifs.Count} entrée(s) de character-index.json ont un nom « U+XXXX » : {string.Join(", ", fautifs)}");
    }
}
