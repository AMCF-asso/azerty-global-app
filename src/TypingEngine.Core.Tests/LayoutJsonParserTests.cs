using TypingEngine.Core;

namespace TypingEngine.Core.Tests;

public sealed class LayoutJsonParserTests
{
    [Theory]
    [InlineData("SC012")]
    [InlineData("0x12")]
    [InlineData("18")]
    public void Parse_AcceptsSupportedScancodeFormats(string scancode)
    {
        string json = $$"""
        {
          "rows": [{ "keys": [{ "position": "C03", "scancode": "{{scancode}}", "base": "e", "shift": "E" }] }],
          "dead_keys": { "dk_circumflex": { "table": { " ": "^", "e": "ê" } } }
        }
        """;

        var layout = LayoutJsonParser.Parse(json);

        Assert.Equal("e", layout.Keys[0x12].Base);
        Assert.Equal("E", layout.Keys[0x12].Shift);
        Assert.Equal("ê", layout.DeadKeys["dk_circumflex"].Apply("e"));
    }

    [Fact]
    public void Parse_MissingRows_NamesTheRootProperty()
    {
        var error = Assert.Throws<LayoutFormatException>(() => LayoutJsonParser.Parse("""{ "dead_keys": {} }"""));

        Assert.Equal("rows", error.Path);
    }

    [Fact]
    public void Parse_MissingKeys_NamesTheRowThatLacksThem()
    {
        string json = """{ "rows": [{ "keys": [] }, { "row_id": 2 }] }""";

        var error = Assert.Throws<LayoutFormatException>(() => LayoutJsonParser.Parse(json));

        Assert.Equal("rows[1].keys", error.Path);
    }

    [Fact]
    public void Parse_MissingScancode_NamesTheKeyThatLacksIt()
    {
        string json = """{ "rows": [{ "keys": [{ "position": "C03", "scancode": "0x12" }, { "position": "C04" }] }] }""";

        var error = Assert.Throws<LayoutFormatException>(() => LayoutJsonParser.Parse(json));

        Assert.Equal("rows[0].keys[1].scancode", error.Path);
    }

    [Fact]
    public void Parse_MissingPosition_NamesTheKeyThatLacksIt()
    {
        string json = """{ "rows": [{ "keys": [{ "scancode": "0x12" }] }] }""";

        var error = Assert.Throws<LayoutFormatException>(() => LayoutJsonParser.Parse(json));

        Assert.Equal("rows[0].keys[0].position", error.Path);
    }

    [Fact]
    public void Parse_RowsThatIsNotAnArray_SaysSoInsteadOfFailingOnEnumeration()
    {
        var error = Assert.Throws<LayoutFormatException>(() => LayoutJsonParser.Parse("""{ "rows": "AZERTY" }"""));

        Assert.Equal("rows", error.Path);
        Assert.Contains("array", error.Problem);
    }

    [Fact]
    public void Parse_ScancodeBeyondUintRange_NamesTheKeyAndTheValue()
    {
        // Ten decimal digits satisfied the schema pattern until it was narrowed to nine.
        string json = """{ "rows": [{ "keys": [{ "position": "C03", "scancode": "9999999999" }] }] }""";

        var error = Assert.Throws<LayoutFormatException>(() => LayoutJsonParser.Parse(json));

        Assert.Equal("rows[0].keys[0].scancode", error.Path);
        Assert.Contains("9999999999", error.Problem);
    }

    [Fact]
    public void Parse_DeadKeysThatIsNotAnObject_NamesTheProperty()
    {
        string json = """{ "rows": [{ "keys": [{ "position": "C03", "scancode": "0x12" }] }], "dead_keys": [] }""";

        var error = Assert.Throws<LayoutFormatException>(() => LayoutJsonParser.Parse(json));

        Assert.Equal("dead_keys", error.Path);
    }

    [Fact]
    public void Parse_CompositionThatIsNotAString_NamesTheTableEntry()
    {
        string json = """
        {
          "rows": [{ "keys": [{ "position": "C03", "scancode": "0x12" }] }],
          "dead_keys": { "dk_circumflex": { "table": { "e": 4 } } }
        }
        """;

        var error = Assert.Throws<LayoutFormatException>(() => LayoutJsonParser.Parse(json));

        Assert.Equal("dead_keys.dk_circumflex.table.e", error.Path);
    }
}
