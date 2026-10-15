// Notation « Mots » des touches à taper (QCM d'Antoine du 11/10, maquette
// lecons-notation2-1-mots.html) : elle remplace la portée à trois lignes dans les Leçons, la
// recherche et le bandeau du clavier virtuel. Journal : DECISIONS-NOTATION.md (Keyboard Layouts).
namespace AZERTYGlobal;

/// <summary>Les mesures de la notation, en pixels déjà mis à l'échelle. Une par usage.</summary>
internal sealed record KeyNotationStyle
{
    /// <summary>Pavé d'un modificateur tenu (« Maj », « AltGr »).</summary>
    internal int TagHeight { get; init; }
    internal int TagMinWidth { get; init; }
    internal int TagPaddingX { get; init; }
    internal int TagRadius { get; init; }
    internal FontSpec TagFont { get; init; }
    /// <summary>Case de la touche frappée.</summary>
    internal int CaseHeight { get; init; }
    internal int CaseMinWidth { get; init; }
    internal int CasePaddingX { get; init; }
    internal int CaseRadius { get; init; }
    internal int CaseBorder { get; init; }
    internal FontSpec CaseFont { get; init; }
    /// <summary>Case dont le nom est un mot (« Verr. Maj. », « Espace ») : police du texte.</summary>
    internal FontSpec CaseWordFont { get; init; }
    /// <summary>Écart vertical entre pavés et case, horizontal entre un temps et « puis ».</summary>
    internal int Gap { get; init; }
    internal int ThenPaddingX { get; init; }
    internal FontSpec ThenFont { get; init; }
    /// <summary>Note sous une case (« allume », « éteint ») ; 0 si l'usage n'en montre pas.</summary>
    internal int NoteHeight { get; init; }
    internal FontSpec NoteFont { get; init; }

    /// <summary>Leçons : sous le caractère courant, pavés de 26 px, cases de 34 px.</summary>
    internal static KeyNotationStyle Lessons(float s) => new()
    {
        TagHeight = Px(26, s), TagMinWidth = Px(52, s), TagPaddingX = Px(10, s), TagRadius = Px(3, s),
        TagFont = FontSpec.Of(AppFonts.Text, 14, TypeRamp.Semibold, s),
        CaseHeight = Px(34, s), CaseMinWidth = Px(34, s), CasePaddingX = Px(8, s), CaseRadius = Px(4, s), CaseBorder = Px(1.5f, s),
        CaseFont = FontSpec.Of(AppFonts.Mono, 17, TypeRamp.Semibold, s),
        CaseWordFont = FontSpec.Of(AppFonts.Text, 15, TypeRamp.Semibold, s),
        Gap = Px(5, s), ThenPaddingX = Px(5, s), ThenFont = FontSpec.Of(AppFonts.Text, 14, TypeRamp.Regular, s),
        NoteHeight = Px(14, s), NoteFont = FontSpec.Of(AppFonts.Text, 12, TypeRamp.Regular, s),
    };

    /// <summary>Recherche : le geste du caractère choisi, grandes cases de 38 px.</summary>
    internal static KeyNotationStyle Search(float s) => new()
    {
        TagHeight = Px(24, s), TagMinWidth = Px(48, s), TagPaddingX = Px(9, s), TagRadius = Px(3, s),
        TagFont = FontSpec.Of(AppFonts.Text, 13, TypeRamp.Semibold, s),
        CaseHeight = Px(38, s), CaseMinWidth = Px(38, s), CasePaddingX = Px(7, s), CaseRadius = Px(3, s), CaseBorder = Px(1.5f, s),
        CaseFont = FontSpec.Of(AppFonts.Mono, 18, TypeRamp.Semibold, s),
        CaseWordFont = FontSpec.Of(AppFonts.Text, 15, TypeRamp.Semibold, s),
        Gap = Px(4, s), ThenPaddingX = Px(6, s), ThenFont = FontSpec.Of(AppFonts.Text, 14, TypeRamp.Regular, s),
    };

