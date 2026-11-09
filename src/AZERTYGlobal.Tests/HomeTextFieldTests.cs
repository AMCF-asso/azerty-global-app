using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// « Votre texte » en vrai champ, le même que le mode Libre (audit UX du 09/11, E21, E22) :
/// fenêtre cachée, touches par OnKeyDown puis le WM_CHAR qui suit, comme Windows ;
/// modificateurs et presse-papiers factices, jamais ceux de la machine.
/// </summary>
public class HomeTextFieldTests
{
    private static readonly EditModifiers NoMods = new(false, false, false, false, false);
    private static readonly EditModifiers CtrlMods = new(true, false, false, false, false);
    private static readonly EditModifiers ShiftMods = new(false, true, false, false, false);
    private static readonly EditModifiers AltGrMods = new(true, false, true, false, true);

    private sealed class Field : IDisposable
    {
        public readonly LessonsWindow Window;
        public readonly TextEditBufferTests.FakeClipboard Clip = new();
        public EditModifiers Mods = NoMods;

        public Field()
        {
            var layout = LayoutLoader.LoadFromResource();
            var mapper = new KeyMapper(layout, new MockWin32Api());
            var hook = new KeyboardHook(mapper); // jamais installé
            ConfigManager.ClearWindowBounds(ConfigManager.LessonsWindowBoundsKey);
            Window = new LessonsWindow(layout, mapper, hook);
            Window.ReadEditModifiers = () => Mods;
            Window.Clipboard = Clip;
            var pageType = typeof(LessonsWindow).GetNestedType("HomePage", System.Reflection.BindingFlags.NonPublic)!;
            BancCapture.SetField(Window, "_homePage", Enum.Parse(pageType, "Text"));
            Paint();
        }

        public TextEditBuffer Buffer => BancCapture.Field<TextEditBuffer>(Window, "_homeEdit");
        public string Text => Buffer.Text;
        public bool Truncated => BancCapture.Field<bool>(Window, "_homeTextTruncated");

        public void Paint()
        {
            IntPtr hwnd = BancCapture.Handle(Window);
            Win32.InvalidateRect(hwnd, IntPtr.Zero, false);
            Win32.SendMessageW(hwnd, Win32.WM_PAINT, IntPtr.Zero, IntPtr.Zero);
        }

        public void Type(string text)
        {
            foreach (char c in text)
            {
                if (c == '\n') { Key(0x0D, '\r'); continue; }
                BancCapture.Call(Window, "OnChar", c);
            }
        }

        /// <summary>Une touche : son keydown, puis le WM_CHAR qu'elle produit éventuellement.</summary>
        public void Key(int vk, char? wmChar = null, EditModifiers? mods = null)
        {
            Mods = mods ?? NoMods;
            BancCapture.Call(Window, "OnKeyDown", vk);
            if (wmChar is char c) BancCapture.Call(Window, "OnChar", c);
            Mods = NoMods;
        }

        public void Ctrl(char letter) => Key(letter, (char)(letter - 'A' + 1), CtrlMods);

        /// <summary>Un point de la première ligne du texte, en coordonnées client (lParam).</summary>
        public IntPtr PointOnFirstLine(int dx)
        {
            var area = BancCapture.Field<Win32.RECT>(Window, "_homeTextArea");
            int x = (dx >= 0 ? area.left + dx : area.right + dx) - BancCapture.Field<int>(Window, "_scrollX");
            int y = area.top + 6 - BancCapture.Field<int>(Window, "_scrollY");
            return (IntPtr)((y << 16) | (x & 0xFFFF));
        }

        public void Dispose() => BancCapture.Teardown(Window);
    }

    [Fact]
    public void Ctrl_A_C_X_V_Z_work_like_a_Windows_field()
    {
        using var isolation = new BancCapture.Isolation();
        using var f = new Field();
        f.Type("un deux");
        f.Ctrl('A');
        Assert.Equal("un deux", f.Buffer.SelectedText);
        Assert.Equal("un deux", f.Text); // le 0x01 du WM_CHAR n'écrit rien
        f.Ctrl('C');
        Assert.Equal("un deux", f.Clip.Content);
        f.Ctrl('X');
        Assert.Equal("", f.Text);
        f.Ctrl('V');
        f.Ctrl('V');
        Assert.Equal("un deuxun deux", f.Text);
        f.Ctrl('Z');
        Assert.Equal("un deux", f.Text);
        f.Ctrl('A');
        f.Type("x");
        Assert.Equal("x", f.Text); // la frappe remplace la sélection
    }

