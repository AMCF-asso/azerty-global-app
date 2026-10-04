namespace AZERTYGlobal;

internal enum KeyboardRenderProfile
{
    Full,
    /// <summary>Tutoriel de l'accueil : ses couleurs de surlignage, ses pastilles, son Retour
    /// arrière terne et sa place propre (lot 9, audit du 25/09 L-01).</summary>
    Onboarding,
    Lesson
}

/// <summary>
/// Genre de surlignage d'une touche guidée : touche à presser (direct), armement d'une touche
/// morte (étape 1), touche finale après l'armement (étape 2). Le tutoriel les distingue par la
/// couleur et une pastille « 1 » ou « 2 » ; les Leçons n'en montrent qu'un, en contour vert.
/// </summary>
internal enum KeyHighlight
{
    Direct,
    Step1,
    Step2
}

/// <summary>Place du clavier : coin haut-gauche et pixels par unité de touche.</summary>
internal readonly record struct KeyboardPlacement(int OriginX, int OriginY, float Scale);

/// <summary>Polices du clavier : caractère principal, résultat de touche morte, couches
/// secondaires, petites mentions, touches de contexte, pastilles du tutoriel.</summary>
internal readonly record struct KeyboardFonts(IntPtr Main, IntPtr DeadKey, IntPtr Small, IntPtr Tiny, IntPtr Context,
    IntPtr Badge = default, IntPtr OverlayMain = default, IntPtr OverlaySmall = default);

