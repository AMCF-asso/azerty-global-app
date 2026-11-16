// Champ de texte des Leçons, sans fenêtre : texte, curseur, sélection, annulation. Le mode Libre
// et « Votre texte » s'en servent tous deux (audit UX du 09/11, E6, E7, E8, E16, E21, E22 :
// les deux champs étaient écrits à la main, chacun avec ses manques). Décision d'Antoine (QCM
// du 09/11) : le mode Libre se comporte comme un vrai champ Windows (sélection au clavier et à la
// souris, Ctrl+A/C/X/V/Z, sauts par mot, Entrée), sans plafond de 220 caractères.
//
// Le curseur et l'ancre sont des index UTF-16 toujours posés sur une limite de grappe (une
// lettre et ses marques, un émoji entier) : rien ne coupe un caractère en deux.
using System.Globalization;

namespace AZERTYGlobal;

/// <summary>Presse-papiers vu du champ : remplaçable dans les tests, qui ne touchent jamais
/// celui de la machine.</summary>
internal interface ITextClipboard
{
    string? Read();
    bool Write(string text);
}

/// <summary>Le presse-papiers de Windows, au nom de la fenêtre qui porte le champ.</summary>
internal sealed class WindowClipboard : ITextClipboard
{
    private readonly Func<IntPtr> _owner;

    public WindowClipboard(Func<IntPtr> owner) => _owner = owner;

    public string? Read()
    {
        if (!Win32.OpenClipboard(_owner())) return null;
        try { return CharacterSearch.ReadClipboardText(); }
        finally { Win32.CloseClipboard(); }
    }

    public bool Write(string text) => ClipboardText.TrySet(_owner(), text);
}

/// <summary>Ce qu'une touche demande au champ.</summary>
internal enum TextEditCommand
{
    None,
    Left, Right, WordLeft, WordRight, Up, Down, LineStart, LineEnd, DocumentStart, DocumentEnd,
    Backspace, Delete, WordBackspace, WordDelete, NewLine,
    SelectAll, Copy, Cut, Paste, Undo, Redo,
}

/// <summary>Une commande, et si elle étend la sélection (Maj tenue sur un déplacement).</summary>
internal readonly record struct TextEditKey(TextEditCommand Command, bool Extend)
{
    public static readonly TextEditKey None = new(TextEditCommand.None, false);
}

/// <summary>Modificateurs tenus pendant une frappe.</summary>
internal readonly record struct EditModifiers(bool Ctrl, bool Shift, bool Alt, bool Win, bool AltGr)
{
    /// <summary>Ctrl seul au sens des raccourcis : ni Alt, ni Win, et jamais Alt Gr, que Windows
    /// signale comme Ctrl+Alt (sinon Alt Gr+A taperait « Tout sélectionner »).</summary>
    public bool CtrlShortcut => PhysicalTextInputBuffer.IsShortcutChord(Ctrl, Alt, Win, AltGr) && Ctrl && !Alt && !Win;
}

internal static class TextEditKeys
{
    private const int VK_BACK = 0x08, VK_RETURN = 0x0D, VK_END = 0x23, VK_HOME = 0x24, VK_LEFT = 0x25,
        VK_UP = 0x26, VK_RIGHT = 0x27, VK_DOWN = 0x28, VK_INSERT = 0x2D, VK_DELETE = 0x2E;

