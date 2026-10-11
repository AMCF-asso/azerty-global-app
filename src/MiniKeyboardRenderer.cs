// Le mini-clavier des Leçons : les deux notations « sans le grand clavier » décidées le 11/10
// (maquettes lecons-notation2-4-mini et lecons-notation2-5-vignettes). Dessinateur GDI sans état,
// appelé par la fenêtre Leçons ; le calcul (quelles touches, quel état, quels numéros, quels
// rectangles) est séparé du dessin pour se tester sans fenêtre.
namespace AZERTYGlobal;

/// <summary>Ce que montre une touche du mini-clavier : éteinte, tenue (Maj, AltGr) ou frappée.</summary>
internal enum MiniKeyState { Off, Held, Struck }

/// <summary>
/// Une touche du mini-clavier, prête à dessiner : son cadre (bords inclus, comme
/// <see cref="PaperKeyboard.ToRect"/>), son état, sa gravure (vide pour une touche de fonction
/// éteinte et pour la barre d'espace) et les numéros des temps où elle sert.
/// </summary>
internal readonly record struct MiniKey(VirtualKeyboard.VisualKey Key, Win32.RECT Rect, MiniKeyState State,
    string Label, IReadOnlyList<int> Numbers)
{
    public bool IsFunction => Key.IsContextual;
}

internal static class MiniKeyboardRenderer
{
    private const uint ScanCapsLock = 0x3A;
    private const uint ScanEnter = 0x1C;

    // ── Mesures (toutes proportionnelles à l'unité, déjà en pixels) ────

    /// <summary>Marge de la carte autour des touches : de quoi loger les pastilles qui
    /// débordent des coins.</summary>
    internal static int Pad(float unit) => Math.Max(3, (int)Math.Round(unit * 0.5f, MidpointRounding.AwayFromZero));

    /// <summary>Taille d'un mini-clavier (carte et marge comprises) pour une unité de touche donnée
    /// (pixels déjà mis à l'échelle). La géométrie est celle de <see cref="PaperKeyboard.Size"/>,
    /// donc celle du grand clavier : 16,3 unités sur 5,4.</summary>
    internal static (int Width, int Height) Size(float unit)
    {
        var (w, h) = PaperKeyboard.Size(unit);
        int pad = Pad(unit);
        return (w + 2 * pad, h + 2 * pad);
    }

    // ── Calcul : les touches, leurs états, leurs numéros ───────────────

    /// <summary>Une touche allumée : état, gravure et numéros des temps (ordre croissant).</summary>
    private sealed class Lit
    {
        public MiniKeyState State;
        public string? Label;
        public readonly List<int> Numbers = new(2);
    }

    /// <summary>
    /// Toutes les touches du clavier, dont le cadre part de (<paramref name="left"/>, <paramref name="top"/>)
    /// pour la carte entière. Les touches de <paramref name="steps"/> sont allumées : Maj et AltGr
    /// tenues, la touche du temps frappée. <paramref name="onlyStep"/> ne garde que ce temps (une
    /// vignette). Une touche servant à deux temps garde le premier état et porte les deux numéros.
    /// </summary>
    internal static IReadOnlyList<MiniKey> Plan(int left, int top, float unit, IReadOnlyList<GestureStep> steps,
        int? onlyStep = null)
    {
        var visual = KeyboardRenderer.VisualKeys;
        var lit = Light(steps, onlyStep);
        int pad = Pad(unit);
        var placement = PaperKeyboard.PlaceAt(left + pad, top + pad, unit);
        var keys = new List<MiniKey>(visual.Count);
        for (int i = 0; i < visual.Count; i++)
        {
            var key = visual[i];
            var rect = PaperKeyboard.ToRect(key, placement);
            if (lit.TryGetValue(i, out var on))
                keys.Add(new MiniKey(key, rect, on.State, LitLabel(key, on), on.Numbers));
            else
                keys.Add(new MiniKey(key, rect, MiniKeyState.Off, EngravedLabel(key), Array.Empty<int>()));
        }
        return keys;
    }

