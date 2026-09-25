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

    private void DiscardExpiredEntries(long now)
    {
        while (_entries.Count > 0 && now - _entries.Peek().CreatedAt > _timeoutMs)
            _entries.Dequeue();
    }
}
