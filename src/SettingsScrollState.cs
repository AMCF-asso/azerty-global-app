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

    /// <summary>
    /// Zone cliente fixe de la fenêtre Paramètres (décision d'Antoine du 2026-09-24) : la
    /// hauteur de l'onglet de référence (« Général »), plafonnée à la zone de travail, pour
    /// tous les onglets. La barre de défilement est réservée dès qu'un onglet dépasse cette
    /// hauteur, pour que la largeur ne change pas non plus d'un onglet à l'autre.
    /// </summary>
    public static (int Viewport, bool ReserveScrollBar) FixedViewport(
        int referenceHeight, int maxClientHeight, params int[] tabHeights)
    {
        int viewport = Math.Max(1, Math.Min(referenceHeight, maxClientHeight));
        bool reserve = false;
        foreach (int height in tabHeights)
            if (height > viewport) reserve = true;
        return (viewport, reserve);
    }
}
