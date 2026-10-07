// Le clavier « papier » de la refonte 1.3.0 : Leçons F (carte à quatre légendes) et clavier
// virtuel A (couche active), maquettes du 07/10. Le tutoriel d'accueil garde KeyboardRenderer.
namespace AZERTYGlobal;

internal enum PaperKeyboardMode
{
    /// <summary>Leçons : les quatre légendes de chaque touche, lettre seule en haut à gauche.</summary>
    Map,
    /// <summary>Clavier virtuel : un signe par touche, celui qu'elle tape maintenant.</summary>
    ActiveLayer,
}

/// <summary>Ce que le clavier montre : modificateurs tenus, touche morte en attente, touches à
/// frapper et leur ordre.</summary>
internal sealed class PaperKeyboardState
{
    public bool Shift { get; init; }
    public bool AltGr { get; init; }
    public bool CapsLock { get; init; }
    public bool Ctrl { get; init; }
    public bool Alt { get; init; }
    public string? ActiveDeadKey { get; init; }
    public uint PressedScancode { get; init; }
    /// <summary>Touche de la dernière faute (Leçons), cerclée jusqu'à la correction.</summary>
    public uint ErrorScancode { get; init; }
    /// <summary>Touches à frapper ou tenir (remplies en bleu). Les touches de contexte sans
    /// scancode (Maj, AltGr, Alt) se donnent par <see cref="TargetContextLabels"/>.</summary>
    public HashSet<uint> TargetScancodes { get; } = new();
    public HashSet<string> TargetContextLabels { get; } = new(StringComparer.Ordinal);
    /// <summary>Ordre des gestes venus de la recherche : pastille « 1 » ou « 2 ».</summary>
    public Dictionary<uint, int> StepBadges { get; } = new();
    /// <summary>Pastilles des touches de contexte sans scancode (Maj, AltGr), par étiquette.</summary>
    public Dictionary<string, int> ContextStepBadges { get; } = new(StringComparer.Ordinal);
    /// <summary>Clavier virtuel : le nom que la consigne donne à une touche à pastille
    /// (« Frappez ù »), gravure de <see cref="Gesture.KeyLabel"/>. La touche le montre en
    /// grand, et ce qu'elle tape dans la couche courante en petit (revue du 07/10, P1 n°2).</summary>
    public Dictionary<uint, string> StepKeyNames { get; } = new();
    /// <summary>Leçons : filtre des légendes (<see cref="KeyboardRenderProfile.Lesson"/>) ou
    /// carte complète (<see cref="KeyboardRenderProfile.Full"/>).</summary>
    public KeyboardRenderProfile Profile { get; init; } = KeyboardRenderProfile.Full;
    public HashSet<string> LessonVisibleCharacters { get; } = new(StringComparer.Ordinal);
    public string? HintCharacter { get; init; }
}

internal static class PaperKeyboard
{
    /// <summary>Touche de 48 px des maquettes : les tailles de légende s'y rapportent.</summary>
    private const float MockKey = 48f;

    private const uint ScanSpace = 0x39;
    private const uint ScanBackspace = 0x0E;
    private const uint ScanEnter = 0x1C;

    /// <summary>Le clavier centré dans <paramref name="bounds"/>, à la plus grande échelle qui
    /// tient ; même géométrie que <see cref="KeyboardRenderer"/>, donc mêmes zones de clic
    /// (<see cref="KeyboardRenderer.BuildHitTestRects(KeyboardPlacement)"/>).</summary>
    internal static KeyboardPlacement Place(Win32.RECT bounds) => KeyboardRenderer.Place(bounds);

    /// <summary>Le clavier à une taille de touche fixe (px par unité), en haut à gauche de
    /// <paramref name="origin"/>.</summary>
    internal static KeyboardPlacement PlaceAt(int x, int y, float unit) => new(x, y, unit);

    /// <summary>Taille occupée par le clavier à une échelle donnée.</summary>
    internal static (int Width, int Height) Size(float unit)
    {
        float right = 0, bottom = 0;
        foreach (var k in KeyboardRenderer.VisualKeys)
        {
            right = Math.Max(right, k.X + k.W);
            bottom = Math.Max(bottom, k.Y + k.H);
        }
        return ((int)(right * unit), (int)(bottom * unit));
    }