    /// <summary>
    /// Les touches d'un champ Windows. Alt ou Win tenus : rien (le système ou l'application s'en
    /// servent). Alt Gr tenu : la frappe est un caractère, pas un raccourci.
    /// </summary>
    public static TextEditKey Map(int vk, EditModifiers m)
    {
        if (m.AltGr) return TextEditKey.None;
        if (m.Alt || m.Win) return TextEditKey.None;
        bool ctrl = m.CtrlShortcut;
        bool shift = m.Shift;
        switch (vk)
        {
            case VK_LEFT: return new(ctrl ? TextEditCommand.WordLeft : TextEditCommand.Left, shift);
            case VK_RIGHT: return new(ctrl ? TextEditCommand.WordRight : TextEditCommand.Right, shift);
            case VK_UP: return ctrl ? TextEditKey.None : new(TextEditCommand.Up, shift);
            case VK_DOWN: return ctrl ? TextEditKey.None : new(TextEditCommand.Down, shift);
            case VK_HOME: return new(ctrl ? TextEditCommand.DocumentStart : TextEditCommand.LineStart, shift);
            case VK_END: return new(ctrl ? TextEditCommand.DocumentEnd : TextEditCommand.LineEnd, shift);
            case VK_BACK: return new(ctrl ? TextEditCommand.WordBackspace : TextEditCommand.Backspace, false);
            case VK_DELETE:
                if (shift && !ctrl) return new(TextEditCommand.Cut, false); // Maj+Suppr, classique de Windows
                return new(ctrl ? TextEditCommand.WordDelete : TextEditCommand.Delete, false);
            case VK_INSERT:
                if (ctrl && !shift) return new(TextEditCommand.Copy, false);  // Ctrl+Inser
                if (shift && !ctrl) return new(TextEditCommand.Paste, false); // Maj+Inser
                return TextEditKey.None;
            case VK_RETURN: return ctrl ? TextEditKey.None : new(TextEditCommand.NewLine, false);
        }
        if (!ctrl) return TextEditKey.None;
        return vk switch
        {
            'A' => new(TextEditCommand.SelectAll, false),
            'C' => shift ? TextEditKey.None : new(TextEditCommand.Copy, false),
            'X' => shift ? TextEditKey.None : new(TextEditCommand.Cut, false),
            'V' => shift ? TextEditKey.None : new(TextEditCommand.Paste, false),
            'Z' => new(shift ? TextEditCommand.Redo : TextEditCommand.Undo, false),
            'Y' => shift ? TextEditKey.None : new(TextEditCommand.Redo, false),
            _ => TextEditKey.None,
        };
    }
}

/// <summary>Résultat d'une insertion : ce qui est entré, et si la limite du champ a coupé.</summary>
internal readonly record struct TextInsertResult(int Inserted, bool Truncated);

internal sealed class TextEditBuffer
{
    private const int MaxUndo = 200;

    private enum EditKind { None, Typing, Deleting }

    private readonly record struct Snapshot(string Text, int Caret, int Anchor);

    private readonly Func<string, string> _normalize;
    private readonly List<Snapshot> _undo = new();
    private readonly List<Snapshot> _redo = new();
    private EditKind _lastKind;
    private int[]? _bounds;

    /// <param name="maxLength">Longueur maximale (UTF-16) ; au-delà, l'insertion est coupée à une
    /// limite de grappe et <see cref="TextInsertResult.Truncated"/> le dit.</param>
    /// <param name="normalize">Forme que garde le champ (« Votre texte » : composée, fins de ligne
    /// en \n) ; appliquée au texte entier après chaque insertion.</param>
    public TextEditBuffer(int maxLength, Func<string, string>? normalize = null)
    {
        MaxLength = maxLength;
        _normalize = normalize ?? DefaultNormalize;
    }

