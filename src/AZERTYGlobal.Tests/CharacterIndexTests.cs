using System.Text.Json;
using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Audit du 25/09, V-04 et L-03 : témoin du chargeur unique de character-index.json.
/// Les cinq chargeurs remplacés choisissaient la méthode recommandée, la variante Caps et
/// l'activation des touches mortes chacun à sa façon ; ce témoin relit la ressource
/// indépendamment et vérifie chaque caractère contre ces règles.
/// </summary>
public class CharacterIndexTests
{
    private static JsonDocument LireRessource()
    {
        using var flux = typeof(CharacterIndex).Assembly.GetManifestResourceStream("character-index.json");
        Assert.NotNull(flux);
        return JsonDocument.Parse(flux!);
    }

    private static string Lire(JsonElement e, string nom) =>
        e.TryGetProperty(nom, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()! : "";

    private static string Cle(JsonElement m) =>
        $"{Lire(m, "type")}|{Lire(m, "key")}|{Lire(m, "layer")}|{Lire(m, "deadkey")}";

    private static string Cle(MethodData m) => $"{m.Type}|{m.Key}|{m.Layer}|{m.DeadKey}";

    [Fact]
    public void Index_EstChargeUneSeuleFois()
    {
        Assert.Same(CharacterIndex.Shared, CharacterIndex.Shared);
        Assert.True(CharacterIndex.Shared.Entries.Count > 1000);
    }

    [Fact]
    public void Index_SuitLesReglesDesAnciensChargeurs()
    {
        using var doc = LireRessource();
        var caracteres = doc.RootElement.GetProperty("characters");
        var index = CharacterIndex.Shared;

        // Activation d'une touche morte : la première méthode « deadkey_activation » de son entrée « dk: ».
        var activations = new Dictionary<string, (string, string)>();
        foreach (var entree in caracteres.EnumerateObject())
        {
            if (!entree.Name.StartsWith("dk:", StringComparison.Ordinal)) continue;
            var premiere = entree.Value.GetProperty("methods").EnumerateArray()
                .First(m => Lire(m, "type") == "deadkey_activation");
            activations[Lire(premiere, "deadkey")] = (Lire(premiere, "key"), Lire(premiere, "layer"));
        }
        Assert.Equal(activations.OrderBy(a => a.Key), index.DeadKeyActivations.OrderBy(a => a.Key).Select(a => new KeyValuePair<string, (string, string)>(a.Key, (a.Value.Key, a.Value.Layer))));

        int rang = 0;
        foreach (var entree in caracteres.EnumerateObject())
        {
            if (entree.Name.StartsWith("dk:", StringComparison.Ordinal)) continue;
            var attendu = entree.Value.GetProperty("methods").EnumerateArray().ToList();
            var obtenu = index.Entries[rang++];
            Assert.Equal(entree.Name, obtenu.Character);
            Assert.Same(obtenu, index.ByCharacter[entree.Name]);

            // Toutes les méthodes, dans l'ordre du fichier.
            Assert.Equal(attendu.Select(Cle), obtenu.Methods.Select(Cle));

            // Méthode par défaut : la première recommandée, sinon la première.
            var recommandee = attendu.FirstOrDefault(m => m.TryGetProperty("recommended", out var r) && r.ValueKind == JsonValueKind.True);
            var preferee = recommandee.ValueKind == JsonValueKind.Undefined ? attendu[0] : recommandee;
            Assert.Equal(Cle(preferee), Cle(obtenu.Preferred!));
            Assert.Equal(recommandee.ValueKind != JsonValueKind.Undefined, obtenu.Recommended != null);

            // Variante Caps : la première méthode sur une couche Caps.
            var caps = attendu.FirstOrDefault(m => Lire(m, "layer").StartsWith("Caps", StringComparison.Ordinal));
            Assert.Equal(caps.ValueKind == JsonValueKind.Undefined ? null : Cle(caps), obtenu.Caps == null ? null : Cle(obtenu.Caps));

            // Méthodes par touche morte, avec leur activation.
            var parTouchesMortes = attendu.Where(m => Lire(m, "type") == "deadkey" && Lire(m, "deadkey").Length > 0).Select(Cle).ToList();
            if (parTouchesMortes.Count == 0)
                Assert.False(index.DeadKeyMethodsByCharacter.ContainsKey(entree.Name));
            else
                Assert.Equal(parTouchesMortes, index.DeadKeyMethodsByCharacter[entree.Name].Select(Cle));
            foreach (var m in obtenu.Methods.Where(m => m.Type == "deadkey"))
                Assert.Equal(activations[m.DeadKey], (m.DkActivationKey, m.DkActivationLayer));

            // Noms des infobulles : présents dès qu'un des deux noms existe.
            var noms = (Lire(entree.Value, "unicodeNameFr"), Lire(entree.Value, "unicodeName"));
            if (noms.Item1.Length > 0 || noms.Item2.Length > 0)
                Assert.Equal(noms, index.Names[entree.Name]);
            else
                Assert.False(index.Names.ContainsKey(entree.Name));
        }
        Assert.Equal(rang, index.Entries.Count);
        Assert.DoesNotContain(index.Names.Keys, k => k.StartsWith("dk:", StringComparison.Ordinal));
    }

    [Fact]
    public void Index_ValeursConnues()
    {
        var index = CharacterIndex.Shared;

        // « É » : la méthode recommandée (Verr. Maj. + 2) n'est pas la première du fichier.
        var e = index.ByCharacter["É"];
        Assert.Equal(("direct", "Digit2", "Caps"), (e.Preferred!.Type, e.Preferred.Key, e.Preferred.Layer));
        Assert.NotSame(e.Methods[0], e.Preferred);

        // « © » passe par la touche morte des symboles, activée par AltGr + ².
        var copyright = index.ByCharacter["©"].Preferred!;
        Assert.Equal(("dk_misc_symbols", "Backquote", "AltGr"), (copyright.DeadKey, copyright.DkActivationKey, copyright.DkActivationLayer));
    }
}