    internal static void Draw(IntPtr hdc, KeyboardPlacement placement, Layout layout, PaperKeyboardMode mode,
        PaperKeyboardState state)
    {
        Win32.SetBkMode(hdc, Win32.TRANSPARENT);
        float k = placement.Scale / MockKey;
        foreach (var key in KeyboardRenderer.VisualKeys)
            DrawKey(hdc, ToRect(key, placement), key, layout, mode, state, k);
    }

    /// <summary>Le cadre d'une touche, comme <see cref="KeyboardRenderer"/> le calcule.</summary>
    internal static Win32.RECT ToRect(VirtualKeyboard.VisualKey key, KeyboardPlacement p) => new()
    {
        left = p.OriginX + (int)(key.X * p.Scale),
        top = p.OriginY + (int)(key.Y * p.Scale),
        right = p.OriginX + (int)((key.X + key.W) * p.Scale) - 1,
        bottom = p.OriginY + (int)((key.Y + key.H) * p.Scale) - 1,
    };

    // ── Une touche ─────────────────────────────────────────────────────

    private static void DrawKey(IntPtr hdc, Win32.RECT rect, VirtualKeyboard.VisualKey key, Layout layout,
        PaperKeyboardMode mode, PaperKeyboardState state, float k)
    {
        bool isoEnter = key.Scancode == ScanEnter && key.H > VirtualKeyboard.KEY_H;
        bool function = key.IsContextual;
        KeyDefinition? def = null;
        if (!function && key.Scancode != 0)
            layout.Keys.TryGetValue(key.Scancode, out def);
        if (def != null && state.Profile != KeyboardRenderProfile.Full)
            def = Filter(def, key.Scancode, state);

        bool held = function && IsHeld(key, state);
        bool target = (key.Scancode != 0 && state.TargetScancodes.Contains(key.Scancode))
                      || (function && state.TargetContextLabels.Contains(key.ContextId ?? key.Label));
        state.StepBadges.TryGetValue(key.Scancode, out int step);
        if (key.Scancode == 0) step = 0;
        if (function && step == 0 && state.ContextStepBadges.TryGetValue(key.ContextId ?? key.Label, out int contextStep))
            step = contextStep;
        bool pressed = key.Scancode != 0 && key.Scancode == state.PressedScancode;
        bool error = key.Scancode != 0 && key.Scancode == state.ErrorScancode;

        // Ce que la touche montre en couche active.
        string? active = null;
        bool activeIsDeadKey = false, armed = false;
        if (mode == PaperKeyboardMode.ActiveLayer && def != null)
        {
            if (state.ActiveDeadKey != null)
            {
                string? raw = def.GetOutput(state.Shift, state.AltGr, state.CapsLock);
                armed = raw == state.ActiveDeadKey;
                active = DeadKeyResult(raw, layout, state.ActiveDeadKey);
            }
            else
            {
                active = def.GetOutput(state.Shift, state.AltGr, state.CapsLock);
                activeIsDeadKey = IsDeadKeyRef(active);
            }
        }
        bool empty = mode == PaperKeyboardMode.ActiveLayer && !function && key.Scancode != ScanSpace
                     && string.IsNullOrEmpty(active);

        // Fond et contour.
        uint fill = function ? PaperTheme.FunctionKey : PaperTheme.Surface;
        uint border = PaperTheme.Border;
        int borderWidth = 1;
        int penStyle = Win32.PS_SOLID;
        bool onAction = false;
        int stateWidth = Math.Max(2, (int)Math.Round(2 * k));
        if (mode == PaperKeyboardMode.ActiveLayer && !held && (target || step > 0))
        {
            // Couche active : le plein bleu dit « tenu en ce moment » ; un geste à faire est
            // cerné de bleu et numéroté (revue du 07/10, P1 du clavier A).
            border = PaperTheme.Action;
            borderWidth = stateWidth;
        }
        else if (held || target || step > 0)
        {
            fill = border = PaperTheme.Action;
            onAction = true;
        }
        else if (armed)
        {
            fill = PaperTheme.ActionTint;
            border = PaperTheme.Action;
            borderWidth = stateWidth;
        }
        else if (empty)
        {
            fill = PaperTheme.Paper;
            border = PaperTheme.KeyFadedBorder;
        }
        else if (activeIsDeadKey)
        {
            border = PaperTheme.TextSecondary;
            penStyle = Win32.PS_DASH;
        }
        if (error)
        {
            border = PaperTheme.Error;
            borderWidth = stateWidth;
        }
        else if (pressed && !onAction)
        {
            fill = PaperTheme.ActionTint;
            border = PaperTheme.Action;
            borderWidth = stateWidth;
        }

        int radius = Math.Max(2, (int)Math.Round(3 * k));
        if (isoEnter)
            DrawIsoEnter(hdc, rect, key, fill, border, borderWidth);
        else
            PaperDraw.RoundBox(hdc, rect, fill, border, radius, borderWidth, penStyle);

        // La faute se voit aussi hors de la touche : un anneau rouge dans l'interstice. Le
        // liseré seul disparaissait sur une touche cible pleine bleue (faute à l'espace fine).
        if (error)
            ErrorRing(hdc, rect, radius, stateWidth);

        // Contenu.
        var nameArea = isoEnter ? IsoEnterTop(rect, key) : rect;
        if (function || (def == null && key.Scancode != ScanSpace))
            DrawFunctionName(hdc, nameArea, key, mode, onAction, k);
        else if (mode == PaperKeyboardMode.ActiveLayer && step > 0
                 && state.StepKeyNames.TryGetValue(key.Scancode, out var stepName))
            DrawStepKeyName(hdc, rect, key, stepName, active, onAction, k);
        else if (mode == PaperKeyboardMode.ActiveLayer)
            DrawActiveSign(hdc, rect, key, active, activeIsDeadKey, onAction, k);
        else if (def != null)
            DrawLegends(hdc, rect, key, def, onAction, k);

        if (step > 0)
            DrawBadge(hdc, rect, step, k);
    }