    public int MaxLength { get; }
    public string Text { get; private set; } = "";
    public int Caret { get; private set; }
    public int Anchor { get; private set; }
    public bool HasSelection => Anchor != Caret;
    public int SelectionStart => Math.Min(Anchor, Caret);
    public int SelectionEnd => Math.Max(Anchor, Caret);
    public string SelectedText => Text[SelectionStart..SelectionEnd];
    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    /// <summary>Fins de ligne en \n, tabulations en espaces, autres caractères de contrôle ôtés.</summary>
    public static string DefaultNormalize(string text)
    {
        if (text.Length == 0) return text;
        var sb = new System.Text.StringBuilder(text.Length);
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '\r')
            {
                sb.Append('\n');
                if (i + 1 < text.Length && text[i + 1] == '\n') i++;
            }
            else if (c == '\t') sb.Append(' ');
            else if (c == '\n' || !char.IsControl(c)) sb.Append(c);
        }
        return sb.ToString();
    }

    /// <summary>Vide le champ et son historique.</summary>
    public void Clear()
    {
        SetState("", 0, 0);
        _undo.Clear();
        _redo.Clear();
        _lastKind = EditKind.None;
    }

    // ── Grappes ──────────────────────────────────────────────────────

    /// <summary>Débuts de grappe, suivis de la longueur du texte.</summary>
    private int[] Bounds()
    {
        if (_bounds != null) return _bounds;
        var starts = StringInfo.ParseCombiningCharacters(Text);
        var bounds = new int[starts.Length + 1];
        starts.CopyTo(bounds, 0);
        bounds[^1] = Text.Length;
        return _bounds = bounds;
    }

    /// <summary>La limite de grappe la plus proche à gauche de <paramref name="at"/> (ou lui-même).</summary>
    public int Snap(int at)
    {
        at = Math.Clamp(at, 0, Text.Length);
        var bounds = Bounds();
        int i = Array.BinarySearch(bounds, at);
        return i >= 0 ? at : bounds[Math.Max(0, ~i - 1)];
    }

    public int PreviousBoundary(int at)
    {
        var bounds = Bounds();
        int i = Array.BinarySearch(bounds, at);
        int index = i >= 0 ? i - 1 : ~i - 1;
        return index < 0 ? 0 : bounds[index];
    }

    public int NextBoundary(int at)
    {
        var bounds = Bounds();
        int i = Array.BinarySearch(bounds, at);
        int index = i >= 0 ? i + 1 : ~i;
        return index >= bounds.Length ? Text.Length : bounds[index];
    }

    // ── Mots ─────────────────────────────────────────────────────────

    private enum CharClass { Space, NewLine, Word, Punctuation }

    private CharClass ClassAt(int index)
    {
        char c = Text[index];
        if (c == '\n') return CharClass.NewLine;
        if (char.IsWhiteSpace(c)) return CharClass.Space;
        var category = CharUnicodeInfo.GetUnicodeCategory(Text, index);
        return category switch
        {
            UnicodeCategory.UppercaseLetter or UnicodeCategory.LowercaseLetter or UnicodeCategory.TitlecaseLetter or
            UnicodeCategory.ModifierLetter or UnicodeCategory.OtherLetter or UnicodeCategory.DecimalDigitNumber or
            UnicodeCategory.LetterNumber or UnicodeCategory.OtherNumber or UnicodeCategory.NonSpacingMark or
            UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark or UnicodeCategory.ConnectorPunctuation
                => CharClass.Word,
            _ => c == '\'' || c == '’' ? CharClass.Word : CharClass.Punctuation, // « l’arbre » : un mot, comme Word
        };
    }

    /// <summary>Ctrl+Droite : au début du mot suivant (après le mot courant et ses espaces) ;
    /// une fin de ligne compte pour un arrêt.</summary>
    public int WordRightFrom(int at)
    {
        at = Snap(at);
        if (at >= Text.Length) return Text.Length;
        var cls = ClassAt(at);
        if (cls == CharClass.NewLine) return NextBoundary(at);
        if (cls != CharClass.Space)
            while (at < Text.Length && ClassAt(at) == cls) at = NextBoundary(at);
        while (at < Text.Length && ClassAt(at) == CharClass.Space) at = NextBoundary(at);
        return at;
    }

    /// <summary>Ctrl+Gauche : au début du mot courant, ou du précédent si l'on y est déjà.</summary>
    public int WordLeftFrom(int at)
    {
        at = Snap(at);
        while (at > 0 && ClassAt(PreviousBoundary(at)) == CharClass.Space) at = PreviousBoundary(at);
        if (at == 0) return 0;
        int previous = PreviousBoundary(at);
        var cls = ClassAt(previous);
        if (cls == CharClass.NewLine) return previous;
        while (at > 0 && ClassAt(PreviousBoundary(at)) == cls) at = PreviousBoundary(at);
        return at;
    }

    /// <summary>Double-clic : le mot (ou la suite d'espaces, ou de ponctuation) sous l'index.</summary>
    public void SelectWordAt(int index)
    {
        if (Text.Length == 0) { MoveTo(0, extend: false); return; }
        int at = Snap(Math.Min(index, Text.Length - 1));
        var cls = ClassAt(at);
        int start = at, end = NextBoundary(at);
        if (cls != CharClass.NewLine)
        {
            while (start > 0 && ClassAt(PreviousBoundary(start)) == cls) start = PreviousBoundary(start);
            while (end < Text.Length && ClassAt(end) == cls) end = NextBoundary(end);
        }
        _lastKind = EditKind.None;
        Anchor = start;
        Caret = end;
    }

    // ── Déplacements ─────────────────────────────────────────────────

    /// <summary>Pose le curseur ; avec <paramref name="extend"/>, l'ancre reste et la sélection
    /// s'étend.</summary>
    public void MoveTo(int index, bool extend)
    {
        int at = Snap(index);
        _lastKind = EditKind.None;
        Caret = at;
        if (!extend) Anchor = at;
    }

    /// <summary>Gauche ou Droite. Sans Maj, une sélection se replie sur son bord, comme dans
    /// Windows.</summary>
    public void MoveHorizontal(int direction, bool word, bool extend)
    {
        if (!extend && HasSelection && !word)
        {
            MoveTo(direction < 0 ? SelectionStart : SelectionEnd, extend: false);
            return;
        }
        int target = direction < 0
            ? (word ? WordLeftFrom(Caret) : PreviousBoundary(Caret))
            : (word ? WordRightFrom(Caret) : NextBoundary(Caret));
        MoveTo(target, extend);
    }

    public void SelectAll()
    {
        _lastKind = EditKind.None;
        Anchor = 0;
        Caret = Text.Length;
    }

    // ── Modifications ────────────────────────────────────────────────

    /// <summary>
    /// Insère au curseur, en remplaçant la sélection. Coupé à une limite de grappe si le champ
    /// est plein (<see cref="TextInsertResult.Truncated"/>). Les frappes successives d'un mot
    /// forment une seule étape d'annulation.
    /// </summary>
    public TextInsertResult Insert(string text, bool typed = false)
    {
        string added = DefaultNormalize(text);
        if (added.Length == 0 && !HasSelection) return new(0, false);

        string before = Text[..SelectionStart];
        string after = Text[SelectionEnd..];
        int room = MaxLength - before.Length - after.Length;
        bool truncated = false;
        if (added.Length > room)
        {
            truncated = true;
            // Les grappes qui tiennent en entier restent ; la première qui déborde et la suite partent.
            int cut = 0;
            var starts = StringInfo.ParseCombiningCharacters(added);
            for (int i = 0; i < starts.Length; i++)
            {
                int end = i + 1 < starts.Length ? starts[i + 1] : added.Length;
                if (end > room) break;
                cut = end;
            }
            added = added[..cut];
            if (added.Length == 0 && !HasSelection) return new(0, true);
        }

        bool continuesTyping = typed && _lastKind == EditKind.Typing && !HasSelection
                               && !(added.Length == 1 && char.IsWhiteSpace(added[0]));
        if (!continuesTyping) PushUndo();
        _redo.Clear();

        string merged = _normalize(before + added + after);
        int caret = Math.Clamp(merged.Length - _normalize(after).Length, 0, merged.Length);
        SetState(merged, caret, caret);
        _lastKind = typed ? EditKind.Typing : EditKind.None;
        return new(added.Length, truncated);
    }

    /// <summary>Retour arrière (ou Suppr) : la sélection, sinon une grappe, sinon un mot
    /// (Ctrl). Rend faux s'il n'y avait rien à effacer.</summary>
    public bool Delete(bool backwards, bool word)
    {
        int start, end;
        if (HasSelection)
        {
            start = SelectionStart;
            end = SelectionEnd;
        }
        else if (backwards)
        {
            if (Caret == 0) return false;
            end = Caret;
            start = word ? WordLeftFrom(Caret) : PreviousBoundary(Caret);
        }
        else
        {
            if (Caret >= Text.Length) return false;
            start = Caret;
            end = word ? WordRightFrom(Caret) : NextBoundary(Caret);
        }
        if (end <= start) return false;

        bool continuesDeleting = _lastKind == EditKind.Deleting && !HasSelection && !word;
        if (!continuesDeleting) PushUndo();
        _redo.Clear();
        SetState(Text.Remove(start, end - start), start, start);
        _lastKind = word ? EditKind.None : EditKind.Deleting;
        return true;
    }

    public bool Copy(ITextClipboard clipboard) => HasSelection && clipboard.Write(SelectedText);

    public bool Cut(ITextClipboard clipboard)
    {
        if (!HasSelection || !clipboard.Write(SelectedText)) return false;
        return Delete(backwards: true, word: false);
    }

    public TextInsertResult Paste(ITextClipboard clipboard)
    {
        string? text = clipboard.Read();
        if (string.IsNullOrEmpty(text)) return new(0, false);
        return Insert(text);
    }

    public bool Undo()
    {
        if (_undo.Count == 0) return false;
        _redo.Add(new Snapshot(Text, Caret, Anchor));
        var s = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        SetState(s.Text, s.Caret, s.Anchor);
        _lastKind = EditKind.None;
        return true;
    }

    public bool Redo()
    {
        if (_redo.Count == 0) return false;
        _undo.Add(new Snapshot(Text, Caret, Anchor));
        var s = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        SetState(s.Text, s.Caret, s.Anchor);
        _lastKind = EditKind.None;
        return true;
    }

    private void PushUndo()
    {
        _undo.Add(new Snapshot(Text, Caret, Anchor));
        if (_undo.Count > MaxUndo) _undo.RemoveAt(0);
    }

    private void SetState(string text, int caret, int anchor)
    {
        if (!ReferenceEquals(text, Text)) _bounds = null;
        Text = text;
        Caret = Math.Clamp(caret, 0, text.Length);
        Anchor = Math.Clamp(anchor, 0, text.Length);
    }
}