    /// <summary>Bandeau du clavier virtuel : le geste venu de la recherche. Maj et AltGr
    /// empilés tiennent dans les 62 px du bandeau : 16 + 2 + 16 + 2 + 24 = 60.</summary>
    internal static KeyNotationStyle Banner(float s) => new()
    {
        TagHeight = Px(16, s), TagMinWidth = Px(38, s), TagPaddingX = Px(7, s), TagRadius = Px(2, s),
        TagFont = FontSpec.Of(AppFonts.Text, 10, TypeRamp.Semibold, s),
        CaseHeight = Px(24, s), CaseMinWidth = Px(24, s), CasePaddingX = Px(5, s), CaseRadius = Px(3, s), CaseBorder = Px(1, s),
        CaseFont = FontSpec.Of(AppFonts.Mono, 13, TypeRamp.Semibold, s),
        CaseWordFont = FontSpec.Of(AppFonts.Text, 12, TypeRamp.Semibold, s),
        Gap = Px(2, s), ThenPaddingX = Px(4, s), ThenFont = FontSpec.Of(AppFonts.Text, 12, TypeRamp.Regular, s),
    };

    internal static int Px(float value, float scale) => Math.Max(1, (int)Math.Round(value * scale));
}

/// <summary>Une note sous la case d'un temps : Verr. Maj. qui allume ou qui éteint les majuscules.</summary>
internal enum KeyNotationNote { None, TurnsOn, TurnsOff }

/// <summary>
/// La notation « Mots » : une colonne par temps, les modificateurs tenus en pavés pleins nommés
/// au-dessus (Maj puis AltGr), la touche frappée en case cernée en bas, « puis » entre deux
/// temps. Les colonnes s'alignent sur la ligne des cases. Pavé plein = tenir, case = frapper :
/// le lexique de la Tablature, sans ses lignes ni ses longueurs de trait.
/// </summary>
internal static class KeyNotation
{
    /// <summary>
    /// Le geste à montrer pour <paramref name="character"/> dans une leçon : ajoute un dernier
    /// temps « Verr. Maj. (éteint) » quand le geste allume Verr. Maj. et que le caractère suivant
    /// est une minuscule (défaut relevé par Antoine le 08/10 : « Verr. Maj. puis é » ne disait pas
    /// de la couper). Rend aussi le nombre de temps déjà faits : Verr. Maj. déjà allumée, touche
    /// morte déjà armée.
    /// </summary>
    internal static (List<GestureStep> Steps, KeyNotationNote[] Notes, int Done) ForLesson(
        IReadOnlyList<GestureStep> gesture, char? following, bool capsLockActive, bool deadKeyArmed)
    {
        var steps = new List<GestureStep>(gesture);
        bool turnsOn = steps.Count > 0 && steps[0].KeyCode == Gesture.CapsLockCode;
        if (turnsOn && following is char next && char.IsLetter(next) && char.IsLower(next))
            steps.Add(steps[0]);
        var notes = new KeyNotationNote[steps.Count];
        if (turnsOn)
        {
            notes[0] = KeyNotationNote.TurnsOn;
            if (steps.Count > gesture.Count) notes[^1] = KeyNotationNote.TurnsOff;
        }
        int done = 0;
        if (turnsOn && capsLockActive) done = 1;
        else if (!turnsOn && deadKeyArmed && gesture.Count > 1) done = 1;
        return (steps, notes, done);
    }

    private enum Part { Tag, Case, Then }

    private static bool IsWordCase(string label) => label.Length > 2;

    private static string NoteText(KeyNotationNote note) => note switch
    {
        KeyNotationNote.TurnsOn => L.KeyNotation_TurnsOn,
        KeyNotationNote.TurnsOff => L.KeyNotation_TurnsOff,
        _ => "",
    };

    private static IEnumerable<string> HeldNames(GestureStep step)
    {
        if (step.Shift) yield return L.Gesture_LineShift;
        if (step.AltGr) yield return L.Gesture_LineAltGr;
    }