    private static bool IsHeld(VirtualKeyboard.VisualKey key, PaperKeyboardState state) => key.Label switch
    {
        "Maj ⇧" => state.Shift,
        "AltGr" => state.AltGr,
        "Verr. Maj." => state.CapsLock,
        "Ctrl" => state.Ctrl,
        "Alt" => state.Alt,
        _ => false,
    };

    private static KeyDefinition Filter(KeyDefinition def, uint scancode, PaperKeyboardState state)
    {
        string? F(int layer, string? value) => KeyboardRenderer.IsSlotVisible(state.Profile, scancode, layer, value,
            state.LessonVisibleCharacters, state.HintCharacter) ? value : null;
        return new KeyDefinition
        {
            Position = def.Position, Scancode = def.Scancode,
            Base = F(0, def.Base), Shift = F(1, def.Shift), AltGr = F(2, def.AltGr), ShiftAltGr = F(3, def.ShiftAltGr),
            Caps = F(4, def.Caps), CapsShift = F(5, def.CapsShift), CapsAltGr = F(6, def.CapsAltGr),
            CapsShiftAltGr = F(7, def.CapsShiftAltGr),
        };
    }

    /// <summary>Ce que donne la touche après la touche morte en attente ; la touche morte
    /// elle-même donne son accent seul.</summary>
    private static string? DeadKeyResult(string? raw, Layout layout, string activeDeadKey)
    {
        if (string.IsNullOrEmpty(raw) || !layout.DeadKeys.TryGetValue(activeDeadKey, out var dk))
            return null;
        if (IsDeadKeyRef(raw))
        {
            if (raw != activeDeadKey) return null;
            var isolated = dk.GetIsolated();
            return isolated == null ? null : dk.Apply(isolated) ?? isolated;
        }
        return dk.Apply(raw);
    }

    // ── Touches de fonction ────────────────────────────────────────────