internal sealed class KeyboardRenderState
{
    public bool Shift { get; init; }
    public bool AltGr { get; init; }
    public bool Ctrl { get; init; }
    public bool Alt { get; init; }
    public bool CapsLock { get; init; }
    public string? ActiveDeadKey { get; init; }
    public uint PressedScancode { get; init; }
    public HashSet<uint> HighlightedScancodes { get; } = new();
    public HashSet<string> HighlightedContextIds { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> HighlightedLabels { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> LessonVisibleCharacters { get; } = new(StringComparer.Ordinal);
    public string? HintCharacter { get; init; }
    public bool ShowInvisibleMarkers { get; init; } = true;
    /// <summary>Genre des touches surlignées (tutoriel).</summary>
    public KeyHighlight HighlightKind { get; set; }
    /// <summary>Exercices 1 et 2 du tutoriel : Verr. Maj. reste surlignée en « direct », sans
    /// pastille, quel que soit le genre des autres touches.</summary>
    public bool KeepCapsLockHighlight { get; set; }
    /// <summary>Échelle d'interface du tutoriel (1 à 96 DPI), pour ses pastilles.</summary>
    public float UiScale { get; init; } = 1f;
}

internal readonly record struct KeyboardHitTestResult(uint Scancode, string Label, Win32.RECT Rect);

internal static class KeyboardRenderer
{
    private const uint CLR_KEY = DarkTheme.Key;
    private const uint CLR_KEY_CONTEXT = DarkTheme.KeyContext;
    private const uint CLR_KEY_BORDER = DarkTheme.Border;
    private const uint CLR_KEY_PRESSED = DarkTheme.KeyPressed;
    private const uint CLR_KEY_DISABLED = DarkTheme.KeyDisabled;
    private const uint CLR_MOD_ACTIVE = DarkTheme.KeyPressed;
    private const uint CLR_KEY_HIGHLIGHT_BORDER = DarkTheme.HighlightDirect;
    private const uint CLR_CTX_TEXT = DarkTheme.TextSecondary;
    private const uint CLR_KEY_LABEL = DarkTheme.TextSecondary;
    private const uint CLR_CHAR_ACTIVE_BLUE = DarkTheme.Accent;
    private const uint CLR_CHAR_DIM = DarkTheme.TextTertiary;
    private const uint CLR_DK_CHAR = DarkTheme.DeadKey;
    private const uint CLR_DK_ACTIVE_TEXT = DarkTheme.Warning;
    private const uint CLR_CAPS_BAR = DarkTheme.Warning;

    // Surlignage du tutoriel, contour puis fond : direct vert, étape 1 orange, étape 2 vert.
    private const uint CLR_HL_DIRECT_BG = DarkTheme.HighlightDirectFill;
    private const uint CLR_HL_STEP1 = DarkTheme.HighlightStep1;
    private const uint CLR_HL_STEP1_BG = DarkTheme.HighlightStep1Fill;
    private const uint CLR_HL_STEP2 = DarkTheme.HighlightStep2;
    private const uint CLR_HL_STEP2_BG = DarkTheme.HighlightStep2Fill;

    private static readonly HashSet<uint> LetterKeyScancodes = new()
    {
        0x10, 0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17, 0x18, 0x19,
        0x1E, 0x1F, 0x20, 0x21, 0x22, 0x23, 0x24, 0x25, 0x26, 0x27,
        0x2C, 0x2D, 0x2E, 0x2F, 0x30, 0x31
    };

    private static readonly HashSet<uint> AccentedNumericScancodes = new()
    {
        0x03, 0x08, 0x0A, 0x0B
    };

    private static readonly HashSet<string> HiddenDeadKeysInOnboarding = new(StringComparer.Ordinal)
    {
        "dk_misc_symbols",
        "dk_dot_above",
        "dk_dot_below",
        "dk_double_acute",
        "dk_double_grave",
        "dk_horn",
        "dk_hook",
        "dk_breve",
        "dk_inverted_breve",
        "dk_stroke",
        "dk_horizontal_stroke",
        "dk_macron",
        "dk_extended_latin",
        "dk_cedilla",
        "dk_comma",
        "dk_phonetic",
        "dk_ring_above",
        "dk_scientific",
        "dk_caron",
        "dk_ogonek",
        "dk_cyrillic",
        "dk_cyrillic_ext",
    };

    private static readonly HashSet<string> DkAlwaysWithDottedCircle = new(StringComparer.Ordinal)
    {
        "dk_dot_above",
        "dk_double_acute",
        "dk_breve",
        "dk_stroke",
        "dk_horizontal_stroke",
        "dk_caron",
        "dk_ogonek",
    };

    private static readonly HashSet<(uint Scancode, int Layer)> HiddenSlotsInOnboarding = new()
    {
        (0x56, 2), (0x56, 3),
        (0x17, 2),
        (0x26, 2),
        (0x32, 2), (0x32, 3),
        (0x33, 2),
        (0x34, 2),
        (0x35, 2),
        (0x05, 2), (0x05, 3),
        (0x07, 3),
        (0x0B, 2),
        (0x2C, 3),
        (0x2D, 3),
    };

    private static readonly Dictionary<string, (string Fr, string En)> CharNamesOverride = new(StringComparer.Ordinal)
    {
        ["’"] = ("APOSTROPHE TYPOGRAPHIQUE", "TYPOGRAPHIC APOSTROPHE"),
    };

    // Cache : BuildKeyLayout() alloue un tableau ; VisualKeys est lu 2× par WM_PAINT
    // de la fenêtre Leçons (audit 2026-07 n5). Le layout est immuable.
    private static readonly VirtualKeyboard.VisualKey[] _visualKeysCache = VirtualKeyboard.BuildKeyLayout();

    public static IReadOnlyList<VirtualKeyboard.VisualKey> VisualKeys => _visualKeysCache;

    public static bool IsSlotVisible(
        KeyboardRenderProfile profile,
        uint scancode,
        int layer,
        string? value,
        IReadOnlySet<string>? lessonVisibleCharacters = null,
        string? hintCharacter = null)
    {
        if (string.IsNullOrEmpty(value)) return false;
        if (profile == KeyboardRenderProfile.Full) return true;

        // Tutoriel et Leçons : le clavier simplifié, plus ce que l'exercice révèle (l'exercice 6
        // du tutoriel montre ◌/, ¿ et ¡ ; une leçon, ses caractères et l'indice).
        if (IsOnboardingSlotVisible(scancode, layer, value)) return true;
        if (StringComparer.Ordinal.Equals(value, hintCharacter)) return true;
        if (lessonVisibleCharacters?.Contains(value) == true) return true;
        if (value.StartsWith("dk_", StringComparison.Ordinal) &&
            lessonVisibleCharacters?.Contains("dk:" + value[3..]) == true)
            return true;

        return false;
    }

    public static Win32.RECT Draw(
        IntPtr hdc,
        Win32.RECT bounds,
        Layout layout,
        KeyboardRenderProfile profile,
        KeyboardRenderState state,
        IntPtr hFontMain,
        IntPtr hFontDeadKey,
        IntPtr hFontSmall,
        IntPtr hFontTiny,
        IntPtr hFontContext)
    {
        var placement = Place(bounds, out int keyboardW, out int keyboardH);
        int height = Math.Max(1, bounds.bottom - bounds.top);
        int statusHeight = state.ActiveDeadKey != null ? Math.Clamp(height / 13, 18, 28) : 0;

        DrawKeys(hdc, placement, layout, profile, state,
            new KeyboardFonts(hFontMain, hFontDeadKey, hFontSmall, hFontTiny, hFontContext));

        if (state.ActiveDeadKey != null)
            DrawActiveDeadKeyStatus(hdc, new Win32.RECT { left = placement.OriginX, top = bounds.bottom - statusHeight, right = placement.OriginX + keyboardW, bottom = bounds.bottom }, state.ActiveDeadKey, hFontTiny);

        return new Win32.RECT
        {
            left = placement.OriginX,
            top = placement.OriginY,
            right = placement.OriginX + keyboardW,
            bottom = placement.OriginY + keyboardH
        };
    }

    /// <summary>Le clavier centré dans <paramref name="bounds"/>, à la plus grande échelle qui
    /// tient.</summary>
    public static KeyboardPlacement Place(Win32.RECT bounds) => Place(bounds, out _, out _);

    private static KeyboardPlacement Place(Win32.RECT bounds, out int keyboardW, out int keyboardH)
    {
        var visualKeys = VisualKeys;
        float maxRight = visualKeys.Max(k => k.X + k.W);
        float maxBottom = visualKeys.Max(k => k.Y + k.H);
        int width = Math.Max(1, bounds.right - bounds.left);
        int height = Math.Max(1, bounds.bottom - bounds.top);
        float scale = Math.Min(width / maxRight, height / maxBottom);
        keyboardW = (int)(maxRight * scale);
        keyboardH = (int)(maxBottom * scale);
        return new KeyboardPlacement(bounds.left + (width - keyboardW) / 2, bounds.top + (height - keyboardH) / 2, scale);
    }

    /// <summary>
    /// Les touches seules, à une place donnée. Le tutoriel garde la sienne
    /// (<see cref="VirtualKeyboard.GetKeyboardGeometry"/>) et n'a pas la ligne de touche morte :
    /// il la dit au-dessus du clavier. Les Leçons passent par <see cref="Draw"/>.
    /// </summary>
    public static void DrawKeys(IntPtr hdc, KeyboardPlacement placement, Layout layout, KeyboardRenderProfile profile,
        KeyboardRenderState state, KeyboardFonts fonts)
    {
        Win32.SetBkMode(hdc, Win32.TRANSPARENT);
        foreach (var key in VisualKeys)
            DrawKey(hdc, ToRect(key, placement), key, placement, layout, profile, state, fonts);
    }

    public static IEnumerable<KeyboardHitTestResult> BuildHitTestRects(Win32.RECT bounds)
        => BuildHitTestRects(Place(bounds));

    /// <summary>Le cadre de chaque touche, tel que <see cref="DrawKeys"/> la dessine.</summary>
    public static IEnumerable<KeyboardHitTestResult> BuildHitTestRects(KeyboardPlacement placement)
    {
        foreach (var key in VisualKeys)
            yield return new KeyboardHitTestResult(key.Scancode, key.Label, ToRect(key, placement));
    }

    public static string BuildTooltipText(
        Layout layout,
        KeyboardRenderProfile profile,
        KeyboardRenderState state,
        uint scancode,
        string contextLabel)
    {
        bool tutorial = profile == KeyboardRenderProfile.Onboarding;
        if (scancode == 0 || !layout.Keys.TryGetValue(scancode, out var keyDef))
            return GetContextTooltip(contextLabel, tutorial);

        DeadKeyDefinition? activeDk = null;
        if (state.ActiveDeadKey != null)
            layout.DeadKeys.TryGetValue(state.ActiveDeadKey, out activeDk);

        var style = new GlyphStyle(state.ShowInvisibleMarkers, tutorial, layout);
        var sb = new System.Text.StringBuilder();
        AppendTooltipLayer(sb, "Base", keyDef.Base, activeDk, style);
        AppendTooltipLayer(sb, L.Keyboard_LayerShift, keyDef.Shift, activeDk, style);
        AppendTooltipLayer(sb, "AltGr", keyDef.AltGr, activeDk, style);
        AppendTooltipLayer(sb, L.Keyboard_LayerShiftAltGr, keyDef.ShiftAltGr, activeDk, style);
        return sb.ToString().TrimEnd('\n');
    }

    private static string GetContextTooltip(string label, bool tutorial)
    {
        return label switch
        {
            "Tab" => L.Keyboard_TooltipTab,
            // Le tutoriel désactive Retour arrière tant qu'il ne sert pas, et le dit.
            "⌫" => tutorial ? L.Learning_TooltipBackspaceDisabled : L.Keyboard_TooltipBackspace,
            "Verr. Maj." => L.Keyboard_TooltipCapsLock,
            "Maj ⇧" => L.Keyboard_TooltipShift,
            "Entrée" => L.Keyboard_TooltipEnter,
            "Ctrl" => L.Keyboard_TooltipCtrl,
            "Win" => L.Keyboard_TooltipWin,
            "Alt" => L.Keyboard_TooltipAlt,
            "AltGr" => L.Keyboard_TooltipAltGr,
            "Menu" => L.Keyboard_TooltipMenu,
            _ => label
        };
    }

    private static void AppendTooltipLayer(System.Text.StringBuilder sb, string label, string? value, DeadKeyDefinition? activeDk, GlyphStyle style)
    {
        if (string.IsNullOrEmpty(value))
            return;

        if (activeDk != null)
        {
            if (value.StartsWith("dk_", StringComparison.Ordinal))
                return;
            var combined = activeDk.Apply(value);
            if (combined != null)
            {
                string combinedDisplay = DisplayInvisible(combined, style.ShowInvisibleMarkers);
                sb.Append(label).Append(" : ").Append(combinedDisplay);
                AppendCharacterName(sb, combined, combinedDisplay);
                sb.Append('\n');
            }
            return;
        }

        string display = GetDisplayChar(value, style) ?? value;
        sb.Append(label).Append(" : ").Append(display);
        if (value.StartsWith("dk_", StringComparison.Ordinal))
            sb.Append(style.Tutorial ? L.Learning_DeadKeyConnector : L.Keyboard_DeadKeyConnector)
                .Append(VirtualKeyboard.GetDeadKeyDisplayName(value));
        else
            AppendCharacterName(sb, value, display);
        sb.Append('\n');
    }

    private static void AppendCharacterName(System.Text.StringBuilder sb, string value, string display)
    {
        if (CharNamesOverride.TryGetValue(display, out var overrideName))
        {
            var chosen = L.IsEnglish ? overrideName.En : overrideName.Fr;
            sb.Append(" — ").Append(chosen.ToUpperInvariant());
            return;
        }

        if (CharacterIndex.Shared.Names.TryGetValue(value, out var name) ||
            CharacterIndex.Shared.Names.TryGetValue(display, out name))
        {
            // Nom selon la langue de l'UI ; repli sur l'autre langue si absent
            // (même logique que VirtualKeyboard).
            string chosen = L.IsEnglish
                ? (name.En.Length > 0 ? name.En : name.Fr)
                : (name.Fr.Length > 0 ? name.Fr : name.En);
            if (chosen.Length > 0)
                sb.Append(" — ").Append(chosen.ToUpperInvariant());
        }
    }

    /// <summary>
    /// Comment dessiner les caractères : repères des espaces insécables, règles du tutoriel
    /// (touche morte non active en gris, symbole de touche morte lu d'abord dans la disposition,
    /// « ◌ » et accent superposés dans leurs polices propres).
    /// </summary>
    private readonly record struct GlyphStyle(bool ShowInvisibleMarkers, bool Tutorial, Layout Layout,
        IntPtr OverlayMain = default, IntPtr OverlaySmall = default);

    private static KeyHighlight HighlightOf(VirtualKeyboard.VisualKey key, KeyboardRenderState state)
        => state.KeepCapsLockHighlight && key.Label == "Verr. Maj." ? KeyHighlight.Direct : state.HighlightKind;

    private static (uint Border, uint Fill) HighlightColors(KeyHighlight kind) => kind switch
    {
        KeyHighlight.Step1 => (CLR_HL_STEP1, CLR_HL_STEP1_BG),
        KeyHighlight.Step2 => (CLR_HL_STEP2, CLR_HL_STEP2_BG),
        _ => (CLR_KEY_HIGHLIGHT_BORDER, CLR_HL_DIRECT_BG),
    };

    private static void DrawKey(
        IntPtr hdc,
        Win32.RECT rect,
        VirtualKeyboard.VisualKey key,
        KeyboardPlacement placement,
        Layout layout,
        KeyboardRenderProfile profile,
        KeyboardRenderState state,
        KeyboardFonts fonts)
    {
        bool tutorial = profile == KeyboardRenderProfile.Onboarding;
        bool highlighted = key.Scancode != 0 && state.HighlightedScancodes.Contains(key.Scancode);
        if (!highlighted && key.ContextId != null && state.HighlightedContextIds.Contains(key.ContextId))
            highlighted = true;
        if (!highlighted && state.HighlightedLabels.Contains(key.Label))
            highlighted = true;
        bool pressed = key.Scancode != 0 && key.Scancode == state.PressedScancode;
        bool modifierActive = key.Label switch
        {
            "Maj ⇧" => state.Shift,
            "AltGr" => state.AltGr,
            "Ctrl" => state.Ctrl,
            "Alt" => state.Alt,
            "Verr. Maj." => state.CapsLock,
            _ => false
        };
        // Tutoriel : Retour arrière terne tant qu'il ne sert pas ; surligné, il redevient lisible.
        bool disabledBackspace = tutorial && key.IsContextual && key.Scancode == 0x0E && !highlighted;
        bool isoEnter = key.Scancode == 0x1C && key.H > VirtualKeyboard.KEY_H;
        KeyHighlight kind = HighlightOf(key, state);
        var (highlightBorder, highlightFill) = tutorial ? HighlightColors(kind) : (CLR_KEY_HIGHLIGHT_BORDER, CLR_MOD_ACTIVE);

        uint fill = key.IsContextual ? CLR_KEY_CONTEXT : CLR_KEY;
        uint border = CLR_KEY_BORDER;
        int borderWidth = 1;
        if (disabledBackspace)
        {
            fill = CLR_KEY_DISABLED;
        }
        else if (pressed)
        {
            fill = CLR_KEY_PRESSED;
            // Le tutoriel montre la frappe seule ; les Leçons gardent le contour de l'indice.
            if (highlighted && !tutorial)
            {
                border = highlightBorder;
                borderWidth = 2;
            }
        }
        else if (highlighted)
        {
            border = highlightBorder;
            borderWidth = 2;
            // Modificateur à presser et déjà pressé : le tutoriel le remplit de la couleur du genre.
            if (modifierActive)
                fill = highlightFill;
        }
        else if (modifierActive)
        {
            fill = CLR_MOD_ACTIVE;
        }

        var brush = Win32.CreateSolidBrush(fill);
        var pen = Win32.CreatePen(0, borderWidth, border);
        var oldBrush = Win32.SelectObject(hdc, brush);
        var oldPen = Win32.SelectObject(hdc, pen);

        if (isoEnter)
            DrawIsoEnter(hdc, rect, key);
        else
            DrawRectKey(hdc, rect, brush);

        Win32.SelectObject(hdc, oldPen);
        Win32.SelectObject(hdc, oldBrush);
        Win32.DeleteObject(pen);
        Win32.DeleteObject(brush);

        if (key.Label == "Verr. Maj." && state.CapsLock)
        {
            int barH = tutorial
                ? Math.Max(2, (int)(placement.Scale * 0.08f))
                : Math.Max(2, (rect.bottom - rect.top) / 18);
            var barRect = new Win32.RECT { left = rect.left, top = rect.bottom - barH, right = rect.right, bottom = rect.bottom };
            var barBrush = Win32.CreateSolidBrush(CLR_CAPS_BAR);
            Win32.FillRect(hdc, ref barRect, barBrush);
            Win32.DeleteObject(barBrush);
        }

        if (key.IsContextual || key.Scancode == 0 || !layout.Keys.TryGetValue(key.Scancode, out var def))
            DrawContextKeyLabel(hdc, rect, key, isoEnter, disabledBackspace, tutorial, fonts.Context);
        else
            DrawKeyCharacters(hdc, rect, key, FilterKeyForProfile(def, key.Scancode, profile, state), layout, profile, state,
                placement.Scale, fonts);

        if (tutorial && highlighted && kind != KeyHighlight.Direct)
            DrawBadge(hdc, rect, kind, state.UiScale, fonts.Badge);
    }

    /// <summary>Pastille « 1 » ou « 2 » en haut à droite d'une touche du tutoriel : l'ordre
    /// d'une séquence de touche morte.</summary>
    private static void DrawBadge(IntPtr hdc, Win32.RECT key, KeyHighlight kind, float uiScale, IntPtr hFont)
    {
        int S(int value) => (int)(value * uiScale);
        int x = key.right - S(12);
        int y = key.top + S(1);
        int size = S(14);
        var rect = new Win32.RECT { left = x, top = y, right = x + size, bottom = y + size };
        var brush = Win32.CreateSolidBrush(HighlightColors(kind).Border);
        Win32.FillRect(hdc, ref rect, brush);
        Win32.DeleteObject(brush);

        string text = kind == KeyHighlight.Step1 ? "1" : "2";
        var oldFont = Win32.SelectObject(hdc, hFont);
        Win32.SetTextColor(hdc, DarkTheme.OnAccent);
        Win32.DrawTextW(hdc, text, text.Length, ref rect, Win32.DT_CENTER | Win32.DT_VCENTER | Win32.DT_SINGLELINE);
        Win32.SelectObject(hdc, oldFont);
    }

    private static void DrawRectKey(IntPtr hdc, Win32.RECT rect, IntPtr brush)
    {
        Win32.FillRect(hdc, ref rect, brush);
        Win32.MoveToEx(hdc, rect.left, rect.top, IntPtr.Zero);
        Win32.LineTo(hdc, rect.right, rect.top);
        Win32.LineTo(hdc, rect.right, rect.bottom);
        Win32.LineTo(hdc, rect.left, rect.bottom);
        Win32.LineTo(hdc, rect.left, rect.top);
    }

    private static void DrawIsoEnter(IntPtr hdc, Win32.RECT rect, VirtualKeyboard.VisualKey key)
    {
        int width = rect.right - rect.left;
        int height = rect.bottom - rect.top;
        int stepY = rect.top + (int)(height * (VirtualKeyboard.KEY_H / key.H));
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
        Win32.Polygon(hdc, pts, pts.Length);
    }

    private static void DrawContextKeyLabel(
        IntPtr hdc,
        Win32.RECT rect,
        VirtualKeyboard.VisualKey key,
        bool isoEnter,
        bool disabled,
        bool tutorial,
        IntPtr hFont)
    {
        var labelRect = rect;
        if (isoEnter)
            labelRect.left = rect.right - (int)((rect.right - rect.left) * (1.25f / key.W));

        Win32.SelectObject(hdc, hFont);
        Win32.SetTextColor(hdc, disabled ? DarkTheme.TextDisabled : CLR_CTX_TEXT);
        // Le tutoriel ne raccourcit pas ses libellés.
        uint flags = Win32.DT_CENTER | Win32.DT_VCENTER | Win32.DT_SINGLELINE | Win32.DT_NOPREFIX
            | (tutorial ? 0u : Win32.DT_END_ELLIPSIS);
        Win32.DrawTextW(hdc, L.Keyboard_KeyCap(key.Label), -1, ref labelRect, flags);
    }

    private static void DrawKeyCharacters(
        IntPtr hdc,
        Win32.RECT rect,
        VirtualKeyboard.VisualKey key,
        KeyDefinition keyDef,
        Layout layout,
        KeyboardRenderProfile profile,
        KeyboardRenderState state,
        float scale,
        KeyboardFonts fonts)
    {
        bool tutorial = profile == KeyboardRenderProfile.Onboarding;
        int kx = rect.left;
        int ky = rect.top;
        int kw = rect.right - rect.left;
        int kh = rect.bottom - rect.top;
        int pad = tutorial ? Math.Max(4, (int)(scale * 0.14f)) : Math.Clamp(kw / 10, 4, 14);
        var style = new GlyphStyle(state.ShowInvisibleMarkers, tutorial, layout, fonts.OverlayMain, fonts.OverlaySmall);

        // Sous Ctrl ou Alt, les Leçons taisent les couches (raccourcis) ; le tutoriel les garde.
        if (!tutorial && ((state.Ctrl && !state.AltGr) || (state.Alt && !state.AltGr)))
            return;

        if (state.ActiveDeadKey != null)
        {
            DrawActiveDeadKeyCharacter(hdc, rect, key, keyDef, layout, state, fonts.DeadKey, fonts.Tiny);
            return;
        }

        if (AccentedNumericScancodes.Contains(key.Scancode))
            PaintAccentedNumericKey(hdc, kx, ky, kw, kh, keyDef, pad, state, style, fonts.Main, fonts.Small);
        else if (LetterKeyScancodes.Contains(key.Scancode) && IsLetterChar(keyDef.Base))
            PaintLetterKey(hdc, kx, ky, kw, kh, keyDef, pad, state, style, fonts.Main, fonts.Small);
        else
            PaintSymbolKey(hdc, kx, ky, kw, kh, keyDef, pad, state, style, fonts.Main, fonts.Small, fonts.Tiny);
    }

    private static void DrawActiveDeadKeyCharacter(
        IntPtr hdc,
        Win32.RECT rect,
        VirtualKeyboard.VisualKey key,
        KeyDefinition keyDef,
        Layout layout,
        KeyboardRenderState state,
        IntPtr hFontMain,
        IntPtr hFontTiny)
    {
        int h = rect.bottom - rect.top;
        int labelH = Math.Clamp(h / 4, 10, 16);
        var charRect = new Win32.RECT { left = rect.left, top = rect.top, right = rect.right, bottom = rect.bottom - labelH - 2 };
        string? output = GetActiveDeadKeyOutput(keyDef, layout, state);
        string? display = FormatActiveDeadKeyDisplay(output, state.ShowInvisibleMarkers);

        if (!string.IsNullOrEmpty(display))
        {
            var oldFont = Win32.SelectObject(hdc, hFontMain);
            Win32.SetTextColor(hdc, CLR_CTX_TEXT);
            Win32.DrawTextW(hdc, display, display.Length, ref charRect,
                Win32.DT_CENTER | Win32.DT_VCENTER | Win32.DT_SINGLELINE | Win32.DT_NOPREFIX | Win32.DT_NOCLIP | Win32.DT_END_ELLIPSIS);
            Win32.SelectObject(hdc, oldFont);
        }

        var labelRect = new Win32.RECT { left = rect.left + 2, top = rect.bottom - labelH - 2, right = rect.right - 2, bottom = rect.bottom - 1 };
        var oldLabelFont = Win32.SelectObject(hdc, hFontTiny);
        Win32.SetTextColor(hdc, CLR_KEY_LABEL);
        string keyCap = L.Keyboard_KeyCap(key.Label);
        Win32.DrawTextW(hdc, keyCap, keyCap.Length, ref labelRect,
            Win32.DT_CENTER | Win32.DT_VCENTER | Win32.DT_SINGLELINE | Win32.DT_NOPREFIX | Win32.DT_END_ELLIPSIS);
        Win32.SelectObject(hdc, oldLabelFont);
    }

    private static string? GetActiveDeadKeyOutput(KeyDefinition keyDef, Layout layout, KeyboardRenderState state)
    {
        if (state.ActiveDeadKey == null)
            return null;

        string? output = keyDef.GetOutput(state.Shift, state.AltGr, state.CapsLock);
        if (output == null)
            return null;

        if (output.StartsWith("dk_", StringComparison.Ordinal))
        {
            if (output == state.ActiveDeadKey && layout.DeadKeys.TryGetValue(state.ActiveDeadKey, out var selfDk))
            {
                var isolated = selfDk.GetIsolated();
                if (isolated != null)
                    return selfDk.Apply(isolated) ?? isolated;
            }
            return null;
        }

        return layout.DeadKeys.TryGetValue(state.ActiveDeadKey, out var dk)
            ? dk.Apply(output)
            : null;
    }

    private static string? FormatActiveDeadKeyDisplay(string? value, bool showInvisibleMarkers)
    {
        if (string.IsNullOrEmpty(value))
            return null;
        string display = DisplayInvisible(value, showInvisibleMarkers);
        return display.Length == 1 && IsCombiningMark(display[0]) ? "◌" + display : display;
    }

    private static void DrawActiveDeadKeyStatus(IntPtr hdc, Win32.RECT rect, string activeDeadKey, IntPtr hFont)
    {
        string name = VirtualKeyboard.GetDeadKeyDisplayName(activeDeadKey);
        string text = L.Keyboard_ActiveDeadKeyStatus(name);
        var textRect = new Win32.RECT { left = rect.left, top = rect.top, right = rect.right - 4, bottom = rect.bottom };
        var oldFont = Win32.SelectObject(hdc, hFont);
        Win32.SetTextColor(hdc, CLR_DK_ACTIVE_TEXT);
        Win32.DrawTextW(hdc, text, text.Length, ref textRect,
            Win32.DT_RIGHT | Win32.DT_VCENTER | Win32.DT_SINGLELINE | Win32.DT_NOPREFIX | Win32.DT_END_ELLIPSIS);
        Win32.SelectObject(hdc, oldFont);
    }

    private static void PaintLetterKey(
        IntPtr hdc,
        int kx,
        int ky,
        int kw,
        int kh,
        KeyDefinition keyDef,
        int pad,
        KeyboardRenderState state,
        GlyphStyle style,
        IntPtr hFontMain,
        IntPtr hFontSmall)
    {
        string? mainChar;
        if (state.CapsLock && state.Shift) mainChar = keyDef.CapsShift ?? keyDef.Base;
        else if (state.CapsLock) mainChar = keyDef.Caps ?? keyDef.Base?.ToUpperInvariant();
        else if (state.Shift) mainChar = keyDef.Shift ?? keyDef.Base?.ToUpperInvariant();
        else mainChar = keyDef.Base;

        string? altGrRaw = keyDef.AltGr;
        string? altGrCharToShow = altGrRaw;
        bool altGrIsLetter = IsLetterChar(altGrRaw);
        if (altGrIsLetter)
        {
            if (state.CapsLock && state.Shift) altGrCharToShow = keyDef.CapsShiftAltGr ?? altGrRaw;
            else if (state.CapsLock) altGrCharToShow = keyDef.CapsAltGr ?? keyDef.ShiftAltGr ?? altGrRaw?.ToUpperInvariant();
            else if (state.Shift) altGrCharToShow = keyDef.ShiftAltGr ?? altGrRaw?.ToUpperInvariant();
        }
        bool hasAltGrChar = !string.IsNullOrEmpty(altGrCharToShow) && altGrCharToShow != mainChar;

        string? shiftAltGrChar = keyDef.ShiftAltGr;
        bool showShiftAltGr = !string.IsNullOrEmpty(shiftAltGrChar)
            && !IsLetterChar(shiftAltGrChar)
            && shiftAltGrChar != altGrRaw;

        bool topLeftActive = !state.AltGr;
        bool bottomRightActive = state.AltGr && (!state.Shift || (state.Shift && altGrIsLetter && !showShiftAltGr));
        bool topRightActive = state.AltGr && state.Shift && showShiftAltGr;

        DrawCharAt(hdc, kx + pad, ky, kx + kw / 2 + pad, ky + kh - pad,
            mainChar, topLeftActive, IsDeadKeyRef(mainChar), alignLeft: true, useMainFont: true, style, hFontMain, hFontSmall, alignTop: true);

        if (hasAltGrChar)
            DrawCharAt(hdc, kx + kw / 2, ky + kh / 2, kx + kw - pad, ky + kh - pad,
                altGrCharToShow, bottomRightActive, IsDeadKeyRef(altGrRaw), alignLeft: false, useMainFont: false, style, hFontMain, hFontSmall);

        if (showShiftAltGr)
            DrawCharAt(hdc, kx + kw / 2, ky, kx + kw - pad, ky + kh / 2 + pad,
                shiftAltGrChar, topRightActive, IsDeadKeyRef(shiftAltGrChar), alignLeft: false, useMainFont: false, style, hFontMain, hFontSmall, alignTop: true);
    }

    private static void PaintAccentedNumericKey(
        IntPtr hdc,
        int kx,
        int ky,
        int kw,
        int kh,
        KeyDefinition keyDef,
        int pad,
        KeyboardRenderState state,
        GlyphStyle style,
        IntPtr hFontMain,
        IntPtr hFontSmall)
    {
        string? letter = state.CapsLock ? (keyDef.Caps ?? keyDef.Base?.ToUpperInvariant()) : keyDef.Base;
        string? digit = keyDef.Shift;
        string? altGr1 = keyDef.AltGr;
        string? altGr2 = keyDef.ShiftAltGr;

        DrawCharAt(hdc, kx + pad, ky + kh / 2, kx + kw / 2, ky + kh - pad,
            letter, !state.AltGr && !state.Shift, IsDeadKeyRef(keyDef.Base), true, false, style, hFontMain, hFontSmall);
        DrawCharAt(hdc, kx + kw / 2, ky + kh / 2, kx + kw - pad, ky + kh - pad,
            altGr1, state.AltGr && !state.Shift, IsDeadKeyRef(altGr1), false, false, style, hFontMain, hFontSmall);
        DrawCharAt(hdc, kx + pad, ky, kx + kw / 2, ky + kh / 2 + pad,
            digit, !state.AltGr && state.Shift, IsDeadKeyRef(digit), true, false, style, hFontMain, hFontSmall, alignTop: true);
        DrawCharAt(hdc, kx + kw / 2, ky, kx + kw - pad, ky + kh / 2 + pad,
            altGr2, state.AltGr && state.Shift, IsDeadKeyRef(altGr2), false, false, style, hFontMain, hFontSmall, alignTop: true);
    }

    private static void PaintSymbolKey(
        IntPtr hdc,
        int kx,
        int ky,
        int kw,
        int kh,
        KeyDefinition keyDef,
        int pad,
        KeyboardRenderState state,
        GlyphStyle style,
        IntPtr hFontMain,
        IntPtr hFontSmall,
        IntPtr hFontTiny)
    {
        if (keyDef.Scancode == 0x39)
        {
            PaintSpaceKey(hdc, kx, ky, kw, kh, keyDef, pad, state, style, hFontMain, hFontTiny);
            return;
        }

        DrawCharAt(hdc, kx + pad, ky + kh / 2, kx + kw / 2, ky + kh - pad,
            keyDef.Base, !state.AltGr && !state.Shift, IsDeadKeyRef(keyDef.Base), true, false, style, hFontMain, hFontSmall);
        DrawCharAt(hdc, kx + kw / 2, ky + kh / 2, kx + kw - pad, ky + kh - pad,
            keyDef.AltGr, state.AltGr && !state.Shift, IsDeadKeyRef(keyDef.AltGr), false, false, style, hFontMain, hFontSmall);
        DrawCharAt(hdc, kx + pad, ky, kx + kw / 2, ky + kh / 2 + pad,
            keyDef.Shift, !state.AltGr && state.Shift, IsDeadKeyRef(keyDef.Shift), true, false, style, hFontMain, hFontSmall, alignTop: true);
        DrawCharAt(hdc, kx + kw / 2, ky, kx + kw - pad, ky + kh / 2 + pad,
            keyDef.ShiftAltGr, state.AltGr && state.Shift, IsDeadKeyRef(keyDef.ShiftAltGr), false, false, style, hFontMain, hFontSmall, alignTop: true);
    }

    private static void PaintSpaceKey(
        IntPtr hdc,
        int kx,
        int ky,
        int kw,
        int kh,
        KeyDefinition keyDef,
        int pad,
        KeyboardRenderState state,
        GlyphStyle style,
        IntPtr hFontMain,
        IntPtr hFontTiny)
    {
        int right = kx + kw - pad;
        int left = Math.Max(kx + kw / 2, right - Math.Max(150, kw / 3));
        int bottom = ky + kh - Math.Max(8, pad);
        int lineH = Math.Max(14, Math.Min(20, kh / 3));
        int gap = Math.Max(1, kh / 28);

        if (!string.IsNullOrEmpty(keyDef.ShiftAltGr))
        {
            DrawCharAt(hdc, left, bottom - (lineH * 2) - gap, right, bottom - lineH - gap,
                keyDef.ShiftAltGr, state.AltGr && state.Shift, IsDeadKeyRef(keyDef.ShiftAltGr), false, false, style, hFontMain, hFontTiny);
        }

        if (!string.IsNullOrEmpty(keyDef.AltGr))
        {
            DrawCharAt(hdc, left, bottom - lineH, right, bottom,
                keyDef.AltGr, state.AltGr && !state.Shift, IsDeadKeyRef(keyDef.AltGr), false, false, style, hFontMain, hFontTiny);
        }
    }

    private static void DrawCharAt(
        IntPtr hdc,
        int left,
        int top,
        int right,
        int bottom,
        string? chr,
        bool isActive,
        bool isDeadKey,
        bool alignLeft,
        bool useMainFont,
        GlyphStyle style,
        IntPtr hFontMain,
        IntPtr hFontSmall,
        bool alignTop = false)
    {
        var disp = GetDisplayChar(chr, style);
        if (string.IsNullOrEmpty(disp)) return;

        uint color = (isDeadKey, isActive) switch
        {
            (true, true) => CLR_DK_CHAR,
            // Le tutoriel éteint une touche morte hors de la couche tenue, comme un caractère.
            (true, false) => style.Tutorial ? CLR_CHAR_DIM : CLR_DK_CHAR,
            (false, true) => CLR_CHAR_ACTIVE_BLUE,
            (false, false) => CLR_CHAR_DIM
        };
        IntPtr hFont = useMainFont ? hFontMain : hFontSmall;
        IntPtr overlay = useMainFont ? style.OverlayMain : style.OverlaySmall;
        var r = new Win32.RECT { left = left, top = top, right = right, bottom = bottom };
        uint vAlign = alignTop ? 0u : Win32.DT_VCENTER;
        uint flags = vAlign | Win32.DT_SINGLELINE | Win32.DT_NOPREFIX | Win32.DT_NOCLIP | (alignLeft ? Win32.DT_LEFT : Win32.DT_RIGHT);
        Win32.SetTextColor(hdc, color);
        var oldFont = Win32.SelectObject(hdc, overlay != IntPtr.Zero && IsOverlaidDottedCircle(disp) ? overlay : hFont);
        // Tutoriel : « ◌ » puis l'accent, dessinés l'un sur l'autre (◌ barré de l'exercice 6).
        // Ailleurs, ou sans ces polices, le couple s'écrit côte à côte.
        if (overlay != IntPtr.Zero && IsOverlaidDottedCircle(disp))
        {
            var mark = r;
            Win32.DrawTextW(hdc, "◌", 1, ref r, flags);
            Win32.DrawTextW(hdc, disp[1..], 1, ref mark, flags);
        }
        else
        {
            Win32.DrawTextW(hdc, disp, disp.Length, ref r, flags);
        }
        Win32.SelectObject(hdc, oldFont);
    }

    /// <summary>« ◌ » suivi d'un accent qui a son propre glyphe (/ ˙ ˝ ˘ − ˇ ˛) : le couple
    /// que le tutoriel superpose. Un accent combinant reste collé au cercle.</summary>
    internal static bool IsOverlaidDottedCircle(string display)
        => display.Length == 2 && display[0] == '◌' && !IsCombiningMark(display[1]);

    private static KeyDefinition FilterKeyForProfile(
        KeyDefinition key,
        uint scancode,
        KeyboardRenderProfile profile,
        KeyboardRenderState state)
    {
        if (profile == KeyboardRenderProfile.Full) return key;

        return new KeyDefinition
        {
            Position = key.Position,
            Scancode = key.Scancode,
            Base = FilterSlot(profile, scancode, 0, key.Base, state),
            Shift = FilterSlot(profile, scancode, 1, key.Shift, state),
            AltGr = FilterSlot(profile, scancode, 2, key.AltGr, state),
            ShiftAltGr = FilterSlot(profile, scancode, 3, key.ShiftAltGr, state),
            Caps = FilterSlot(profile, scancode, 4, key.Caps, state),
            CapsShift = FilterSlot(profile, scancode, 5, key.CapsShift, state),
            CapsAltGr = FilterSlot(profile, scancode, 6, key.CapsAltGr, state),
            CapsShiftAltGr = FilterSlot(profile, scancode, 7, key.CapsShiftAltGr, state),
        };
    }

    private static string? FilterSlot(
        KeyboardRenderProfile profile,
        uint scancode,
        int layer,
        string? value,
        KeyboardRenderState state)
    {
        return IsSlotVisible(profile, scancode, layer, value, state.LessonVisibleCharacters, state.HintCharacter)
            ? value
            : null;
    }

    private static bool IsOnboardingSlotVisible(uint scancode, int layer, string value)
    {
        if (value.StartsWith("dk_", StringComparison.Ordinal) && HiddenDeadKeysInOnboarding.Contains(value))
            return false;
        if (HiddenSlotsInOnboarding.Contains((scancode, layer)))
            return false;
        return true;
    }

    private static string? GetDisplayChar(string? value, GlyphStyle style)
    {
        if (string.IsNullOrEmpty(value)) return null;

        string result;
        bool isDk = value.StartsWith("dk_", StringComparison.Ordinal);
        if (isDk)
        {
            // Le tutoriel lit d'abord l'accent isolé de la disposition (sa table « espace ») ;
            // les deux sources s'accordent pour toutes les touches mortes qu'il affiche.
            string? isolated = style.Tutorial && style.Layout.DeadKeys.TryGetValue(value, out var dk) ? dk.GetIsolated() : null;
            result = !string.IsNullOrWhiteSpace(isolated) ? isolated : TrayApplication.GetDeadKeySymbol(value);
        }
        else
        {
            result = DisplayInvisible(value, style.ShowInvisibleMarkers);
        }

        if (result.Length == 1 && IsCombiningMark(result[0]))
            return "◌" + result;
        if (isDk && DkAlwaysWithDottedCircle.Contains(value))
            return "◌" + result;

        return result;
    }

    internal static string DisplayInvisible(string value)
    {
        return DisplayInvisible(value, showMarkers: true);
    }

    private static string DisplayInvisible(string value, bool showMarkers)
    {
        if (!showMarkers)
            return value;

        return value switch
        {
            "\u202F" => L.Keyboard_NarrowNbsp,
            "\u00A0" => L.Keyboard_Nbsp,
            "\u2009" => L.Keyboard_ThinSpace,
            "\u200A" => L.Keyboard_HairSpace,
            "\u2002" => L.Keyboard_EnSpace,
            "\u2003" => L.Keyboard_EmSpace,
            "\u2007" => L.Keyboard_FigureSpace,
            "\u200B" => L.Keyboard_ZeroWidthSpace,
            _ => value
        };
    }

    private static bool IsLetterChar(string? s) => s != null && s.Length == 1 && char.IsLetter(s[0]);

    /// <summary>Touche d'une lettre ordinaire (rangées A à N), dont Verr. Maj. fait la
    /// majuscule.</summary>
    internal static bool IsLetterKey(uint scancode) => LetterKeyScancodes.Contains(scancode);

    private static bool IsDeadKeyRef(string? s) => s != null && s.StartsWith("dk_", StringComparison.Ordinal);

    internal static bool IsCombiningMark(char c) =>
        (c >= '\u0300' && c <= '\u036F') ||
        (c >= '\u1AB0' && c <= '\u1AFF') ||
        (c >= '\u1DC0' && c <= '\u1DFF') ||
        (c >= '\u20D0' && c <= '\u20FF') ||
        (c >= '\uFE20' && c <= '\uFE2F');

    /// <summary>Cadre d'une touche : les bords tombent sur la grille des unités, moins un
    /// pixel à droite et en bas (l'espace entre deux touches).</summary>
    private static Win32.RECT ToRect(VirtualKeyboard.VisualKey key, KeyboardPlacement placement)
        => ToRect(key, placement.OriginX, placement.OriginY, placement.Scale);

    private static Win32.RECT ToRect(VirtualKeyboard.VisualKey key, int originX, int originY, float scale)
    {
        return new Win32.RECT
        {
            left = originX + (int)(key.X * scale),
            top = originY + (int)(key.Y * scale),
            right = originX + (int)((key.X + key.W) * scale) - 1,
            bottom = originY + (int)((key.Y + key.H) * scale) - 1
        };
    }
}
