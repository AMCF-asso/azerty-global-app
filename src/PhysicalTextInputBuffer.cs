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

    public bool HasPending
    {
        get
        {
            DiscardExpiredEntries(_clock());
            return _entries.Count > 0;
        }
    }

    public char Resolve(char received)
    {
        long now = _clock();
        DiscardExpiredEntries(now);
        if (_entries.Count > 0)
        {
            var entry = _entries.Peek();

            // Un caractère de contrôle reçu (Ctrl+lettre, Retour arrière, Entrée, Tab) n'est
            // jamais remplacé par du texte attendu : Ctrl+A tapait « a » (audit 09/11, E1, E12).
            // Il ne consomme l'entrée que si elle attendait elle aussi un contrôle.
            if (char.IsControl(received))
            {
                if (char.IsControl(entry.Text[entry.Index]) && ++entry.Index >= entry.Text.Length)
                    _entries.Dequeue();
                return received;
            }

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
        // Raccourci (Ctrl, Alt ou Win tenu) : aucun texte n'est attendu. Sans cette garde,
        // Ctrl+A tapait « a » et Ctrl+chiffre laissait une entrée fantôme d'une seconde qui
        // décalait les lettres suivantes (audit 09/11, E1, E11).
        if (IsShortcutChord(mapper.CtrlDown, mapper.AltDown, mapper.WinDown, mapper.AltGrDown))
            return;
        // Audit 09/11, E14 : une couche verrouillée (grecque, cyrillique…) n'est pas une touche morte
        // armée (mapper.ActiveDeadKey ne rend que la couche d'un coup) : le moteur écrit « α », la file
        // attendait « a » et le remplaçait.
        var layer = mapper.MaintainableLayerState;
        string? lockedLayer = layer.Mode == MaintainableLayerMode.Locked ? layer.LayerId : null;
        string? text = ExpectedText(layout, scancode, mapper.ShiftDown, mapper.AltGrDown,
            mapper.CapsLockActive, mapper.ActiveDeadKey, lockedLayer);
        if (!string.IsNullOrEmpty(text))
            Enqueue(text);
    }

    /// <summary>
    /// Ctrl, Alt ou Win tenu, hors Alt Gr : la frappe est un raccourci, pas du texte. Sous
    /// Windows Alt Gr se signale comme Ctrl+Alt : le tester seul casserait toutes les
    /// frappes Alt Gr des leçons (KeyMapper.IsAltGrDown).
    /// </summary>
    internal static bool IsShortcutChord(bool ctrl, bool alt, bool win, bool altGr) =>
        (ctrl || alt || win) && !altGr;

    /// <summary>
    /// Texte d'une frappe : la sortie de la couche, transformée par la touche morte armée
    /// (accent seul puis caractère si elle ne s'y applique pas). Une touche morte seule ne
    /// produit rien, sauf si elle en remplace une autre, dont l'accent sort alors.
    /// </summary>
    internal static string? ExpectedText(Layout layout, uint scancode, bool shift, bool altGr, bool capsLock,
        string? activeDeadKey, string? lockedLayer = null)
    {
        if (!layout.Keys.TryGetValue(scancode, out var keyDef)) return null;
        string? output = keyDef.GetOutput(shift, altGr, capsLock);
        if (string.IsNullOrEmpty(output)) return null;

        // Couche verrouillée (KeyMapper : seulement hors touche morte armée et hors activation de
        // couche) : la table de la couche transforme la frappe, l'espace reste un espace, une touche
        // que la couche ne connaît pas sort telle quelle, la couche conservée.
        if (lockedLayer != null && activeDeadKey == null && !output.StartsWith("dk_", StringComparison.Ordinal))
        {
            if (output == " ") return output;
            return layout.DeadKeys.GetValueOrDefault(lockedLayer)?.Apply(output) ?? output;
        }

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