    /// <summary>Nom d'une touche de fonction : centré au clavier virtuel (choix d'Antoine, 07/10),
    /// en bas à gauche aux Leçons comme sur la maquette F.</summary>
    private static void DrawFunctionName(IntPtr hdc, Win32.RECT rect, VirtualKeyboard.VisualKey key, PaperKeyboardMode mode,
        bool onAction, float k)
    {
        string name = FunctionName(key.Label);
        bool backspace = key.Scancode == ScanBackspace;
        uint color = onAction ? PaperTheme.OnAction : PaperTheme.TextSecondary;
        if (mode == PaperKeyboardMode.ActiveLayer || backspace)
        {
            int size = backspace ? 20 : 12;
            var font = FontSpec.Of(backspace ? AppFonts.Symbols : AppFonts.Text, Scaled(size, k * MockKey / 50f), onAction ? TypeRamp.Semibold : TypeRamp.Regular, 1f);
            PaperText.DrawLine(hdc, name, font, rect, color, TextAlign.Center);
            return;
        }
        // Centré comme sur le clavier virtuel : un seul style de touche de fonction (QCM du 07/10).
        var small = FontSpec.Of(AppFonts.Text, Scaled(11, k), onAction ? TypeRamp.Semibold : TypeRamp.Regular, 1f);
        PaperText.DrawLine(hdc, name, small, rect, color, TextAlign.Center);
    }

    /// <summary>Nom affiché : « Maj » sans flèche ; le reste comme partout ailleurs.</summary>
    private static string FunctionName(string label) => label switch
    {
        "Maj ⇧" => L.Gesture_LineShift,
        _ => L.Keyboard_KeyCap(label),
    };

    /// <summary>Partie haute de l'Entrée ISO, où se pose son nom.</summary>
    private static Win32.RECT IsoEnterTop(Win32.RECT rect, VirtualKeyboard.VisualKey key)
    {
        int height = rect.bottom - rect.top;
        return rect with { bottom = rect.top + (int)(height * (VirtualKeyboard.KEY_H / key.H)) };
    }

    private static void DrawIsoEnter(IntPtr hdc, Win32.RECT rect, VirtualKeyboard.VisualKey key, uint fill, uint border, int borderWidth)
    {
        int width = rect.right - rect.left;
        int stepY = IsoEnterTop(rect, key).bottom;
        int bottomLeft = rect.right - (int)(width * (1.25f / key.W));
        var pts = new Win32.POINT[]
        {
            new() { x = rect.left, y = rect.top },
            new() { x = rect.right, y = rect.top },
            new() { x = rect.right, y = rect.bottom },
            new() { x = bottomLeft, y = rect.bottom },
            new() { x = bottomLeft, y = stepY },
            new() { x = rect.left, y = stepY },
        };
        var brush = Win32.CreateSolidBrush(fill);
        var pen = Win32.CreatePen(Win32.PS_SOLID, borderWidth, border);
        var oldBrush = Win32.SelectObject(hdc, brush);
        var oldPen = Win32.SelectObject(hdc, pen);
        Win32.Polygon(hdc, pts, pts.Length);
        Win32.SelectObject(hdc, oldPen);
        Win32.SelectObject(hdc, oldBrush);
        Win32.DeleteObject(pen);
        Win32.DeleteObject(brush);
    }

    // ── Couche active (clavier virtuel A) ──────────────────────────────

