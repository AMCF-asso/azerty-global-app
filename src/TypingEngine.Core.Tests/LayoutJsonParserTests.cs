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
}
