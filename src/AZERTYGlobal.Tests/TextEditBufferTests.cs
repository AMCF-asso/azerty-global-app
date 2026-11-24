using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Le champ de texte commun au mode Libre et à « Votre texte » (audit UX du 09/11, E6, E8,
/// E16, E21, E22), sans fenêtre ni presse-papiers de la machine.
/// </summary>
public class TextEditBufferTests
{
    internal sealed class FakeClipboard : ITextClipboard
    {
        public string? Content;
        public bool Fails;
        public string? Read() => Content;
        public bool Write(string text)
        {
            if (Fails) return false;
            Content = text;
            return true;
        }
    }

    private static TextEditBuffer With(string text, int max = 10_000)
    {
        var b = new TextEditBuffer(max);
        b.Insert(text);
        return b;
    }

    private static readonly EditModifiers None = new(false, false, false, false, false);
    private static readonly EditModifiers Ctrl = new(true, false, false, false, false);
    private static readonly EditModifiers Shift = new(false, true, false, false, false);
    private static readonly EditModifiers CtrlShift = new(true, true, false, false, false);
    // Alt Gr : Windows le signale comme Ctrl+Alt.
    private static readonly EditModifiers AltGr = new(true, false, true, false, true);
    private static readonly EditModifiers Alt = new(false, false, true, false, false);

    // ── Touches ──────────────────────────────────────────────────────

    [Fact]
    public void Ctrl_letter_is_the_windows_shortcut()
    {
        Assert.Equal(TextEditCommand.SelectAll, TextEditKeys.Map('A', Ctrl).Command);
        Assert.Equal(TextEditCommand.Copy, TextEditKeys.Map('C', Ctrl).Command);
        Assert.Equal(TextEditCommand.Cut, TextEditKeys.Map('X', Ctrl).Command);
        Assert.Equal(TextEditCommand.Paste, TextEditKeys.Map('V', Ctrl).Command);
        Assert.Equal(TextEditCommand.Undo, TextEditKeys.Map('Z', Ctrl).Command);
        Assert.Equal(TextEditCommand.Redo, TextEditKeys.Map('Y', Ctrl).Command);
    }

    /// <summary>Piège du lot 1 : Alt Gr+lettre tape un caractère d'AZERTY Global, jamais un raccourci.</summary>
    [Theory]
    [InlineData('A')] [InlineData('C')] [InlineData('X')] [InlineData('V')] [InlineData('Z')] [InlineData('Y')]
    [InlineData(0x25)] [InlineData(0x08)] [InlineData(0x0D)]
    public void AltGr_never_maps_to_a_command(int vk)
    {
        Assert.Equal(TextEditCommand.None, TextEditKeys.Map(vk, AltGr).Command);
    }

    [Fact]
    public void Plain_letters_and_Alt_letters_are_not_commands()
    {
        Assert.Equal(TextEditCommand.None, TextEditKeys.Map('A', None).Command);
        Assert.Equal(TextEditCommand.None, TextEditKeys.Map('A', Shift).Command);
        Assert.Equal(TextEditCommand.None, TextEditKeys.Map('A', Alt).Command);
        Assert.Equal(TextEditCommand.None, TextEditKeys.Map(0x25, Alt).Command);
    }

    [Fact]
    public void Navigation_keys_map_with_word_and_extend()
    {
        Assert.Equal(new TextEditKey(TextEditCommand.Left, false), TextEditKeys.Map(0x25, None));
        Assert.Equal(new TextEditKey(TextEditCommand.WordRight, true), TextEditKeys.Map(0x27, CtrlShift));
        Assert.Equal(new TextEditKey(TextEditCommand.DocumentEnd, false), TextEditKeys.Map(0x23, Ctrl));
        Assert.Equal(new TextEditKey(TextEditCommand.LineStart, true), TextEditKeys.Map(0x24, Shift));
        Assert.Equal(TextEditCommand.WordBackspace, TextEditKeys.Map(0x08, Ctrl).Command);
        Assert.Equal(TextEditCommand.WordDelete, TextEditKeys.Map(0x2E, Ctrl).Command);
        Assert.Equal(TextEditCommand.NewLine, TextEditKeys.Map(0x0D, None).Command);
        Assert.Equal(TextEditCommand.NewLine, TextEditKeys.Map(0x0D, Shift).Command);
        Assert.Equal(TextEditCommand.Redo, TextEditKeys.Map('Z', CtrlShift).Command);
        Assert.Equal(TextEditCommand.Cut, TextEditKeys.Map(0x2E, Shift).Command);
        Assert.Equal(TextEditCommand.Paste, TextEditKeys.Map(0x2D, Shift).Command);
        Assert.Equal(TextEditCommand.Copy, TextEditKeys.Map(0x2D, Ctrl).Command);
    }

    // ── Sélection et édition ────────────────────────────────────────

    [Fact]
    public void Typing_replaces_the_selection()
    {
        var b = With("bonjour monde");
        b.MoveTo(0, false);
        b.MoveTo(7, true);
        Assert.Equal("bonjour", b.SelectedText);
        b.Insert("salut", typed: true);
        Assert.Equal("salut monde", b.Text);
        Assert.Equal(5, b.Caret);
        Assert.False(b.HasSelection);
    }

    [Fact]
    public void SelectAll_then_Backspace_empties()
    {
        var b = With("abc def");
        b.SelectAll();
        Assert.True(b.Delete(backwards: true, word: false));
        Assert.Equal("", b.Text);
    }

    [Fact]
    public void Shift_arrow_extends_and_plain_arrow_collapses_to_the_edge()
    {
        var b = With("abcdef");
        b.MoveTo(2, false);
        b.MoveHorizontal(1, word: false, extend: true);
        b.MoveHorizontal(1, word: false, extend: true);
        Assert.Equal("cd", b.SelectedText);
        b.MoveHorizontal(-1, word: false, extend: false);
        Assert.Equal(2, b.Caret);
        Assert.False(b.HasSelection);
    }

    [Fact]
    public void Word_jumps_follow_windows()
    {
        var b = With("Le chat, dort.");
        b.MoveTo(0, false);
        Assert.Equal(3, b.WordRightFrom(0));   // « Le » puis l'espace
        Assert.Equal(7, b.WordRightFrom(3));   // « chat »
        Assert.Equal(9, b.WordRightFrom(7));   // « , » et l'espace
        Assert.Equal(9, b.WordLeftFrom(13));   // dans « dort » : son début
        Assert.Equal(7, b.WordLeftFrom(9));    // la virgule
        Assert.Equal(3, b.WordLeftFrom(7));
    }

    [Fact]
    public void Apostrophe_stays_inside_a_word()
    {
        var b = With("l’arbre vert");
        Assert.Equal(8, b.WordRightFrom(0));
    }

    [Fact]
    public void Ctrl_Backspace_deletes_the_previous_word_only()
    {
        var b = With("un deux trois");
        Assert.True(b.Delete(backwards: true, word: true));
        Assert.Equal("un deux ", b.Text);
        Assert.True(b.Delete(backwards: true, word: true));
        Assert.Equal("un ", b.Text);
    }

    [Fact]
    public void Newline_is_a_word_stop()
    {
        var b = With("ab\ncd");
        Assert.Equal(2, b.WordRightFrom(0));
        Assert.Equal(3, b.WordRightFrom(2));
        Assert.Equal(2, b.WordLeftFrom(3));
    }

    [Fact]
    public void Double_click_selects_the_word_under_the_point()
    {
        var b = With("le grand chêne");
        b.SelectWordAt(5);
        Assert.Equal("grand", b.SelectedText);
        b.SelectWordAt(2);
        Assert.Equal(" ", b.SelectedText);
    }

    // ── Grappes (E16) ───────────────────────────────────────────────

    [Fact]
    public void Backspace_never_splits_an_emoji_or_a_letter_and_its_mark()
    {
        var b = With("a😀");
        Assert.True(b.Delete(backwards: true, word: false));
        Assert.Equal("a", b.Text);

        var c = With("éx"); // e + accent combinant
        c.MoveTo(2, false);       // entre la marque et x : déjà une limite
        Assert.True(c.Delete(backwards: true, word: false));
        Assert.Equal("x", c.Text);
    }

    [Fact]
    public void Caret_snaps_to_a_grapheme_boundary()
    {
        var b = With("😀b");
        b.MoveTo(1, false); // au milieu de la paire de substitution
        Assert.Equal(0, b.Caret);
        b.MoveHorizontal(1, false, false);
        Assert.Equal(2, b.Caret);
    }

    // ── Annuler, rétablir ───────────────────────────────────────────

    [Fact]
    public void Undo_restores_a_whole_typed_word_and_redo_brings_it_back()
    {
        var b = new TextEditBuffer(1000);
        foreach (char c in "ab cd") b.Insert(c.ToString(), typed: true);
        Assert.True(b.Undo());
        Assert.Equal("ab", b.Text);   // « cd » et son espace forment une étape
        Assert.True(b.Undo());
        Assert.Equal("", b.Text);
        Assert.True(b.Redo());
        Assert.Equal("ab", b.Text);
        b.Insert("x", typed: true);
        Assert.False(b.CanRedo);      // une modification efface la branche à rétablir
    }

    [Fact]
    public void Undo_restores_text_deleted_by_selection()
    {
        var b = With("garde-moi");
        b.SelectAll();
        b.Delete(true, false);
        Assert.True(b.Undo());
        Assert.Equal("garde-moi", b.Text);
    }

    // ── Presse-papiers ──────────────────────────────────────────────

    [Fact]
    public void Copy_cut_paste_through_the_clipboard()
    {
        var clip = new FakeClipboard();
        var b = With("un deux");
        b.MoveTo(3, false);
        b.MoveTo(7, true);
        Assert.True(b.Copy(clip));
        Assert.Equal("deux", clip.Content);
        Assert.True(b.Cut(clip));
        Assert.Equal("un ", b.Text);
        b.MoveTo(0, false);
        Assert.Equal(new TextInsertResult(4, false), b.Paste(clip));
        Assert.Equal("deuxun ", b.Text);
    }

    [Fact]
    public void Cut_keeps_the_text_when_the_clipboard_refuses()
    {
        var clip = new FakeClipboard { Fails = true };
        var b = With("rien");
        b.SelectAll();
        Assert.False(b.Cut(clip));
        Assert.Equal("rien", b.Text);
    }

    [Fact]
    public void Pasted_line_ends_and_tabs_are_normalized()
    {
        var clip = new FakeClipboard { Content = "a\r\nb\tc\u0007" };
        var b = new TextEditBuffer(100);
        b.Paste(clip);
        Assert.Equal("a\nb c", b.Text);
    }

    // ── Limite (E6, E22) ────────────────────────────────────────────

    [Fact]
    public void Limit_cuts_at_a_grapheme_and_says_so()
    {
        var b = new TextEditBuffer(5);
        var r = b.Insert("abcd😀");
        Assert.True(r.Truncated);
        Assert.Equal("abcd", b.Text); // l'émoji (2 unités) ne tient pas en entier
        Assert.Equal(new TextInsertResult(1, false), b.Insert("x")); // la dernière place
        Assert.Equal(new TextInsertResult(0, true), b.Insert("y"));
        Assert.Equal("abcdx", b.Text);
    }

    [Fact]
    public void Replacing_a_selection_frees_its_room()
    {
        var b = new TextEditBuffer(5);
        b.Insert("abcde");
        b.SelectAll();
        Assert.Equal(new TextInsertResult(3, false), b.Insert("xyz"));
        Assert.Equal("xyz", b.Text);
    }

    [Fact]
    public void A_long_text_stays_fast_to_edit()
    {
        var b = new TextEditBuffer(100_000);
        b.Insert(string.Concat(Enumerable.Repeat("Le chat dort sur le tapis. ", 2000)));
        var watch = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < 200; i++)
        {
            b.MoveHorizontal(-1, word: true, extend: false);
            b.Insert("x", typed: true);
        }
        Assert.True(watch.ElapsedMilliseconds < 2000, $"{watch.ElapsedMilliseconds} ms");
    }
}