    private static int TagWidth(IntPtr hdc, string name, KeyNotationStyle style)
        => Math.Max(style.TagMinWidth, PaperText.Width(hdc, name, style.TagFont) + 2 * style.TagPaddingX);

    private static int CaseWidth(IntPtr hdc, string label, KeyNotationStyle style)
        => Math.Max(style.CaseMinWidth,
            PaperText.Width(hdc, label, IsWordCase(label) ? style.CaseWordFont : style.CaseFont) + 2 * style.CasePaddingX + 2 * style.CaseBorder);

    private static int ColumnWidth(IntPtr hdc, GestureStep step, KeyNotationNote note, KeyNotationStyle style)
    {
        int w = CaseWidth(hdc, step.KeyLabel, style);
        foreach (var name in HeldNames(step)) w = Math.Max(w, TagWidth(hdc, name, style));
        if (note != KeyNotationNote.None && style.NoteHeight > 0)
            w = Math.Max(w, PaperText.Width(hdc, NoteText(note), style.NoteFont));
        return w;
    }

    private static int ThenWidth(IntPtr hdc, KeyNotationStyle style)
        => PaperText.Width(hdc, L.Gesture_ThenShort, style.ThenFont) + 2 * style.ThenPaddingX;

    private static int MaxHeld(IReadOnlyList<GestureStep> steps)
    {
        int max = 0;
        foreach (var s in steps) max = Math.Max(max, (s.Shift ? 1 : 0) + (s.AltGr ? 1 : 0));
        return max;
    }

    private static bool ShowsNotes(IReadOnlyList<KeyNotationNote>? notes, KeyNotationStyle style)
        => style.NoteHeight > 0 && notes != null && notes.Any(n => n != KeyNotationNote.None);

    /// <summary>Hauteur au-dessus de la ligne des cases : les pavés empilés.</summary>
    internal static int HeightAboveCases(IReadOnlyList<GestureStep> steps, KeyNotationStyle style)
        => MaxHeld(steps) * (style.TagHeight + style.Gap);

    /// <summary>Largeur et hauteur de la notation.</summary>
    internal static (int Width, int Height) Measure(IntPtr hdc, IReadOnlyList<GestureStep> steps, KeyNotationStyle style,
        IReadOnlyList<KeyNotationNote>? notes = null)
    {
        if (steps.Count == 0) return (0, 0);
        int width = 0;
        for (int i = 0; i < steps.Count; i++)
        {
            if (i > 0) width += ThenWidth(hdc, style);
            width += ColumnWidth(hdc, steps[i], notes is null ? KeyNotationNote.None : notes[i], style);
        }
        int height = HeightAboveCases(steps, style) + style.CaseHeight + (ShowsNotes(notes, style) ? style.Gap + style.NoteHeight : 0);
        return (width, height);
    }