    /// <summary>Les touches allumées d'un geste, par position dans <see cref="KeyboardRenderer.VisualKeys"/>.
    /// Mêmes correspondances que <see cref="VirtualKeyboard.AddGesture"/> : Verr. Maj. est la touche
    /// 0x3A, Maj la touche de gauche, AltGr se reconnaît à son nom.</summary>
    private static Dictionary<int, Lit> Light(IReadOnlyList<GestureStep> steps, int? onlyStep)
    {
        var lit = new Dictionary<int, Lit>();
        void Add(int index, MiniKeyState state, int number, string? label)
        {
            if (index < 0) return;
            if (!lit.TryGetValue(index, out var entry))
                lit[index] = entry = new Lit { State = state, Label = label };
            entry.Numbers.Add(number);
        }

        for (int i = 0; i < steps.Count; i++)
        {
            if (onlyStep is { } only && i != only) continue;
            var step = steps[i];
            if (step.Shift) Add(IndexOf(k => k.ContextId == VirtualKeyboard.ContextShiftLeft), MiniKeyState.Held, i + 1, null);
            if (step.AltGr) Add(IndexOf(k => k.Label == "AltGr"), MiniKeyState.Held, i + 1, null);
            Add(KeyIndex(step.KeyCode), MiniKeyState.Struck, i + 1, step.ShortLabel);
        }
        return lit;
    }

    private static int IndexOf(Func<VirtualKeyboard.VisualKey, bool> match)
    {
        var visual = KeyboardRenderer.VisualKeys;
        for (int i = 0; i < visual.Count; i++)
            if (match(visual[i])) return i;
        return -1;
    }

    private static int KeyIndex(string keyCode)
    {
        uint scancode = keyCode == Gesture.CapsLockCode ? ScanCapsLock
            : VirtualKeyboard.KeyCodeToScancode.TryGetValue(keyCode, out var found) ? found : 0u;
        return scancode == 0 ? -1 : IndexOf(k => k.Scancode == scancode);
    }

    /// <summary>Scancode → code de touche, pour retrouver la gravure d'une touche éteinte. Espace n'y est pas :
    /// la barre ne porte pas de nom.</summary>
    private static readonly Dictionary<uint, string> CodeOfScancode = BuildCodeOfScancode();

    private static Dictionary<uint, string> BuildCodeOfScancode()
    {
        var map = new Dictionary<uint, string>();
        foreach (var (code, scancode) in VirtualKeyboard.KeyCodeToScancode)
            if (code != "Space") map.TryAdd(scancode, code);
        return map;
    }

    /// <summary>Gravure d'une touche éteinte : celle du clavier AZERTY (« é » sur le 2, « ù », « W »),
    /// la même que la gravure d'une touche frappée (<see cref="Gesture.KeyLabel"/>). Les touches de
    /// fonction éteintes et l'espace n'ont pas de nom.</summary>
    private static string EngravedLabel(VirtualKeyboard.VisualKey key)
        => key.IsContextual || key.Scancode == 0x39 || !CodeOfScancode.TryGetValue(key.Scancode, out var code)
            ? ""
            : Gesture.KeyLabel(code);

    /// <summary>Nom d'une touche allumée : court pour une touche de fonction (« VM » / « Caps »,
    /// « Maj » / « Shift », « AltGr »), la gravure du temps pour les autres (rien sur l'espace).</summary>
    private static string LitLabel(VirtualKeyboard.VisualKey key, Lit on)
    {
        if (key.Scancode == ScanCapsLock) return L.Gesture_CapsLockShort;
        if (key.ContextId == VirtualKeyboard.ContextShiftLeft || key.ContextId == VirtualKeyboard.ContextShiftRight)
            return L.Gesture_LineShift;
        if (key.Label == "AltGr") return L.Gesture_LineAltGr;
        if (key.IsContextual || key.Scancode == 0x39) return "";
        return on.Label ?? EngravedLabel(key);
    }

    // ── Pastille d'ordre (notation « sous le caractère ») ──────────────

