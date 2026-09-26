namespace AZERTYGlobal;

/// <summary>
/// Conserve dans l'ordre le texte attendu pour chaque frappe physique jusqu'à la
/// réception des WM_CHAR correspondants. Une file est nécessaire : sous forte
/// cadence, plusieurs keydown peuvent précéder le premier WM_CHAR.
/// </summary>
internal sealed class PhysicalTextInputBuffer
{
    private sealed class Entry
    {
        public Entry(string text, long createdAt)
        {
            Text = text;
            CreatedAt = createdAt;
        }

        public string Text { get; }
        public long CreatedAt { get; }
        public int Index { get; set; }
    }

    private readonly Queue<Entry> _entries = new();
    private readonly long _timeoutMs;
    private readonly Func<long> _clock;

    public PhysicalTextInputBuffer(long timeoutMs, Func<long>? clock = null)
    {
        if (timeoutMs <= 0)
            throw new ArgumentOutOfRangeException(nameof(timeoutMs));

        _timeoutMs = timeoutMs;
        _clock = clock ?? (() => Environment.TickCount64);
    }

    public void Enqueue(string text)
    {
        if (string.IsNullOrEmpty(text)) return;

        long now = _clock();
        DiscardExpiredEntries(now);
        _entries.Enqueue(new Entry(text, now));
    }

    public char Resolve(char received)
    {
        long now = _clock();
        DiscardExpiredEntries(now);
        if (_entries.Count > 0)
        {
            var entry = _entries.Peek();

            char expected = entry.Text[entry.Index++];
            if (entry.Index >= entry.Text.Length)
                _entries.Dequeue();
            return expected;
        }

        return received;
    }

    public void Clear() => _entries.Clear();

    /// <summary>
    /// Retient le texte qu'AZERTY Global va produire pour la touche physique qui vient
    /// d'être enfoncée. Appelé au keydown, avant le moteur : c'est là que la touche morte
    /// armée et les modificateurs tenus sont encore ceux de cette frappe. Le tutoriel et les
    /// Leçons en ont chacun une file (audit du 25/09, L-04 : le calcul était recopié).
    /// </summary>
    public void CaptureKey(Layout layout, uint scancode, KeyMapper mapper)
    {
        string? text = ExpectedText(layout, scancode, mapper.ShiftDown, mapper.AltGrDown,
            mapper.CapsLockActive, mapper.ActiveDeadKey);
        if (!string.IsNullOrEmpty(text))
            Enqueue(text);
    }

    /// <summary>
    /// Texte d'une frappe : la sortie de la couche, transformée par la touche morte armée
    /// (accent seul puis caractère si elle ne s'y applique pas). Une touche morte seule ne
    /// produit rien, sauf si elle en remplace une autre, dont l'accent sort alors.
    /// </summary>
    internal static string? ExpectedText(Layout layout, uint scancode, bool shift, bool altGr, bool capsLock,
        string? activeDeadKey)
    {
        if (!layout.Keys.TryGetValue(scancode, out var keyDef)) return null;
        string? output = keyDef.GetOutput(shift, altGr, capsLock);
        if (string.IsNullOrEmpty(output)) return null;

        DeadKeyDefinition? active = null;
        if (activeDeadKey != null)
            layout.DeadKeys.TryGetValue(activeDeadKey, out active);

        if (output.StartsWith("dk_", StringComparison.Ordinal))
        {
            // Une activation de touche morte seule ne produit pas de WM_CHAR.
            if (active?.GetIsolated() is not { } isolated) return null;
            return layout.DeadKeys.GetValueOrDefault(output)?.Apply(isolated) ?? isolated;
        }
        if (active == null) return output;
        if (active.Apply(output) is { } transformed) return transformed;
        return active.GetIsolated() is { } alone ? alone + output : output;
    }

    private void DiscardExpiredEntries(long now)
    {
        while (_entries.Count > 0 && now - _entries.Peek().CreatedAt > _timeoutMs)
            _entries.Dequeue();
    }
}