    /// <summary>
    /// Dessine la notation, coin haut gauche en (<paramref name="left"/>, <paramref name="top"/>).
    /// <paramref name="ink"/> colore pavés et contours ; les <paramref name="done"/> premiers temps,
    /// déjà faits, passent en pâle. <paramref name="secondInk"/>, s'il est donné, colore le second
    /// temps, comme l'étape 2 du clavier virtuel. Rend la largeur dessinée.
    /// </summary>
    internal static int Draw(IntPtr hdc, IReadOnlyList<GestureStep> steps, KeyNotationStyle style, int left, int top, uint ink,
        IReadOnlyList<KeyNotationNote>? notes = null, int done = 0, uint caseFill = PaperTheme.Surface, uint? secondInk = null)
    {
        if (steps.Count == 0) return 0;
        int caseTop = top + HeightAboveCases(steps, style);
        bool showNotes = ShowsNotes(notes, style);
        int x = left;
        for (int i = 0; i < steps.Count; i++)
        {
            if (i > 0)
            {
                int tw = ThenWidth(hdc, style);
                PaperText.DrawLine(hdc, L.Gesture_ThenShort, style.ThenFont,
                    new Win32.RECT { left = x, top = caseTop, right = x + tw, bottom = caseTop + style.CaseHeight },
                    PaperTheme.TextSecondary, TextAlign.Center);
                x += tw;
            }
            var step = steps[i];
            var note = notes is null ? KeyNotationNote.None : notes[i];
            int cw = ColumnWidth(hdc, step, note, style);
            uint color = i < done ? PaperTheme.Pale : i == 1 && secondInk is uint second ? second : ink;

            // Pavés empilés au-dessus de la case, le plus proche de la case en dernier (Maj, puis AltGr).
            var held = HeldNames(step).ToList();
            int y = caseTop - held.Count * (style.TagHeight + style.Gap);
            foreach (var name in held)
            {
                int w = TagWidth(hdc, name, style);
                var r = new Win32.RECT { left = x + (cw - w) / 2, top = y, right = x + (cw - w) / 2 + w, bottom = y + style.TagHeight };
                PaperDraw.RoundBox(hdc, r, color, color, style.TagRadius);
                PaperText.DrawLine(hdc, name, style.TagFont, r, color == PaperTheme.Action ? PaperTheme.OnAction : PaperTheme.Paper, TextAlign.Center);
                y += style.TagHeight + style.Gap;
            }

            // La case : toujours cernée, jamais pleine, pour ne pas se confondre avec un pavé tenu.
            string label = step.KeyLabel;
            int kw = CaseWidth(hdc, label, style);
            var box = new Win32.RECT { left = x + (cw - kw) / 2, top = caseTop, right = x + (cw - kw) / 2 + kw, bottom = caseTop + style.CaseHeight };
            PaperDraw.RoundBox(hdc, box, caseFill, color, style.CaseRadius, style.CaseBorder);
            PaperText.DrawLine(hdc, label, IsWordCase(label) ? style.CaseWordFont : style.CaseFont, box, color, TextAlign.Center);

            if (showNotes && note != KeyNotationNote.None)
            {
                int ny = caseTop + style.CaseHeight + style.Gap;
                PaperText.DrawLine(hdc, NoteText(note), style.NoteFont,
                    new Win32.RECT { left = x - style.Gap, top = ny, right = x + cw + style.Gap, bottom = ny + style.NoteHeight },
                    PaperTheme.TextSecondary, TextAlign.Center);
            }
            x += cw;
        }
        return x - left;
    }

    /// <summary>
    /// La notation « Recette écrite » : « Maj + AltGr + T », « ù, puis B », « Verr. Maj., é,
    /// puis Verr. Maj. (pour l'éteindre) ». Les noms de touches en gras.
    /// </summary>
    internal static List<TextRun> Recipe(IReadOnlyList<GestureStep> steps, IReadOnlyList<KeyNotationNote>? notes = null)
    {
        var runs = new List<TextRun>();
        for (int i = 0; i < steps.Count; i++)
        {
            if (i > 0) runs.Add(new TextRun(i == steps.Count - 1 ? L.KeyNotation_RecipeThenLast : L.KeyNotation_RecipeThen));
            foreach (var name in HeldNames(steps[i]))
            {
                runs.Add(new TextRun(name, true));
                runs.Add(new TextRun(" + "));
            }
            runs.Add(new TextRun(steps[i].KeyLabel, true));
            if (notes is not null && notes[i] == KeyNotationNote.TurnsOff)
                runs.Add(new TextRun(L.KeyNotation_RecipeTurnsOff));
        }
        return runs;
    }

    /// <summary>La recette en texte simple (lecteur d'écran, infobulle).</summary>
    internal static string PlainRecipe(IReadOnlyList<GestureStep> steps, IReadOnlyList<KeyNotationNote>? notes = null)
        => string.Concat(Recipe(steps, notes).Select(run => run.Text));
}