    private static int BadgeSize(float unit) => Math.Max(10, (int)Math.Round(unit * 0.8f));
    private static int BadgeRing(float unit) => Math.Max(1, (int)Math.Round(unit * 0.1f));

    /// <summary>Le disque de la pastille n° <paramref name="index"/> (0 pour la première) d'une touche :
    /// il déborde de son coin haut droit, sans quitter la carte ; la seconde pastille d'une touche
    /// frappée à deux temps se pose à sa gauche. Bornes comme <c>Ellipse</c> (droite et bas exclus).</summary>
    internal static Win32.RECT BadgeRect(Win32.RECT keyRect, float unit, int index)
    {
        int d = BadgeSize(unit), ring = BadgeRing(unit);
        int inset = Math.Max(0, Pad(unit) - ring - 1);
        int right = keyRect.right + 1 + inset - index * (d + 1);
        int top = keyRect.top - inset;
        return new Win32.RECT { left = right - d, top = top, right = right, bottom = top + d };
    }

    // ── Dessin d'un clavier ────────────────────────────────────────────

    /// <summary>Teinte d'une touche tenue : l'action à 15 % sur la surface (--teinte des maquettes).</summary>
    private static uint HeldFill => PaperDraw.Blend(PaperTheme.Action, PaperTheme.Surface, 0.15f);

    private static FontSpec MonoFont(float unit, bool lit)
        => new(AppFonts.Mono, Math.Max(6, (int)Math.Round(unit * (lit ? 0.62f : 0.55f))), lit ? TypeRamp.Medium : TypeRamp.Regular);

    private static FontSpec NameFont(float unit, int shrink = 0)
        => new(AppFonts.Text, Math.Max(6, (int)Math.Round(unit * 0.58f) - shrink), TypeRamp.Semibold);

    /// <summary>La carte et ses touches ; les pastilles se posent ensuite, par-dessus les voisines.</summary>
    private static void DrawKeyboard(IntPtr hdc, int left, int top, float unit, float scale, IReadOnlyList<MiniKey> keys, bool badges)
    {
        var (w, h) = Size(unit);
        var card = new Win32.RECT { left = left, top = top, right = left + w - 1, bottom = top + h - 1 };
        PaperDraw.RoundBox(hdc, card, PaperTheme.Surface, PaperTheme.Border, Math.Max(3, (int)Math.Round(unit * 0.25f)));
        foreach (var key in keys)
            DrawKey(hdc, key, unit, scale, badges);
    }

    private static void DrawKey(IntPtr hdc, MiniKey k, float unit, float scale, bool badges)
    {
        uint fill = k.IsFunction ? PaperTheme.FunctionKey : PaperTheme.Surface;
        uint border = PaperTheme.KeyFadedBorder;
        int borderWidth = 1;
        uint ink = PaperTheme.Pale;
        bool lit = k.State != MiniKeyState.Off;
        if (k.State == MiniKeyState.Struck)
        {
            fill = border = PaperTheme.Action;
            ink = PaperTheme.OnAction;
        }
        else if (k.State == MiniKeyState.Held)
        {
            fill = HeldFill;
            border = ink = PaperTheme.Action;
            borderWidth = Math.Max(2, (int)Math.Round(2 * scale));
        }

        int radius = Math.Max(1, (int)Math.Round(unit * 0.13f));
        if (k.Key.Scancode == ScanEnter && k.Key.H > VirtualKeyboard.KEY_H)
            DrawIsoEnter(hdc, k.Rect, k.Key, fill, border, borderWidth);
        else
            PaperDraw.RoundBox(hdc, k.Rect, fill, border, radius, borderWidth);

        if (k.Label.Length == 0) return;
        if (k.IsFunction)
        {
            // Le nom d'une touche de fonction allumée tient dans la touche, au besoin en réduisant la police.
            int width = k.Rect.right - k.Rect.left;
            var font = NameFont(unit);
            for (int shrink = 1; shrink <= 2 && PaperText.Width(hdc, k.Label, font) > width - 2; shrink++)
                font = NameFont(unit, shrink);
            // La pastille couvre le coin haut droit : le nom descend d'autant sous elle.
            var nameRect = badges ? k.Rect with { top = k.Rect.top + (int)((k.Rect.bottom - k.Rect.top) * 0.3f) } : k.Rect;
            PaperText.DrawLine(hdc, k.Label, font, nameRect, ink, TextAlign.Center);
            return;
        }

        // Gravure en haut à gauche, comme la lettre des touches du grand clavier.
        var mono = MonoFont(unit, lit);
        var (ascent, descent) = PaperText.Metrics(hdc, mono);
        int lineTop = k.Rect.top + Math.Max(1, (int)Math.Round(unit * 0.08f));
        int baseline = lineTop + (mono.Height - (ascent + descent)) / 2 + ascent;
        PaperText.Draw(hdc, k.Label, mono, k.Rect.left + Math.Max(1, (int)Math.Round(unit * 0.14f)), baseline, ink);
    }

