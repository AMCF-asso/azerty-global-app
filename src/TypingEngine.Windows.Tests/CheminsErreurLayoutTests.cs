namespace TypingEngine.Windows.Tests;

/// <summary>
/// Audit des chemins d'erreur (rapport-A S-7, voisin de K107) : une touche morte dont la valeur n'est
/// pas un objet doit lever une LayoutFormatException qui nomme son chemin, pas une
/// InvalidOperationException sans chemin. Placé ici (et non dans TypingEngine.Core.Tests, hors de la
/// portée d'écriture de la tâche) : LayoutJsonParser est public.
/// </summary>
public sealed class CheminsErreurLayoutTests
{
    // rapport-A S-7 (K107)
    [Theory]
    [Trait("Audit", "chemins-erreur")]
    [InlineData("5")]
    [InlineData("null")]
    [InlineData("\"dk\"")]
    public void Parse_DeadKeyEntryThatIsNotAnObject_NamesTheEntry(string value)
    {
        string json = $$"""
        {
          "rows": [{ "keys": [{ "position": "C03", "scancode": "0x12" }] }],
          "dead_keys": { "dk_circumflex": {{value}} }
        }
        """;

        var error = Assert.Throws<LayoutFormatException>(() => LayoutJsonParser.Parse(json));

        Assert.Equal("dead_keys.dk_circumflex", error.Path);
    }
}