    private static void DrawActiveSign(IntPtr hdc, Win32.RECT rect, VirtualKeyboard.VisualKey key, string? value,
        bool deadKey, bool onAction, float k)
    {
        if (string.IsNullOrEmpty(value)) return;
        float u = k * MockKey / 50f; // tailles de la maquette A, touche de 50 px
        uint ink = onAction ? PaperTheme.OnAction : PaperTheme.Ink;

        // Espace : ce qu'elle tape se dit en mots (« insécable fine »).
        if (key.Scancode == ScanSpace && value.Length == 1 && char.IsWhiteSpace(value[0]))
        {
            if (value == " ") return;
            var nameFont = FontSpec.Of(AppFonts.Text, Scaled(12, u), TypeRamp.Regular, 1f);
            PaperText.DrawLine(hdc, SpaceName(value), nameFont, rect, onAction ? PaperTheme.OnAction : PaperTheme.TextSecondary, TextAlign.Center);
            return;
        }

        string display = Display(value);
        if (display.Length > 2 && !display.StartsWith('◌'))
        {
            // Nom d'un invisible (« esp. ins. ») : petit, comme un nom de touche.
            var nameFont = FontSpec.Of(AppFonts.Text, Scaled(12, u), TypeRamp.Regular, 1f);
            PaperText.DrawLine(hdc, display, nameFont, rect, onAction ? PaperTheme.OnAction : PaperTheme.TextSecondary, TextAlign.Center);
            return;
        }
        var font = FontSpec.Of(AppFonts.Title, Scaled(deadKey ? 28 : 23, u), TypeRamp.Semibold, 1f);
        PaperText.DrawLine(hdc, display, font, rect, ink, TextAlign.Center);
    }

    /// <summary>Touche à pastille : le nom que dit la consigne (« ù ») en grand, ce qu'elle tape
    /// maintenant (« ´ ») en petit en haut à gauche ; la pastille garde le coin haut droit.</summary>
    private static void DrawStepKeyName(IntPtr hdc, Win32.RECT rect, VirtualKeyboard.VisualKey key, string name,
        string? active, bool onAction, float k)
    {
        float u = k * MockKey / 50f;
        uint ink = onAction ? PaperTheme.OnAction : PaperTheme.Ink;
        uint secondary = onAction ? PaperTheme.OnAction : PaperTheme.TextSecondary;
        bool space = key.Scancode == ScanSpace;
        // Un mot (« Espace ») se pose comme un nom de touche ; une gravure comme un signe.
        var nameFont = name.Length > 2
            ? FontSpec.Of(AppFonts.Text, Scaled(12, u), TypeRamp.Semibold, 1f)
            : FontSpec.Of(AppFonts.Title, Scaled(23, u), TypeRamp.Semibold, 1f);
        PaperText.DrawLine(hdc, name, nameFont, rect, ink, TextAlign.Center);

        string? typed = TypedBesideName(name, active, space);
        if (typed == null) return;
        // Le signe dans la police des signes, assez grand pour qu'un accent seul se lise.
        // Un accent seul a un trait fin : il garde la taille et l'encre d'un signe.
        bool accent = IsDeadKeyRef(active);
        var font = space
            ? FontSpec.Of(AppFonts.Text, Scaled(11, u), TypeRamp.Regular, 1f)
            : FontSpec.Of(AppFonts.Title, Scaled(accent ? 22 : 13, u), TypeRamp.Semibold, 1f);
        var (ascent, descent) = PaperText.Metrics(hdc, font);
        // En haut à gauche : un accent seul y reste un accent (en bas, « ´ » passait pour une
        // virgule) ; la pastille tient le coin d'en face.
        int lineTop = rect.top + Scaled(accent ? -2 : 2, u);
        int baseline = lineTop + (font.Height - (ascent + descent)) / 2 + ascent;
        int x = rect.left + Scaled(6, u);
        PaperText.Draw(hdc, typed, font, x, baseline, accent ? ink : secondary);
    }

    /// <summary>Le petit signe d'une touche à pastille : ce qu'elle tape dans la couche courante,
    /// ou rien s'il redit son nom (« z » sous « Z ») ou si elle ne tape rien.</summary>
    internal static string? TypedBesideName(string name, string? active, bool space)
    {
        if (string.IsNullOrEmpty(active) || (space && active == " ")) return null;
        string typed = space && active.Length == 1 && char.IsWhiteSpace(active[0]) ? SpaceName(active) : Display(active);
        return string.Equals(typed, name, StringComparison.OrdinalIgnoreCase) ? null : typed;
    }

    // ── Carte à quatre légendes (Leçons F) ─────────────────────────────