    /// <summary>L'Entrée ISO tient sur deux rangées ; même contour que <see cref="PaperKeyboard"/> (cette
    /// routine y est privée).</summary>
    private static void DrawIsoEnter(IntPtr hdc, Win32.RECT rect, VirtualKeyboard.VisualKey key, uint fill, uint border, int borderWidth)
    {
        int width = rect.right - rect.left;
        int stepY = rect.top + (int)((rect.bottom - rect.top) * (VirtualKeyboard.KEY_H / key.H));
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

    /// <summary>Disque plein (<c>Ellipse</c>), contour de la même couleur.</summary>
    private static void Disc(IntPtr hdc, Win32.RECT r, uint color)
    {
        var brush = Win32.CreateSolidBrush(color);
        var pen = Win32.CreatePen(Win32.PS_SOLID, 1, color);
        var oldBrush = Win32.SelectObject(hdc, brush);
        var oldPen = Win32.SelectObject(hdc, pen);
        Win32.Ellipse(hdc, r.left, r.top, r.right, r.bottom);
        Win32.SelectObject(hdc, oldPen);
        Win32.SelectObject(hdc, oldBrush);
        Win32.DeleteObject(pen);
        Win32.DeleteObject(brush);
    }

    private static void DrawBadge(IntPtr hdc, Win32.RECT disc, int number, float unit)
    {
        int ring = BadgeRing(unit);
        // Le liseré de la couleur de la carte détache la pastille de la touche (box-shadow des maquettes).
        Disc(hdc, new Win32.RECT { left = disc.left - ring, top = disc.top - ring, right = disc.right + ring, bottom = disc.bottom + ring },
            PaperTheme.Surface);
        Disc(hdc, disc, PaperTheme.Ink);
        var font = new FontSpec(AppFonts.Text, Math.Max(7, (int)Math.Round((disc.right - disc.left) * 0.66f)), TypeRamp.Semibold);
        PaperText.DrawLine(hdc, number.ToString(System.Globalization.CultureInfo.InvariantCulture), font, disc, PaperTheme.Surface,
            TextAlign.Center);
    }

    // ── Notation « Mini-clavier sous le caractère » ────────────────────

    /// <summary>
    /// Un clavier, les touches de tous les temps allumées, une pastille numérotée par temps (une
    /// touche frappée à deux temps porte deux pastilles), puis la légende « frappée / tenue / 1,
    /// puis 2 » (« tout en même temps » pour un seul temps). Rend la hauteur dessinée ; la largeur
    /// est celle de <see cref="Size"/>, ou plus si la légende déborde (<see cref="MeasureNumbered"/>).
    /// </summary>
    internal static int DrawNumbered(IntPtr hdc, int left, int top, float unit, IReadOnlyList<GestureStep> steps, float scale)
    {
        if (steps.Count == 0) return 0;
        var keys = Plan(left, top, unit, steps);
        DrawKeyboard(hdc, left, top, unit, scale, keys, badges: true);
        foreach (var key in keys)
            for (int i = 0; i < key.Numbers.Count; i++)
                DrawBadge(hdc, BadgeRect(key.Rect, unit, i), key.Numbers[i], unit);
        int cardHeight = Size(unit).Height;
        int gap = Scaled(6, scale);
        var (_, legendHeight) = Legend(hdc, left, top + cardHeight + gap, scale, steps.Count, draw: true);
        return cardHeight + gap + legendHeight;
    }

    /// <summary>Taille de la notation numérotée : le clavier et sa légende.</summary>
    internal static (int Width, int Height) MeasureNumbered(IntPtr hdc, float unit, IReadOnlyList<GestureStep> steps, float scale)
    {
        if (steps.Count == 0) return (0, 0);
        var (cardWidth, cardHeight) = Size(unit);
        int gap = Scaled(6, scale);
        var (legendWidth, legendHeight) = Legend(hdc, 0, 0, scale, steps.Count, draw: false);
        return (Math.Max(cardWidth, legendWidth), cardHeight + gap + legendHeight);
    }

    /// <summary>La légende : un échantillon « frappée », un échantillon « tenue », puis l'ordre.</summary>
    private static (int Width, int Height) Legend(IntPtr hdc, int left, int top, float scale, int stepCount, bool draw)
    {
        var font = FontSpec.Of(AppFonts.Text, 12, TypeRamp.Regular, scale);
        var (ascent, descent) = PaperText.Metrics(hdc, font);
        int swatchW = Scaled(13, scale), swatchH = Scaled(11, scale);
        int rowHeight = Math.Max(swatchH, ascent + descent);
        int baseline = top + (rowHeight - (ascent + descent)) / 2 + ascent;
        int swatchTop = top + (rowHeight - swatchH) / 2;
        int between = Scaled(8, scale), beside = Scaled(3, scale);
        int heldBorder = Math.Max(2, (int)Math.Round(2 * scale));

        int x = left;
        // frappée
        if (draw) PaperDraw.RoundBox(hdc, new Win32.RECT { left = x, top = swatchTop, right = x + swatchW - 1, bottom = swatchTop + swatchH - 1 },
            PaperTheme.Action, PaperTheme.Action, Math.Max(1, Scaled(2, scale)));
        x += swatchW + beside;
        x = Text(hdc, L.MiniKeyboard_Struck, font, x, baseline, draw) + between;
        // tenue
        if (draw) PaperDraw.RoundBox(hdc, new Win32.RECT { left = x, top = swatchTop, right = x + swatchW - 1, bottom = swatchTop + swatchH - 1 },
            HeldFill, PaperTheme.Action, Math.Max(1, Scaled(2, scale)), heldBorder);
        x += swatchW + beside;
        x = Text(hdc, L.MiniKeyboard_Held, font, x, baseline, draw) + between;
        // l'ordre
        string order = stepCount > 1 ? L.MiniKeyboard_Order(stepCount) : L.MiniKeyboard_AllAtOnce;
        x = Text(hdc, order, font, x, baseline, draw);
        return (x - left, rowHeight);

        static int Text(IntPtr hdc, string text, FontSpec font, int x, int baseline, bool draw)
            => draw ? PaperText.Draw(hdc, text, font, x, baseline, PaperTheme.TextSecondary) : x + PaperText.Width(hdc, text, font);
    }

    // ── Notation « Une vignette par temps » ────────────────────────────

    /// <summary>Un numéro dans un rond puis « d’abord » / « puis » (« ensemble, en un temps » s'il n'y a
    /// qu'un temps), au-dessus d'un mini-clavier qui ne montre que ce temps, sans pastille.</summary>
    private static string WhenText(int index, int count)
        => count == 1 ? L.MiniKeyboard_Together : index == 0 ? L.MiniKeyboard_First : L.MiniKeyboard_Next;

    /// <summary>Mesures d'une rangée de vignettes : largeur de chaque vignette, entête, flèche.</summary>
    private readonly record struct FramesLayout(int[] Widths, int HeaderHeight, int HeaderGap, int ArrowWidth, int ArrowGap,
        int Width, int Height);

    private static FramesLayout LayoutFrames(IntPtr hdc, float unit, IReadOnlyList<GestureStep> steps, float scale)
    {
        var (miniWidth, miniHeight) = Size(unit);
        int circle = Scaled(20, scale), circleGap = Scaled(7, scale);
        var when = FontSpec.Of(AppFonts.Text, 13, TypeRamp.Regular, scale);
        var arrow = FontSpec.Of(AppFonts.Text, 18, TypeRamp.Regular, scale);
        int arrowWidth = PaperText.Width(hdc, "→", arrow), arrowGap = Scaled(6, scale);

        var widths = new int[steps.Count];
        int total = 0;
        for (int i = 0; i < steps.Count; i++)
        {
            int header = circle + circleGap + PaperText.Width(hdc, WhenText(i, steps.Count), when);
            widths[i] = Math.Max(miniWidth, header);
            total += widths[i];
        }
        total += Math.Max(0, steps.Count - 1) * (arrowWidth + 2 * arrowGap);
        int headerGap = Scaled(5, scale);
        return new FramesLayout(widths, circle, headerGap, arrowWidth, arrowGap, total, circle + headerGap + miniHeight);
    }

    /// <summary>Taille de la rangée de vignettes : tous les temps, entêtes et flèches compris.</summary>
    internal static (int Width, int Height) MeasureFrames(IntPtr hdc, float unit, IReadOnlyList<GestureStep> steps, float scale)
    {
        if (steps.Count == 0) return (0, 0);
        var layout = LayoutFrames(hdc, unit, steps, scale);
        return (layout.Width, layout.Height);
    }

    /// <summary>
    /// Un mini-clavier par temps, seulement les touches de ce temps, sans pastille ; au-dessus de chacun
    /// un numéro dans un rond et « d’abord » / « puis » (« ensemble, en un temps » s'il n'y a qu'un
    /// temps) ; « → » entre les vignettes. Rend la hauteur dessinée.
    /// </summary>
    internal static int DrawFrames(IntPtr hdc, int left, int top, float unit, IReadOnlyList<GestureStep> steps, float scale)
    {
        if (steps.Count == 0) return 0;
        var layout = LayoutFrames(hdc, unit, steps, scale);
        var when = FontSpec.Of(AppFonts.Text, 13, TypeRamp.Regular, scale);
        var number = FontSpec.Of(AppFonts.Text, 12, TypeRamp.Semibold, scale);
        var arrow = FontSpec.Of(AppFonts.Text, 18, TypeRamp.Regular, scale);
        int circle = layout.HeaderHeight;
        int miniTop = top + circle + layout.HeaderGap;
        int miniHeight = Size(unit).Height;

        int x = left;
        for (int i = 0; i < steps.Count; i++)
        {
            var disc = new Win32.RECT { left = x, top = top, right = x + circle, bottom = top + circle };
            Disc(hdc, disc, PaperTheme.Ink);
            PaperText.DrawLine(hdc, (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), number, disc, PaperTheme.Surface,
                TextAlign.Center);
            var text = new Win32.RECT { left = x + circle + Scaled(7, scale), top = top, right = x + layout.Widths[i], bottom = top + circle };
            PaperText.DrawLine(hdc, WhenText(i, steps.Count), when, text, PaperTheme.TextSecondary);

            DrawKeyboard(hdc, x, miniTop, unit, scale, Plan(x, miniTop, unit, steps, onlyStep: i), badges: false);
            x += layout.Widths[i];
            if (i < steps.Count - 1)
            {
                var slot = new Win32.RECT { left = x + layout.ArrowGap, top = miniTop, right = x + layout.ArrowGap + layout.ArrowWidth, bottom = miniTop + miniHeight };
                PaperText.DrawLine(hdc, "→", arrow, slot, PaperTheme.TextSecondary, TextAlign.Center);
                x += layout.ArrowWidth + 2 * layout.ArrowGap;
            }
        }
        return layout.Height;
    }

    private static int Scaled(float size, float scale) => Math.Max(1, (int)Math.Round(size * scale));
}