    [Fact]
    public void Ctrl_Backspace_erases_a_word_and_Shift_arrows_select()
    {
        using var isolation = new BancCapture.Isolation();
        using var f = new Field();
        f.Type("le chat dort");
        f.Key(0x08, '\u007F', CtrlMods); // Ctrl+Retour arrière : son WM_CHAR est DEL (0x7F)
        Assert.Equal("le chat ", f.Text);
        f.Key(0x25, mods: ShiftMods);
        f.Key(0x25, mods: ShiftMods);
        Assert.Equal("t ", f.Buffer.SelectedText);
    }

    /// <summary>Piège du lot 1 : Alt Gr tenu tape un caractère, jamais « Tout sélectionner ».</summary>
    [Fact]
    public void AltGr_letter_types_its_character()
    {
        using var isolation = new BancCapture.Isolation();
        using var f = new Field();
        f.Type("a");
        f.Key('A', 'æ', AltGrMods);
        Assert.Equal("aæ", f.Text);
        Assert.False(f.Buffer.HasSelection);
    }

    [Fact]
    public void A_paste_over_the_limit_is_cut_and_says_so()
    {
        using var isolation = new BancCapture.Isolation();
        using var f = new Field();
        f.Clip.Content = new string('b', LessonsHomeLogic.MaxTextLength + 1000);
        f.Ctrl('V');
        Assert.Equal(LessonsHomeLogic.MaxTextLength, f.Text.Length);
        Assert.True(f.Truncated);
        f.Paint(); // le pied dit « Champ plein » sans erreur de dessin
        f.Key(0x08, '\b');
        Assert.False(f.Truncated);
        Assert.Equal(LessonsHomeLogic.MaxTextLength - 1, f.Text.Length);
    }

    [Fact]
    public void Up_and_Down_move_between_lines_even_before_a_repaint()
    {
        using var isolation = new BancCapture.Isolation();
        using var f = new Field();
        f.Type("première\nseconde");
        f.Paint();
        f.Type(" ligne"); // pas de repeint : la mise en page se refait à la demande
        f.Key(0x26); // Haut
        Assert.True(f.Buffer.Caret <= "première".Length, $"curseur à {f.Buffer.Caret}");
        f.Key(0x28, mods: ShiftMods); // Maj+Bas : sélectionne jusqu'à la ligne d'en dessous
        Assert.Contains("\n", f.Buffer.SelectedText);
        f.Key(0x24); // Origine : début de la ligne visuelle
        Assert.Equal("première\n".Length, f.Buffer.Caret);
        f.Key(0x23); // Fin
        Assert.Equal(f.Text.Length, f.Buffer.Caret);
    }

    [Fact]
    public void Mouse_drag_selects_and_release_or_click_keeps_it()
    {
        using var isolation = new BancCapture.Isolation();
        using var f = new Field();
        f.Type("alpha beta");
        f.Paint();
        Assert.True((bool)BancCapture.Call(f.Window, "OnFieldMouseDown", f.PointOnFirstLine(1), IntPtr.Zero)!);
        Assert.Equal(0, f.Buffer.Caret);
        BancCapture.Call(f.Window, "OnFieldMouseDrag", f.PointOnFirstLine(-2));
        BancCapture.Call(f.Window, "EndFieldDrag");
        Assert.Equal("alpha beta", f.Buffer.SelectedText);
        BancCapture.Call(f.Window, "OnClick", f.PointOnFirstLine(1)); // le relâchement d'un clic
        Assert.Equal("alpha beta", f.Buffer.SelectedText);

        BancCapture.Call(f.Window, "OnDoubleClick", f.PointOnFirstLine(1));
        Assert.Equal("alpha", f.Buffer.SelectedText);
        BancCapture.Call(f.Window, "OnFieldMouseDown", f.PointOnFirstLine(-2), (IntPtr)0x0004); // Maj+clic
        BancCapture.Call(f.Window, "EndFieldDrag");
        Assert.Equal("alpha beta", f.Buffer.SelectedText);
    }

    [Fact]
    public void Accessibility_sees_the_selection()
    {
        using var isolation = new BancCapture.Isolation();
        using var f = new Field();
        f.Type("lecteur");
        f.Ctrl('A');
        f.Paint();
        var view = BancCapture.Field<LessonSurfaceView?>(f.Window, "_surfaceView")!;
        Assert.Equal((0, 7), view.Selection);
    }

    [Fact]
    public void Enter_on_a_focused_button_writes_nothing_in_the_field()
    {
        using var isolation = new BancCapture.Isolation();
        using var f = new Field();
        f.Type("a");
        f.Paint();
        BancCapture.SetField(f.Window, "_focusedActionIndex", 0);
        f.Key(0x0D, '\r');
        Assert.Equal("a", f.Text);
    }
}
