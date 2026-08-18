using TypingEngine.Core;

namespace TypingEngine.Core.Tests;

/// <summary>
/// Characterization of the orphan dead-key path: the engine is asked to resolve a dead key
/// whose name is not in the layout's table. Added by the v1.2.0 release audit (finding R7),
/// which found the path uncovered.
///
/// This is reachable only from a malformed layout, and nothing rejects one: neither
/// LayoutJsonParser nor azerty-layout.schema.json enforces referential integrity between a
/// key's dk_ output and the dead_keys table.
///
/// These tests pin down what the engine does today. They are not a statement that today's
/// behaviour is correct -- they exist so that a future change to it cannot pass unnoticed,
/// which is exactly how the 1.0.0 to 1.2.0 change went unrecorded.
/// </summary>
public sealed class CompositionEngineOrphanDeadKeyTests
{
    /// <summary>
    /// Activation accepts a name that the layout does not define. No validation happens
    /// here, so the engine can enter a state it cannot resolve.
    /// </summary>
    [Fact]
    public void ActivatingUnknownDeadKey_IsAcceptedWithoutValidation()
    {
        var engine = new CompositionEngine(CreateLayout());

        var activation = engine.Process("dk_absent_from_table");

        Assert.Equal("", activation.Text);
        Assert.True(activation.StateChanged);
        Assert.Equal("dk_absent_from_table", engine.ActiveDeadKey);
    }

    /// <summary>
    /// Resolving against a missing table returns the character untouched, but flags
    /// StateChanged. The Windows adapter reads that as "the composition branch handled this
    /// key", emits the text and returns true -- so KeyMapper never reaches CanPassThrough on
    /// this path. Before the engine was extracted, the equivalent branch fell through to the
    /// normal character path, pass-through included.
    /// </summary>
    [Fact]
    public void ResolvingUnknownDeadKey_ReturnsCharacterAndClaimsStateChange()
    {
        var engine = new CompositionEngine(CreateLayout());
        engine.Process("dk_absent_from_table");

        var result = engine.Process("a");

        Assert.Equal("a", result.Text);
        Assert.True(result.StateChanged);
        Assert.Null(engine.ActiveDeadKey);
    }

    /// <summary>
    /// Chaining two dead keys where the first is unknown: the isolated form falls back to the
    /// empty string rather than throwing, and the second dead key becomes active.
    /// </summary>
    [Fact]
    public void ChainingFromUnknownDeadKey_YieldsEmptyIsolatedForm()
    {
        var engine = new CompositionEngine(CreateLayout());
        engine.Process("dk_absent_from_table");

        var result = engine.Process("dk_circumflex");

        Assert.Equal("", result.Text);
        Assert.True(result.StateChanged);
        Assert.Equal("dk_circumflex", engine.ActiveDeadKey);
    }

    /// <summary>
    /// The healthy path, kept alongside as the witness: without it, a broken engine that
    /// returned its input unchanged for every dead key would satisfy all three tests above.
    /// </summary>
    [Fact]
    public void KnownDeadKey_StillComposes()
    {
        var engine = new CompositionEngine(CreateLayout());
        engine.Process("dk_circumflex");

        var result = engine.Process("e");

        Assert.Equal("E_CIRCUMFLEX", result.Text);
        Assert.NotEqual("e", result.Text);
    }

    /// <summary>
    /// ASCII-only table, per this project's text conventions. The composed value is a marker
    /// string rather than a precomposed character: what matters here is that a lookup hit is
    /// distinguishable from a lookup miss.
    /// </summary>
    private static Layout CreateLayout()
    {
        var layout = new Layout();
        var deadKey = new DeadKeyDefinition { Name = "dk_circumflex" };
        deadKey.Table[" "] = "^";
        deadKey.Table["e"] = "E_CIRCUMFLEX";
        layout.DeadKeys[deadKey.Name] = deadKey;
        return layout;
    }
}
