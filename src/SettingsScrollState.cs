namespace AZERTYGlobal;

/// <summary>Défilement des contrôles : conserver les petits deltas et le focus visible.</summary>
internal sealed class SettingsScrollState
{
    private int _wheelRemainder;
    public int ConsumeWheel(int delta)
    {
        int total = _wheelRemainder + delta;
        _wheelRemainder = total % 120;
        return total / 120;
    }

    public static int EnsureVisible(int scroll, int top, int bottom, int viewport, int margin)
    {
        if (top < margin) return scroll + top - margin;
        if (bottom > viewport - margin) return scroll + bottom - viewport + margin;
        return scroll;
    }
}