    private static void DrawLegends(IntPtr hdc, Win32.RECT rect, VirtualKeyboard.VisualKey key, KeyDefinition def,
        bool onAction, float k)
    {
        uint ink = onAction ? PaperTheme.OnAction : PaperTheme.Ink;
        uint secondary = onAction ? PaperTheme.OnAction : PaperTheme.TextSecondary;
        int left = rect.left + Scaled(6, k), right = rect.right - Scaled(5, k);
        int top = rect.top + Scaled(5, k), bottom = rect.bottom - Scaled(6, k);

        if (key.Scancode == ScanSpace)
        {
            var nameFont = FontSpec.Of(AppFonts.Text, Scaled(10, k), onAction ? TypeRamp.Semibold : TypeRamp.Regular, 1f);
            Corner(hdc, SpaceLegend(def.ShiftAltGr), nameFont, left, right, top, bottom, Corner_.TopRight, secondary, false);
            Corner(hdc, SpaceLegend(def.AltGr), nameFont, left, right, top, bottom, Corner_.BottomRight, secondary, false);
            return;
        }

        var mono = (int size) => FontSpec.Of(AppFonts.Mono, Scaled(size, k), TypeRamp.Regular, 1f);
        bool letter = KeyboardRenderer.IsLetterKey(key.Scancode) && def.Base is { Length: 1 } b && char.IsLetter(b[0]);
        string? shiftAltGr = def.ShiftAltGr != def.AltGr ? def.ShiftAltGr : null;
        if (letter)
        {
            Corner(hdc, def.Base, mono(17), left, right, top, bottom, Corner_.TopLeft, ink, IsDeadKeyRef(def.Base));
        }
        else
        {
            Corner(hdc, def.Shift, mono(SecondaryLegend), left, right, top, bottom, Corner_.TopLeft, secondary, IsDeadKeyRef(def.Shift));
            Corner(hdc, def.Base, mono(15), left, right, top, bottom, Corner_.BottomLeft, ink, IsDeadKeyRef(def.Base));
        }
        Corner(hdc, shiftAltGr, mono(SecondaryLegend), left, right, top, bottom, Corner_.TopRight, secondary, IsDeadKeyRef(shiftAltGr));
        Corner(hdc, def.AltGr, mono(SecondaryLegend), left, right, top, bottom, Corner_.BottomRight, secondary, IsDeadKeyRef(def.AltGr));
    }

    /// <summary>Taille commune des trois légendes secondaires (Maj, AltGr, Maj+AltGr) : 10 et
    /// 11 px rendaient ẞ plus petit que ß et les signes fins (! ¡ #) peu lisibles (QCM du 07/10).</summary>
    private const int SecondaryLegend = 13;

    private enum Corner_ { TopLeft, TopRight, BottomLeft, BottomRight }

    /// <summary>Une légende dans un coin ; une touche morte a son signe dans un cadre pointillé.</summary>
    private static void Corner(IntPtr hdc, string? value, FontSpec font, int left, int right, int top, int bottom,
        Corner_ corner, uint color, bool deadKey)
    {
        if (string.IsNullOrEmpty(value)) return;
        string text = Display(value);
        int width = PaperText.Width(hdc, text, font);
        var (ascent, descent) = PaperText.Metrics(hdc, font);
        bool atTop = corner is Corner_.TopLeft or Corner_.TopRight;
        bool atLeft = corner is Corner_.TopLeft or Corner_.BottomLeft;
        // Les maquettes placent la boîte de ligne (line-height 1) contre le bord : le haut des
        // capitales y touche presque, le bas des jambages aussi.
        int lineHeight = font.Height;
        int lineTop = atTop ? top : bottom - lineHeight;
        int baseline = lineTop + (lineHeight - (ascent + descent)) / 2 + ascent;
        int pad = deadKey ? Math.Max(2, font.Height / 6) : 0;
        int x = atLeft ? left + pad : right - width - pad;
        PaperText.Draw(hdc, text, font, x, baseline, color);
        if (deadKey)
        {
            var box = new Win32.RECT { left = x - pad, top = lineTop - 1, right = x + width + pad, bottom = lineTop + lineHeight + 1 };
            DottedFrame(hdc, box, color);
        }
    }

