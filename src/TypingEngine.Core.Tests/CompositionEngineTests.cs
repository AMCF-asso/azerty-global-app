using TypingEngine.Core;

namespace TypingEngine.Core.Tests;

public sealed class CompositionEngineTests
{
    [Fact]
    public void DeadKeyThenCompatibleCharacter_EmitsComposition()
    {
        var engine = new CompositionEngine(CreateLayout());

        var activation = engine.Process("dk_circumflex");
        Assert.Equal("", activation.Text);
        Assert.Equal("dk_circumflex", engine.ActiveDeadKey);

        var composition = engine.Process("e");
        Assert.Equal("ê", composition.Text);
        Assert.Null(engine.ActiveDeadKey);
    }

    [Fact]
    public void DeadKeyThenUnsupportedCharacter_EmitsIsolatedMarkAndCharacter()
    {
        var engine = new CompositionEngine(CreateLayout());
        engine.Process("dk_circumflex");

        var result = engine.Process("x");

        Assert.Equal("^x", result.Text);
        Assert.Null(engine.ActiveDeadKey);
    }

    [Fact]
    public void Cancel_ClearsPendingCompositionWithoutOutput()
    {
        var engine = new CompositionEngine(CreateLayout());
        engine.Process("dk_circumflex");

        Assert.True(engine.Cancel());
        Assert.Null(engine.ActiveDeadKey);
        Assert.False(engine.Cancel());
    }

    private static Layout CreateLayout()
    {
        var layout = new Layout();
        var deadKey = new DeadKeyDefinition { Name = "dk_circumflex" };
        deadKey.Table[" "] = "^";
        deadKey.Table["e"] = "ê";
        layout.DeadKeys[deadKey.Name] = deadKey;
        return layout;
    }
}
