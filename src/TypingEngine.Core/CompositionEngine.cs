namespace TypingEngine.Core;

/// <summary>Pure state machine for dead-key composition.</summary>
public sealed class CompositionEngine
{
    private readonly Layout _layout;

    public CompositionEngine(Layout layout) =>
        _layout = layout ?? throw new ArgumentNullException(nameof(layout));

    public string? ActiveDeadKey { get; private set; }

    public bool Cancel()
    {
        if (ActiveDeadKey is null) return false;
        ActiveDeadKey = null;
        return true;
    }

    public CompositionResult Process(string output)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (output.StartsWith("dk_", StringComparison.Ordinal)) return ActivateDeadKey(output);
        if (ActiveDeadKey is null) return new CompositionResult(output, false);

        string activeName = ActiveDeadKey;
        ActiveDeadKey = null;
        if (!_layout.DeadKeys.TryGetValue(activeName, out var deadKey))
            return new CompositionResult(output, true);

        string text = deadKey.Apply(output) ?? string.Concat(deadKey.GetIsolated(), output);
        return new CompositionResult(text, true);
    }

    private CompositionResult ActivateDeadKey(string name)
    {
        if (ActiveDeadKey is null)
        {
            ActiveDeadKey = name;
            return new CompositionResult("", true);
        }

        string previousName = ActiveDeadKey;
        ActiveDeadKey = null;
        string isolated = _layout.DeadKeys.GetValueOrDefault(previousName)?.GetIsolated() ?? "";
        string? transformed = _layout.DeadKeys.GetValueOrDefault(name)?.Apply(isolated);
        if (transformed is not null) return new CompositionResult(transformed, true);

        ActiveDeadKey = name;
        return new CompositionResult(isolated, true);
    }
}

public readonly record struct CompositionResult(string Text, bool StateChanged);
