using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Le mode Libre en vrai champ (QCM d'Antoine du 09/11 ; audit UX E6, E7, E8, L13), de bout en
/// bout sur la fenêtre cachée : touches par OnKeyDown puis le WM_CHAR qui suit, comme Windows ;
/// modificateurs et presse-papiers factices, jamais ceux de la machine.
/// </summary>
public class FreeModeFieldTests
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
            var modeType = typeof(LessonsWindow).GetNestedType("WindowMode", System.Reflection.BindingFlags.NonPublic)!;
            BancCapture.Call(Window, "SwitchMode", Enum.Parse(modeType, "Free"));
            Paint();
        }

        public TextEditBuffer Buffer => BancCapture.Field<TextEditBuffer>(Window, "_free");
        public string Text => Buffer.Text;

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

        public IntPtr PointIn(double fx, double fy)
        {
            var r = BancCapture.Field<Win32.RECT>(Window, "_freePreviewRect");
            int x = r.left + (int)((r.right - r.left) * fx) - BancCapture.Field<int>(Window, "_scrollX");
            int y = r.top + (int)((r.bottom - r.top) * fy) - BancCapture.Field<int>(Window, "_scrollY");
            return (IntPtr)((y << 16) | (x & 0xFFFF));
        }

        public void Dispose() => BancCapture.Teardown(Window);
    }

    [Fact]
    public void Ctrl_A_selects_everything_and_types_nothing()
    {
        using var isolation = new BancCapture.Isolation();
        using var f = new Field();
        f.Type("bonjour");
        f.Ctrl('A');
        Assert.Equal("bonjour", f.Text);
        Assert.Equal("bonjour", f.Buffer.SelectedText);
        f.Type("x");
        Assert.Equal("x", f.Text); // la frappe remplace la sélection
    }

    [Fact]
    public void Copy_cut_paste_and_undo_go_through_the_clipboard()
    {
        using var isolation = new BancCapture.Isolation();
        using var f = new Field();
        f.Type("un deux");
        f.Ctrl('A');
        f.Ctrl('C');
        Assert.Equal("un deux", f.Clip.Content);
        f.Ctrl('X');
        Assert.Equal("", f.Text);
        f.Ctrl('V');
        f.Ctrl('V');
        Assert.Equal("un deuxun deux", f.Text);
        f.Ctrl('Z');
        Assert.Equal("un deux", f.Text);
        f.Ctrl('Y');
        Assert.Equal("un deuxun deux", f.Text);
    }

    [Fact]
    public void A_paste_is_not_counted_as_typing()
    {
        using var isolation = new BancCapture.Isolation();
        using var f = new Field();
        f.Clip.Content = "un long texte collé";
        f.Ctrl('V');
        Assert.Equal(0, BancCapture.Field<int>(f.Window, "_freeChars"));
        f.Type("ab");
        Assert.Equal(2, BancCapture.Field<int>(f.Window, "_freeChars"));
    }

    [Fact]
    public void Shift_and_Ctrl_arrows_select_by_word()
    {
        using var isolation = new BancCapture.Isolation();
        using var f = new Field();
        f.Type("le chat dort");
        f.Key(0x25, mods: new EditModifiers(true, true, false, false, false)); // Ctrl+Maj+Gauche
        Assert.Equal("dort", f.Buffer.SelectedText);
        f.Key(0x08); // Retour arrière efface la sélection
        Assert.Equal("le chat ", f.Text);
        f.Key(0x08, mods: CtrlMods); // Ctrl+Retour arrière : le mot
        Assert.Equal("le ", f.Text);
        f.Key(0x24, mods: ShiftMods); // Maj+Origine
        Assert.Equal("le ", f.Buffer.SelectedText);
    }

    [Fact]
    public void Enter_starts_a_new_line()
    {
        using var isolation = new BancCapture.Isolation();
        using var f = new Field();
        f.Type("ligne 1\nligne 2");
        Assert.Equal("ligne 1\nligne 2", f.Text);
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
    public void No_220_cap_and_a_full_field_says_so()
    {
        using var isolation = new BancCapture.Isolation();
        using var f = new Field();
        f.Type(new string('a', 300));
        Assert.Equal(300, f.Text.Length); // l'ancien plafond coupait à 220 en silence

        f.Clip.Content = new string('b', LessonsWindow.FREE_MAX_CHARS);
        f.Ctrl('V');
        Assert.Equal(LessonsWindow.FREE_MAX_CHARS, f.Text.Length);
        Assert.True(BancCapture.Field<bool>(f.Window, "_freeTruncated"));
        f.Paint(); // le message se dessine sans erreur, et un repeint d'un champ plein reste possible
        f.Key(0x08);
        Assert.False(BancCapture.Field<bool>(f.Window, "_freeTruncated"));
    }

    [Fact]
    public void Escape_closes_but_keeps_the_text_and_reset_stats_keeps_it_too()
    {
        using var isolation = new BancCapture.Isolation();
        using var f = new Field();
        f.Type("à garder");
        f.Key(0x1B); // Échap : la fenêtre se ferme
        Assert.False(BancCapture.Field<bool>(f.Window, "_visible"));
        Assert.Equal("à garder", f.Text);
        Assert.Equal(0, BancCapture.Field<int>(f.Window, "_freeChars")); // la séance de mesure, elle, repart

        BancCapture.Call(f.Window, "ResetFreeStats");
        Assert.Equal("à garder", f.Text); // L13 : effacer les statistiques n'efface plus le texte
    }

    [Fact]
    public void Mouse_drag_selects_and_release_keeps_the_selection()
    {
        using var isolation = new BancCapture.Isolation();
        using var f = new Field();
        f.Type("un texte assez long pour glisser dessus");
        f.Paint();
        Assert.True((bool)BancCapture.Call(f.Window, "OnFieldMouseDown", f.PointIn(0.01, 0.15), IntPtr.Zero)!);
        Assert.Equal(0, f.Buffer.Caret);
        BancCapture.Call(f.Window, "OnFieldMouseDrag", f.PointIn(0.99, 0.15));
        BancCapture.Call(f.Window, "EndFieldDrag");
        Assert.True(f.Buffer.HasSelection);
        string selected = f.Buffer.SelectedText;
        Assert.StartsWith("un texte", selected);

        // Le WM_LBUTTONUP qui suit un glisser ne passe plus par OnClick ; un clic simple
        // ne repose pas non plus le curseur au relâchement.
        BancCapture.Call(f.Window, "OnClick", f.PointIn(0.5, 0.15));
        Assert.Equal(selected, f.Buffer.SelectedText);
    }

    [Fact]
    public void Shift_click_extends_and_double_click_selects_a_word()
    {
        using var isolation = new BancCapture.Isolation();
        using var f = new Field();
        f.Type("alpha beta gamma");
        f.Paint();
        BancCapture.Call(f.Window, "OnFieldMouseDown", f.PointIn(0.01, 0.15), IntPtr.Zero);
        BancCapture.Call(f.Window, "EndFieldDrag");
        BancCapture.Call(f.Window, "OnFieldMouseDown", f.PointIn(0.99, 0.15), (IntPtr)0x0004); // MK_SHIFT
        BancCapture.Call(f.Window, "EndFieldDrag");
        Assert.Equal("alpha beta gamma", f.Buffer.SelectedText);

        BancCapture.Call(f.Window, "OnDoubleClick", f.PointIn(0.01, 0.15));
        Assert.Equal("alpha", f.Buffer.SelectedText);
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
    public void Long_text_wraps_by_word_and_keeps_the_caret_visible()
    {
        using var isolation = new BancCapture.Isolation();
        using var f = new Field();
        f.Clip.Content = string.Concat(Enumerable.Repeat("mot ", 3000));
        var watch = System.Diagnostics.Stopwatch.StartNew();
        f.Ctrl('V');
        for (int i = 0; i < 20; i++)
        {
            f.Type("x");
            f.Paint();
        }
        Assert.True(watch.ElapsedMilliseconds < 5000, $"{watch.ElapsedMilliseconds} ms pour 20 frappes sur 12 000 caractères");
        var caret = BancCapture.Field<Win32.RECT?>(f.Window, "_systemCaretRect")!.Value;
        var box = BancCapture.Field<Win32.RECT>(f.Window, "_freePreviewRect");
        Assert.InRange(caret.top, box.top, box.bottom);
    }
}