    private static void ErrorRing(IntPtr hdc, Win32.RECT r, int radius, int width)
    {
        var pen = Win32.CreatePen(Win32.PS_SOLID, 1, PaperTheme.Error);
        var oldPen = Win32.SelectObject(hdc, pen);
        var oldBrush = Win32.SelectObject(hdc, Win32.GetStockObject(Win32.NULL_BRUSH));
        for (int i = 1; i <= width; i++)
        {
            int round = (radius + i) * 2;
            Win32.RoundRect(hdc, r.left - i, r.top - i, r.right + 1 + i, r.bottom + 1 + i, round, round);
        }
        Win32.SelectObject(hdc, oldBrush);
        Win32.SelectObject(hdc, oldPen);
        Win32.DeleteObject(pen);
    }

    private static void DottedFrame(IntPtr hdc, Win32.RECT r, uint color)
    {
        var pen = Win32.CreatePen(Win32.PS_DOT, 1, color);
        var oldPen = Win32.SelectObject(hdc, pen);
        var oldBrush = Win32.SelectObject(hdc, Win32.GetStockObject(Win32.NULL_BRUSH));
        Win32.RoundRect(hdc, r.left, r.top, r.right, r.bottom, 5, 5);
        Win32.SelectObject(hdc, oldBrush);
        Win32.SelectObject(hdc, oldPen);
        Win32.DeleteObject(pen);
    }

    // ── Pastille d'ordre ───────────────────────────────────────────────

    /// <summary>Pastille « 1 » ou « 2 » : dans quel ordre frapper les gestes venus de la recherche.</summary>
    private static void DrawBadge(IntPtr hdc, Win32.RECT rect, int step, float k)
    {
        float u = k * MockKey / 50f;
        int size = Scaled(17, u), inset = Scaled(4, u);
        var r = new Win32.RECT { left = rect.right - inset - size, top = rect.top + inset, right = rect.right - inset, bottom = rect.top + inset + size };
        var brush = Win32.CreateSolidBrush(PaperTheme.Surface);
        var pen = Win32.CreatePen(Win32.PS_SOLID, 1, PaperTheme.Action);
        var oldBrush = Win32.SelectObject(hdc, brush);
        var oldPen = Win32.SelectObject(hdc, pen);
        Win32.Ellipse(hdc, r.left, r.top, r.right + 1, r.bottom + 1);
        Win32.SelectObject(hdc, oldPen);
        Win32.SelectObject(hdc, oldBrush);
        Win32.DeleteObject(pen);
        Win32.DeleteObject(brush);
        PaperText.DrawLine(hdc, step.ToString(System.Globalization.CultureInfo.InvariantCulture),
            FontSpec.Of(AppFonts.Text, Scaled(11, u), TypeRamp.Semibold, 1f), r, PaperTheme.Action, TextAlign.Center);
    }

    // ── Signes ─────────────────────────────────────────────────────────

    /// <summary>Le signe affiché : accent d'une touche morte, « ◌ » devant une marque
    /// combinante, nom d'un invisible.</summary>
    private static string Display(string value)
    {
        string result = IsDeadKeyRef(value) ? TrayApplication.GetDeadKeySymbol(value) : KeyboardRenderer.DisplayInvisible(value);
        return result.Length == 1 && KeyboardRenderer.IsCombiningMark(result[0]) ? "◌" + result : result;
    }

    private static string SpaceName(string value) => value switch
    {
        " " => L.PaperKeyboard_Nbsp,
        " " => L.PaperKeyboard_NarrowNbsp,
        _ => KeyboardRenderer.DisplayInvisible(value),
    };

    private static string? SpaceLegend(string? value) => string.IsNullOrEmpty(value) ? null : SpaceName(value);

    private static bool IsDeadKeyRef(string? s) => s != null && s.StartsWith("dk_", StringComparison.Ordinal);

    private static int Scaled(float size, float k) => Math.Max(1, (int)Math.Round(size * k));
}
