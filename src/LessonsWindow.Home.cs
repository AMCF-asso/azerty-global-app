// Accueil des Leçons (direction B, choisie par Antoine le 11/10) : à l'ouverture, « Aujourd'hui »
// (révision du jour, reprise de la leçon en cours, « Votre texte ») puis la carte de maîtrise en
// grand ; « Votre carte » détaille une touche ; « Votre texte » est une page à part. L'écran
// d'exercice (Tablature) reste celui de LessonsWindow.Paper.cs, inchangé.
//
// Cette page n'emprunte rien à LessonsWindow.Paper.cs, refait en parallèle : en-tête, boutons,
// liens et palette sont les siens (préfixe « Home »). Elle ne partage que la mécanique de
// LessonsWindow.cs (zones cliquables AddClick, survol IsHot, contour de focus, DPI par S()).
// La logique pure vit dans LessonsHomeLogic.cs.
using System.Runtime.InteropServices;

namespace AZERTYGlobal;

internal sealed partial class LessonsWindow
{
    /// <summary>Page affichée : l'écran d'exercice, ou l'une des pages de l'accueil.</summary>
    private enum HomePage { Exercise, Today, Map, Text }

    private HomePage _homePage = HomePage.Exercise;
    // ShowChallenge() ouvre directement sur l'exercice du défi : pas d'accueil à l'ouverture.
    private bool _homeOpenOnExercise;
    // « Plus tard » : la révision du jour se replie jusqu'à la prochaine ouverture.
    private bool _homeReviewDeferred;
    // Clic de la souris en cours de traitement, lu par l'action de la carte (une zone, plusieurs touches).
    private (int X, int Y)? _homeClickPoint;
    // Séance composée en cours (révision, touche, texte) : prend la place de l'exercice du
    // catalogue pour CurrentModule, CurrentLesson et CurrentExercise, sans toucher aux index.
    private HomeComposed? _homeComposed;
    private (string Key, HomeSession Session)? _homeReviewCache;
    // Votre carte : la touche choisie (null : celle que DefaultKey propose), celle montrée au
    // dernier repeint, les touches suivies dans l'ordre du clavier dessiné avec leur cadre (clic,
    // flèches), la zone cliquable du clavier, la séance de la touche montrée.
    private uint? _homeMapKey;
    private uint? _homeMapShownKey;
    private readonly List<(uint Scancode, Win32.RECT Rect)> _homeMapKeys = new();
    private Win32.RECT _homeMapZone;
    private (string Key, HomeSession Session)? _homeKeySessionCache;

    private sealed record HomeComposed(LessonModule Module, LessonLesson Lesson, LessonExercise Exercise,
        int ReturnExerciseIndex);

    // ── Accroches de LessonsWindow.cs ────────────────────────────────────

    /// <summary>Une page de l'accueil est à l'écran (ni Paramètres, ni Écriture libre).</summary>
    private bool HomeShown => _homePage != HomePage.Exercise && !_settingsOpen && _mode == WindowMode.Lessons;

    /// <summary>La séance en cours est composée (révision, touche, texte) : le récapitulatif doit
    /// dire « Terminer » plutôt que « Suivant » (accroche de l'en-tête papier).</summary>
    internal bool HomeIsComposedSession => _homeComposed != null;

    private float HomeScale => _dpiScale * _windowScale;

    private FontSpec HomeFont(string family, int size, int weight = TypeRamp.Regular) =>
        FontSpec.Of(family, size, weight, HomeScale);

    /// <summary>À l'ouverture de la fenêtre (de cachée à visible) : « Aujourd'hui », sauf si
    /// l'appelant a positionné un exercice (défi du jour).</summary>
    private void HomeOnShow()
    {
        bool onExercise = _homeOpenOnExercise;
        _homeOpenOnExercise = false;
        if (onExercise)
        {
            _homePage = HomePage.Exercise; // même si la fenêtre était déjà ouverte sur l'accueil
            _focusedActionIndex = -1;
            return;
        }
        if (_visible) return; // un second Show() ne sort personne d'un exercice
        _homePage = HomePage.Today;
        _mode = WindowMode.Lessons;
        _settingsOpen = false;
        _pickerOpen = false;
        _homeReviewDeferred = false;
        _focusedActionIndex = -1;
    }

    /// <summary>Quitter la séance composée : l'exercice du catalogue reprend la main, à l'index
    /// qu'il avait.</summary>
    private void HomeClearComposed()
    {
        if (_homeComposed is not { } composed) return;
        _homeComposed = null;
        // Borné : le catalogue a pu être rechargé pendant la séance (langue, défi du jour).
        _exerciseIndex = Math.Clamp(composed.ReturnExerciseIndex, 0, CurrentLesson.Exercises.Count - 1);
    }

    /// <summary>« Suivant » à la fin d'une séance composée : retour à Aujourd'hui (rien après une
    /// révision). Vrai si c'était une séance composée.</summary>
    private bool HomeFinishComposed()
    {
        if (_homeComposed == null) return false;
        HomeClearComposed();
        HomeShowPage(HomePage.Today);
        return true;
    }

    private void HomeShowPage(HomePage page)
    {
        _settingsOpen = false;
        _pickerOpen = false;
        _mode = WindowMode.Lessons;
        _homePage = page;
        _focusedActionIndex = -1;
        ClearHint();
        Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
    }

    /// <summary>Accroche pour l'en-tête papier : le lien « Aujourd'hui » de l'écran d'exercice.</summary>
    private void HomeShowToday() => HomeShowPage(HomePage.Today);

    /// <summary>« Toutes les leçons », « Reprendre » : l'écran d'exercice, à la position du
    /// catalogue. Une séance composée cède la place ; sans elle, la séance en cours reprend telle
    /// quelle (le texte déjà tapé reste).</summary>
    private void HomeShowExercise()
    {
        bool wasComposed = _homeComposed != null;
        HomeClearComposed();
        _settingsOpen = false;
        _pickerOpen = false;
        _mode = WindowMode.Lessons;
        _homePage = HomePage.Exercise;
        _focusedActionIndex = -1;
        if (wasComposed)
            StartCurrentSession(savePosition: false);
        else
        {
            UpdateAutoHintIfEnabled();
            Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
        }
    }

    /// <summary>
    /// Lance une séance composée sur l'écran d'exercice. L'en-tête papier lit CurrentModule,
    /// CurrentLesson et _exerciseIndex : la séance se présente donc comme un module d'une leçon et
    /// d'un exercice, sans entrer dans le catalogue ; la position du catalogue (celle de
    /// « Reprendre ») n'est pas modifiée et n'est pas enregistrée (StartCurrentSession).
    /// </summary>
    private void HomeStartComposed(LessonExercise exercise, string moduleTitle, string lessonTitle,
        IReadOnlyList<string> characters)
    {
        HomeClearComposed();
        var lesson = new LessonLesson(exercise.ModuleId, exercise.LessonId, lessonTitle, "", characters, new[] { exercise });
        var module = new LessonModule(exercise.ModuleId, moduleTitle, "", "", isSynthetic: true, new[] { lesson });
        _homeComposed = new HomeComposed(module, lesson, exercise, _exerciseIndex);
        _exerciseIndex = 0;
        _settingsOpen = false;
        _pickerOpen = false;
        _mode = WindowMode.Lessons;
        _homePage = HomePage.Exercise;
        _focusedActionIndex = -1;
        StartCurrentSession(savePosition: false);
    }

    /// <summary>Échap : de « Votre carte » ou « Votre texte » vers Aujourd'hui ; sur Aujourd'hui,
    /// la fenêtre se ferme comme avant.</summary>
    private bool HomeEscape()
    {
        if (!HomeShown || _homePage == HomePage.Today) return false;
        HomeShowPage(HomePage.Today);
        return true;
    }

    /// <summary>Touches de l'accueil : rien n'atteint la séance cachée. Sur « Votre carte », les
    /// flèches gauche et droite passent à la touche suivie précédente ou suivante.</summary>
    private bool HomeHandleKey(int vk)
    {
        if (!HomeShown) return false;
        const int VK_LEFT = 0x25, VK_RIGHT = 0x27;
        if (_homePage == HomePage.Map && (vk == VK_LEFT || vk == VK_RIGHT))
            HomeMoveMapKey(vk == VK_RIGHT ? 1 : -1);
        return true;
    }

    private void HomeMoveMapKey(int delta)
    {
        if (_homeMapKeys.Count == 0) return;
        int i = _homeMapKeys.FindIndex(k => k.Scancode == _homeMapShownKey);
        i = i < 0 ? 0 : (i + delta + _homeMapKeys.Count) % _homeMapKeys.Count;
        _homeMapKey = _homeMapKeys[i].Scancode;
        Win32.InvalidateRect(_hWnd, IntPtr.Zero, false);
    }

    /// <summary>« Votre carte », sur <paramref name="key"/> s'il est donné, sinon sur la touche
    /// déjà choisie.</summary>
    private void HomeShowMap(uint? key = null)
    {
        if (key != null) _homeMapKey = key;
        HomeShowPage(HomePage.Map);
    }

    /// <summary>
    /// Le clavier de la carte : une seule zone cliquable (pas une par touche, qui noierait l'ordre
    /// de Tab). Un clic choisit la touche suivie sous le point et ouvre « Votre carte » ; un clic
    /// entre les touches ou sur une touche sans rien à suivre ne fait rien. Au clavier (Entrée,
    /// pas de point dans la zone) : « Votre carte » sur la touche déjà choisie.
    /// </summary>
    private void HomeMapClick()
    {
        var point = _homeClickPoint;
        _homeClickPoint = null;
        var zone = _homeMapZone;
        bool clicked = point is { } at && at.X >= zone.left && at.X <= zone.right && at.Y >= zone.top && at.Y <= zone.bottom;
        uint? key = null;
        if (clicked)
        {
            var (x, y) = point!.Value;
            foreach (var (scancode, rect) in _homeMapKeys)
                if (x >= rect.left && x <= rect.right && y >= rect.top && y <= rect.bottom) { key = scancode; break; }
            if (key == null) return;
        }
        if (_homePage == HomePage.Map)
        {
            if (key != null) _homeMapKey = key;
            Win32.InvalidateRect(_hWnd, IntPtr.Zero, false);
        }
        else
            HomeShowMap(key);
    }

    /// <summary>Caractères tapés sur l'accueil : rien n'atteint la séance cachée.</summary>
    private bool HomeHandleChar(char c)
    {
        if (!HomeShown) return false;
        return true;
    }

    /// <summary>La page de l'accueil a-t-elle un champ où l'on tape (Tab y passe en premier) ?</summary>
    private bool HomePageHasSurface => false;

    /// <summary>Le clic en cours : le point, retenu pour l'action de la carte ; le focus clavier
    /// quitte la cible (pas de surface de frappe à lui rendre sur Aujourd'hui).</summary>
    private void HomeNoteClick(int x, int y)
    {
        _homeClickPoint = (x, y);
        if (HomeShown) _focusedActionIndex = -1;
    }

    private void HomeOnHide()
    {
        _homeClickPoint = null;
    }

    // ── Palette ──────────────────────────────────────────────────────────

    /// <summary>
    /// Couleurs de l'accueil. PaperTheme n'a pas de jeu pour le contraste élevé : celui-ci reprend
    /// la palette « .contraste » des maquettes (fond noir, encre et filets blancs, action cyan).
    /// Le clavier papier (PaperKeyboard) et la Tablature gardent leurs touches claires : ce qu'on
    /// y superpose reste donc en couleurs de PaperTheme.
    /// </summary>
    private readonly record struct HomePalette(uint Paper, uint Surface, uint Ink, uint Muted, uint Border,
        uint Action, uint Pale, uint OnAction, bool HighContrast);

    private static HomePalette HomePaletteNow() => HomeHighContrastOn()
        ? new HomePalette(0x00000000, 0x00000000, 0x00FFFFFF, 0x00FFFFFF, 0x00FFFFFF, 0x00FFEB1A, 0x00FFFFFF, 0x00000000, true)
        : new HomePalette(PaperTheme.Paper, PaperTheme.Surface, PaperTheme.Ink, PaperTheme.TextSecondary,
            PaperTheme.Border, PaperTheme.Action, PaperTheme.Pale, PaperTheme.OnAction, false);

    [StructLayout(LayoutKind.Sequential)]
    private struct HomeHighContrast
    {
        public uint cbSize;
        public uint dwFlags;
        public IntPtr lpszDefaultScheme;
    }

    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW")]
    private static extern bool HomeSystemParametersInfo(uint action, uint param, ref HomeHighContrast value, uint winIni);

    private static bool HomeHighContrastOn()
    {
        const uint SPI_GETHIGHCONTRAST = 0x0042;
        const uint HCF_HIGHCONTRASTON = 0x0001;
        var value = new HomeHighContrast { cbSize = (uint)Marshal.SizeOf<HomeHighContrast>() };
        return HomeSystemParametersInfo(SPI_GETHIGHCONTRAST, value.cbSize, ref value, 0)
               && (value.dwFlags & HCF_HIGHCONTRASTON) != 0;
    }

    // ── Données de la page ───────────────────────────────────────────────

    private sealed record HomeSnapshot(DateTimeOffset Now, IReadOnlyList<GestureMasteryEntry> Due, HomeSession Review,
        IReadOnlyDictionary<uint, HomeKeyState> Keys, IReadOnlyDictionary<string, GestureMasteryEntry> Entries);

    /// <summary>L'état du jour : relu à chaque repeint (le tutoriel verse ses gestes dans le même
    /// fichier ; <see cref="LessonProgressStore.DueGestures"/> le relit s'il a changé). La séance
    /// de révision, elle, est gardée tant que les caractères échus, le jour et la langue restent.</summary>
    private HomeSnapshot HomeBuildSnapshot()
    {
        var now = DateTimeOffset.UtcNow;
        var due = _progress.DueGestures(int.MaxValue);
        var entries = _progress.Gestures;
        string key = $"{L.Language}|{LessonsHomeLogic.DaySeed(DateTimeOffset.Now)}|" +
                     string.Join("", due.Take(LessonsHomeLogic.ReviewCharacters).Select(entry => entry.Character));
        if (_homeReviewCache is not { } cached || cached.Key != key)
        {
            var session = LessonsHomeLogic.BuildReview(_catalog, due, L.LessonsHome_ReviewInstruction,
                LessonsHomeLogic.DaySeed(DateTimeOffset.Now));
            _homeReviewCache = cached = (key, session);
        }
        var keys = LessonsHomeLogic.KeyStates(HomeKeyCatalog.Shared, entries, now);
        return new HomeSnapshot(now, due, cached.Session, keys, entries);
    }

    /// <summary>La position du catalogue (celle de « Reprendre »), même pendant une séance composée.</summary>
    private (LessonModule Module, LessonLesson Lesson, int ExerciseIndex) HomeResumePosition()
    {
        var module = _catalog.Modules[_moduleIndex];
        var lesson = module.Lessons[_lessonIndex];
        int exercise = _homeComposed?.ReturnExerciseIndex ?? _exerciseIndex;
        return (module, lesson, Math.Clamp(exercise, 0, lesson.Exercises.Count - 1));
    }

    private void HomeStartReview()
    {
        var review = HomeBuildSnapshot().Review;
        if (review.Exercise is not { } exercise) return;
        HomeStartComposed(exercise, L.LessonsHome_ReviewTitle, L.LessonsHome_ReviewLessonTitle, review.Covered);
    }

    // ── Dessin de la page ────────────────────────────────────────────────

    private void DrawHome(IntPtr hdc, Win32.RECT rc)
    {
        var pal = HomePaletteNow();
        PaperDraw.Fill(hdc, rc, pal.Paper);
        var snapshot = HomeBuildSnapshot();
        DrawHomeHeader(hdc, rc, pal);
        var body = new Win32.RECT { left = S(24), top = S(66), right = rc.right - S(24), bottom = rc.bottom - S(20) };
        if (_homePage == HomePage.Map)
            DrawHomeMapPage(hdc, body, pal, snapshot);
        else
            DrawHomeToday(hdc, body, pal, snapshot);
        HomeDrawFocus(hdc, pal);
    }

    /// <summary>Le contour de la cible focalisée au clavier, à l'encre de la palette.</summary>
    private void HomeDrawFocus(IntPtr hdc, in HomePalette pal)
    {
        if (_focusedActionIndex < 0 || _clickActions.Count == 0) return;
        if (_focusedActionIndex >= _clickActions.Count) _focusedActionIndex = _clickActions.Count - 1;
        DrawFocusOutline(hdc, _clickActions[_focusedActionIndex].Rect, pal.Ink);
    }

    private string HomeEllipsize(IntPtr hdc, string text, FontSpec font, int maxWidth)
    {
        if (maxWidth <= 0) return "";
        if (PaperText.Width(hdc, text, font) <= maxWidth) return text;
        for (int n = text.Length - 1; n > 0; n--)
        {
            string candidate = text[..n].TrimEnd() + "…";
            if (PaperText.Width(hdc, candidate, font) <= maxWidth) return candidate;
        }
        return "…";
    }

    /// <summary>Lien souligné à la couleur d'action, posé à <paramref name="x"/>, centré sur
    /// <paramref name="midY"/>. Rend son cadre cliquable.</summary>
    private Win32.RECT HomeLink(IntPtr hdc, int x, int midY, string text, string name, Action action,
        in HomePalette pal, string? tooltip = null, bool measureOnly = false, int half = 13)
    {
        var font = HomeFont(AppFonts.Text, 15);
        int width = PaperText.Width(hdc, text, font);
        var rect = new Win32.RECT { left = x - S(4), top = midY - S(half), right = x + width + S(4), bottom = midY + S(half) };
        if (measureOnly) return rect;
        var (ascent, descent) = PaperText.Metrics(hdc, font);
        int baseline = midY - (ascent + descent) / 2 + ascent;
        PaperText.Draw(hdc, text, font, x, baseline, pal.Action);
        int thick = IsHot(rect) ? Math.Max(1, S(2)) : Math.Max(1, S(1));
        PaperDraw.Fill(hdc, x, baseline + S(3), x + width, baseline + S(3) + thick, pal.Action);
        AddClick(rect, action, name);
        if (!string.IsNullOrEmpty(tooltip)) AddHover(rect, tooltip, preferAbove: false, compact: true);
        return rect;
    }

    /// <summary>Bouton rectangulaire : plein à l'action (principal) ou contour d'encre.</summary>
    private void HomeButton(IntPtr hdc, Win32.RECT rect, string text, string name, Action action, bool primary,
        in HomePalette pal)
    {
        bool hot = IsHot(rect);
        uint fill = primary ? pal.Action : (hot && !pal.HighContrast ? PaperDraw.Blend(pal.Action, pal.Surface, 0.07f) : pal.Surface);
        uint border = primary ? pal.Action : (hot ? pal.Action : pal.Ink);
        PaperDraw.RoundBox(hdc, rect, fill, border, S(3));
        PaperText.DrawLine(hdc, text, HomeFont(AppFonts.Text, 14, TypeRamp.Semibold), rect,
            primary ? pal.OnAction : pal.Ink, TextAlign.Center);
        AddClick(rect, action, name);
    }

    private int HomeButtonWidth(IntPtr hdc, string text) =>
        Math.Max(S(96), PaperText.Width(hdc, text, HomeFont(AppFonts.Text, 14, TypeRamp.Semibold)) + S(28));

    // ── En-tête : titre, onglets, liens ──────────────────────────────────

    private void DrawHomeHeader(IntPtr hdc, Win32.RECT rc, in HomePalette pal)
    {
        int mid = S(29);
        int x = S(24);
        var titleFont = HomeFont(AppFonts.Title, 24, TypeRamp.Semibold);
        x += PaperText.DrawLine(hdc, L.LessonsWin_Title, titleFont,
            new Win32.RECT { left = x, top = 0, right = rc.right, bottom = S(58) }, pal.Ink, TextAlign.Left) + S(20);

        // Les liens de droite, mesurés d'abord : les onglets prennent la place qui reste.
        var links = new List<(string Text, Action Action)>
        {
            (L.LessonsPaper_FreeTyping, () => SwitchMode(WindowMode.Free)),
            (L.LessonsWin_SettingsTab, ToggleSettings),
        };
        int gap = S(22);
        int linksWidth = gap * (links.Count - 1);
        foreach (var link in links)
        {
            var measure = HomeLink(hdc, 0, 0, link.Text, link.Text, link.Action, pal, measureOnly: true);
            linksWidth += measure.right - measure.left - S(8);
        }
        int lx = rc.right - S(24) - linksWidth;

        // Onglets : l'onglet de la page courante (Votre texte garde « Aujourd'hui », dont il vient).
        var tabs = new List<(string Text, bool Active, Action Action)>
        {
            (L.LessonsHome_Today, _homePage is HomePage.Today or HomePage.Text, () => HomeShowPage(HomePage.Today)),
            (L.LessonsHome_YourMap, _homePage == HomePage.Map, () => HomeShowMap()),
            (L.LessonsHome_AllLessons, false, HomeShowExercise),
        };
        foreach (var tab in tabs)
        {
            var font = HomeFont(AppFonts.Text, 15, tab.Active ? TypeRamp.Semibold : TypeRamp.Regular);
            int width = PaperText.Width(hdc, tab.Text, font) + S(28);
            var rect = new Win32.RECT { left = x, top = S(12), right = x + width, bottom = S(46) };
            if (rect.right > lx - S(12)) break;
            bool hot = IsHot(rect);
            PaperText.DrawLine(hdc, tab.Text, font, rect, tab.Active || hot ? pal.Ink : pal.Muted, TextAlign.Center);
            if (tab.Active)
                PaperDraw.Fill(hdc, rect.left, rect.bottom - Math.Max(2, S(2)), rect.right, rect.bottom, pal.Ink);
            AddClick(rect, tab.Action, tab.Text, LessonControlKind.ListItem, selected: tab.Active);
            x = rect.right + S(4);
        }

        foreach (var link in links)
        {
            var rect = HomeLink(hdc, lx, mid, link.Text, link.Text, link.Action, pal);
            lx = rect.right - S(4) + gap;
        }
    }

    /// <summary>
    /// Accroche pour l'écran d'exercice : le lien « ‹ Aujourd'hui » sous l'en-tête papier, qui
    /// ramène à l'accueil. À retirer quand l'en-tête papier porte lui-même ce lien
    /// (<see cref="HomeShowToday"/>).
    /// </summary>
    private void HomeDrawReturnLink(IntPtr hdc)
    {
        if (_mode != WindowMode.Lessons || _settingsOpen) return;
        var pal = HomePaletteNow();
        // Entre l'en-tête (58) et le corps (88) : ni le cadre cliquable ni l'anneau de focus de
        // la partition ne le recouvrent.
        HomeLink(hdc, S(24), S(71), L.LessonsHome_BackToToday, L.LessonsHome_BackToTodayTooltip, HomeShowToday, pal,
            L.LessonsHome_BackToTodayTooltip, half: 9);
    }

    // ── Aujourd'hui ──────────────────────────────────────────────────────

    private void DrawHomeToday(IntPtr hdc, Win32.RECT body, in HomePalette pal, HomeSnapshot snapshot)
    {
        int gap = S(14);
        int width = body.right - body.left;
        int reviewW = (int)((width - 2 * gap) * 1.55 / 3.55);
        int otherW = (width - 2 * gap - reviewW) / 2;
        int top = body.top;
        int cardH = S(226);
        var review = new Win32.RECT { left = body.left, top = top, right = body.left + reviewW, bottom = top + cardH };
        var resume = new Win32.RECT { left = review.right + gap, top = top, right = review.right + gap + otherW, bottom = top + cardH };
        var text = new Win32.RECT { left = resume.right + gap, top = top, right = body.right, bottom = top + cardH };

        DrawHomeReviewCard(hdc, review, pal, snapshot);
        DrawHomeResumeCard(hdc, resume, pal);
        DrawHomeTextCard(hdc, text, pal);

        var map = new Win32.RECT { left = body.left, top = review.bottom + S(16), right = body.right, bottom = body.bottom };
        DrawHomeMap(hdc, map, pal, snapshot, L.LessonsHome_MapTitle, L.LessonsHome_MapHint, selected: null);

        _surfaceView = HomeSurface(L.LessonsHome_Today, HomeTodaySummary(snapshot), body);
    }

    private LessonSurfaceView HomeSurface(string name, string text, Win32.RECT box) =>
        new(name, L.LessonsHome_SurfaceHelp, text, 0, box, Array.Empty<Win32.RECT>(), null);

    private void HomeCard(IntPtr hdc, Win32.RECT rect, in HomePalette pal, bool first)
    {
        PaperDraw.RoundBox(hdc, rect, pal.Surface, first ? pal.Ink : pal.Border, S(3));
    }

    private void HomeCardHeading(IntPtr hdc, Win32.RECT card, string title, string? sub, in HomePalette pal)
    {
        int pad = S(16);
        var titleFont = HomeFont(AppFonts.Title, 19, TypeRamp.Semibold);
        PaperText.DrawLine(hdc, HomeEllipsize(hdc, title, titleFont, card.right - card.left - 2 * pad), titleFont,
            new Win32.RECT { left = card.left + pad, top = card.top + S(14), right = card.right - pad, bottom = card.top + S(40) },
            pal.Ink, TextAlign.Left);
        if (sub == null) return;
        var subFont = HomeFont(AppFonts.Text, 13);
        PaperText.DrawLine(hdc, HomeEllipsize(hdc, sub, subFont, card.right - card.left - 2 * pad), subFont,
            new Win32.RECT { left = card.left + pad, top = card.top + S(41), right = card.right - pad, bottom = card.top + S(59) },
            pal.Muted, TextAlign.Left);
    }

    private int HomeParagraph(IntPtr hdc, Win32.RECT rect, IReadOnlyList<TextRun> runs, in HomePalette pal, bool draw = true) =>
        PaperText.DrawParagraph(hdc, runs, HomeFont(AppFonts.Text, 13), HomeFont(AppFonts.Text, 13, TypeRamp.Semibold),
            rect, pal.Muted, pal.Ink, S(19), draw);

    /// <summary>Le caractère tel qu'on le montre en grand : un espace ou une marque combinante se
    /// disent par un signe.</summary>
    private static string HomeGlyph(string character)
    {
        if (character.Length == 1 && char.IsWhiteSpace(character[0])) return "·";
        if (character.Length == 1 && KeyboardRenderer.IsCombiningMark(character[0])) return "◌" + character;
        return character;
    }

    // ── Révision du jour ─────────────────────────────────────────────────

    private void DrawHomeReviewCard(IntPtr hdc, Win32.RECT card, in HomePalette pal, HomeSnapshot snapshot)
    {
        HomeCard(hdc, card, pal, first: true);
        int due = snapshot.Due.Count;
        var review = snapshot.Review;
        HomeCardHeading(hdc, card, L.LessonsHome_ReviewTitle,
            due == 0 ? L.LessonsHome_ReviewNone : L.LessonsHome_ReviewCount(due), pal);

        int pad = S(16);
        int inner = card.right - card.left - 2 * pad;
        int footerTop = card.bottom - S(16) - S(34);
        var paragraph = new Win32.RECT { left = card.left + pad, top = footerTop - S(8) - S(38), right = card.right - pad, bottom = footerTop - S(8) };

        if (due == 0)
        {
            HomeParagraph(hdc, new Win32.RECT { left = paragraph.left, top = card.top + S(72), right = paragraph.right, bottom = paragraph.bottom },
                new[] { new TextRun(L.LessonsHome_ReviewNoneHint) }, pal);
            return;
        }

        if (_homeReviewDeferred)
        {
            HomeParagraph(hdc, new Win32.RECT { left = paragraph.left, top = card.top + S(72), right = paragraph.right, bottom = paragraph.bottom },
                new[] { new TextRun(L.LessonsHome_ReviewDeferred) }, pal);
        }
        else
        {
            DrawHomeDueStrip(hdc, new Win32.RECT { left = card.left + pad, top = card.top + S(68), right = card.right - pad, bottom = card.top + S(68 + 60) },
                snapshot.Due, pal);
            var runs = new List<TextRun>();
            if (review.Words.Count > 0)
            {
                runs.Add(new TextRun(L.LessonsHome_ReviewWords("\u0001")));
                // Les mots en gras : le gabarit coupe autour d'un repère.
                runs = HomeWordsRuns(L.LessonsHome_ReviewWords("\u0001"), string.Join(", ", review.Words));
                if (review.Missing.Count > 0)
                    runs.Add(new TextRun(" " + L.LessonsHome_ReviewMissing(string.Join(", ", review.Missing))));
            }
            else
            {
                runs.Add(new TextRun(L.LessonsHome_ReviewNoWords));
            }
            HomeParagraph(hdc, paragraph, runs, pal);
        }

        // Pied : Réviser (si une séance existe), Plus tard.
        int x = card.left + pad;
        if (review.Exercise != null)
        {
            string label = L.LessonsHome_ReviewButton;
            int buttonW = HomeButtonWidth(hdc, label);
            var button = new Win32.RECT { left = x, top = footerTop, right = x + buttonW, bottom = footerTop + S(34) };
            HomeButton(hdc, button, label, L.LessonsHome_ReviewButtonName(due), HomeStartReview, primary: true, pal);
            x = button.right + S(14);
        }
        if (!_homeReviewDeferred)
            HomeLink(hdc, x, footerTop + S(17), L.LessonsHome_ReviewLater, L.LessonsHome_ReviewLater,
                () => { _homeReviewDeferred = true; Win32.InvalidateRect(_hWnd, IntPtr.Zero, false); }, pal);
    }

    /// <summary>« Dans des mots des leçons : <b>École, cœur</b>. » : le gabarit de la langue est
    /// coupé autour du repère, les mots prennent le gras.</summary>
    private static List<TextRun> HomeWordsRuns(string template, string words)
    {
        int at = template.IndexOf('\u0001');
        var runs = new List<TextRun>();
        if (at < 0) { runs.Add(new TextRun(template)); return runs; }
        runs.Add(new TextRun(template[..at]));
        runs.Add(new TextRun(words, true));
        runs.Add(new TextRun(template[(at + 1)..]));
        return runs;
    }

    /// <summary>
    /// Les caractères à revoir en grand, chacun sur son geste en abrégé (le trait pour ce qui se
    /// tient, la case pour ce qui se frappe : <see cref="TablatureRenderer.DrawShortGesture"/>).
    /// Ce qui ne tient pas dans la largeur devient « +N ».
    /// </summary>
    private void DrawHomeDueStrip(IntPtr hdc, Win32.RECT area, IReadOnlyList<GestureMasteryEntry> due, in HomePalette pal)
    {
        var glyphFont = HomeFont(AppFonts.Title, 26, TypeRamp.Semibold);
        var moreFont = HomeFont(AppFonts.Text, 13);
        int pad = S(8), minCell = S(40), moreWidth = S(40);
        int glyphH = S(32);
        int x = area.left;
        int shown = 0;
        for (int i = 0; i < due.Count; i++)
        {
            string glyph = HomeGlyph(due[i].Character);
            var steps = Gesture.Of(due[i].Character);
            int gestureW = steps.Count == 0 ? 0 : TablatureRenderer.DrawShortGesture(hdc, steps, false, HomeScale, 0, 0, draw: false);
            int cell = Math.Max(minCell, Math.Max(gestureW, PaperText.Width(hdc, glyph, glyphFont))) + 2 * pad;
            int reserve = i < due.Count - 1 ? moreWidth : 0;
            if (x + cell + reserve > area.right) break;
            int centre = x + cell / 2;
            PaperText.DrawLine(hdc, glyph, glyphFont,
                new Win32.RECT { left = x, top = area.top, right = x + cell, bottom = area.top + glyphH }, pal.Ink, TextAlign.Center);
            if (steps.Count > 0)
                TablatureRenderer.DrawShortGesture(hdc, steps, false, HomeScale, centre + gestureW / 2, area.top + glyphH + S(15));
            x += cell;
            shown++;
            if (i < due.Count - 1 && x + moreWidth <= area.right)
                PaperDraw.Fill(hdc, x, area.top + S(4), x + 1, area.top + glyphH + S(26), pal.Border);
        }
        if (shown < due.Count)
            PaperText.DrawLine(hdc, L.LessonsHome_ReviewMore(due.Count - shown), moreFont,
                new Win32.RECT { left = x + S(8), top = area.top, right = area.right, bottom = area.top + glyphH }, pal.Muted, TextAlign.Left);
    }

    // ── Reprendre ────────────────────────────────────────────────────────

    private void DrawHomeResumeCard(IntPtr hdc, Win32.RECT card, in HomePalette pal)
    {
        HomeCard(hdc, card, pal, first: false);
        var (module, lesson, exerciseIndex) = HomeResumePosition();
        bool started = _progress.CountCompleted(_catalog) > 0;
        HomeCardHeading(hdc, card, started ? L.LessonsHome_ResumeTitle : L.LessonsHome_StartTitle,
            $"{module.Title} · {lesson.Title}", pal);

        int pad = S(16);
        int inner = card.right - card.left - 2 * pad;
        int footerTop = card.bottom - S(16) - S(34);

        // Jalons : un trait par exercice de la leçon.
        int count = lesson.Exercises.Count;
        int gapJ = S(3);
        int segment = S(22);
        if (count * (segment + gapJ) - gapJ > inner)
            segment = Math.Max(S(4), (inner + gapJ) / Math.Max(1, count) - gapJ);
        int jx = card.left + pad;
        int jy = card.top + S(72);
        for (int i = 0; i < count; i++)
        {
            uint color = i == exerciseIndex ? pal.Action : _progress.IsCompleted(lesson.Exercises[i]) ? pal.Ink : pal.Border;
            PaperDraw.Fill(hdc, jx, jy, jx + segment, jy + Math.Max(2, S(3)), color);
            jx += segment + gapJ;
        }

        var small = HomeFont(AppFonts.Text, 13);
        PaperText.DrawLine(hdc, L.LessonsPaper_ExerciseOf(exerciseIndex + 1, count), small,
            new Win32.RECT { left = card.left + pad, top = jy + S(8), right = card.right - pad, bottom = jy + S(26) }, pal.Muted, TextAlign.Left);

        // L'extrait : la première ligne de l'exercice, ce qui est déjà tapé en encre.
        var exercise = lesson.Exercises[exerciseIndex];
        string line = exercise.Lines.Length > 0 ? exercise.Lines[0] : "";
        int typed = 0;
        if (_homeComposed == null && _session.Exercise == exercise)
            typed = _session.LineIndex > 0 ? line.Length : Math.Min(_session.CursorPosition, line.Length);
        var extractFont = HomeFont(AppFonts.Title, 21, TypeRamp.Semibold);
        string shownLine = HomeEllipsize(hdc, line, extractFont, inner);
        var (ascent, descent) = PaperText.Metrics(hdc, extractFont);
        int baseline = jy + S(34) + S(28) - (ascent + descent) / 2 + ascent;
        int inked = Math.Min(typed, shownLine.Length);
        int ex = card.left + pad;
        if (inked > 0)
            ex = PaperText.Draw(hdc, shownLine[..inked], extractFont, ex, baseline, pal.Ink);
        if (inked < shownLine.Length)
            PaperText.Draw(hdc, shownLine[inked..], extractFont, ex, baseline, inked > 0 ? pal.Pale : pal.Ink);

        string label = started ? L.LessonsHome_ResumeButton : L.LessonsHome_StartButton;
        int buttonW = HomeButtonWidth(hdc, label);
        var button = new Win32.RECT { left = card.left + pad, top = footerTop, right = card.left + pad + buttonW, bottom = footerTop + S(34) };
        HomeButton(hdc, button, label, started
                ? L.LessonsHome_ResumeName(module.Title, lesson.Title, exerciseIndex + 1, count)
                : L.LessonsHome_StartName(module.Title, lesson.Title, exerciseIndex + 1, count),
            HomeShowExercise, primary: false, pal);
    }

    // ── Votre texte (carte de l'accueil) ─────────────────────────────────

    private void DrawHomeTextCard(IntPtr hdc, Win32.RECT card, in HomePalette pal)
    {
        HomeCard(hdc, card, pal, first: false);
        HomeCardHeading(hdc, card, L.LessonsHome_YourText, L.LessonsHome_TextSub, pal);
        int pad = S(16);
        var runs = new List<TextRun>
        {
            new(L.LessonsHome_TextBodyLead),
            new(L.LessonsHome_TextBodyStrong, true),
            new(L.LessonsHome_TextBodyTail),
        };
        HomeParagraph(hdc, new Win32.RECT { left = card.left + pad, top = card.top + S(72), right = card.right - pad, bottom = card.bottom - S(60) },
            runs, pal);
    }

    // ── Carte de maîtrise en grand (lecture « un niveau par touche ») ────

    /// <summary>
    /// Un petit tampon 1×1 pour AlphaBlend (msimg32) : une couleur, une opacité, un rectangle.
    /// Un seul pour toute la carte, libéré à la fin (même principe que
    /// LearningModule.PaintFocusLostOverlay).
    /// </summary>
    private sealed class HomeBlender : IDisposable
    {
        private readonly IntPtr _hdc;
        private readonly IntPtr _mem;
        private readonly IntPtr _bitmap;
        private readonly IntPtr _old;

        public HomeBlender(IntPtr hdc)
        {
            _hdc = hdc;
            _mem = Win32.CreateCompatibleDC(hdc);
            _bitmap = Win32.CreateCompatibleBitmap(hdc, 1, 1);
            _old = Win32.SelectObject(_mem, _bitmap);
        }

        /// <summary>Pose <paramref name="color"/> à <paramref name="alpha"/> (0 à 255) sur le rectangle.</summary>
        public void Fill(Win32.RECT rect, uint color, byte alpha)
        {
            int width = rect.right - rect.left + 1, height = rect.bottom - rect.top + 1;
            if (width <= 0 || height <= 0) return;
            var one = new Win32.RECT { left = 0, top = 0, right = 1, bottom = 1 };
            var brush = Win32.CreateSolidBrush(color);
            Win32.FillRect(_mem, ref one, brush);
            Win32.DeleteObject(brush);
            var blend = new Win32.BLENDFUNCTION
            {
                BlendOp = Win32.AC_SRC_OVER, BlendFlags = 0, SourceConstantAlpha = alpha, AlphaFormat = 0,
            };
            Win32.AlphaBlend(_hdc, rect.left, rect.top, width, height, _mem, 0, 0, 1, 1, blend);
        }

        public void Dispose()
        {
            Win32.SelectObject(_mem, _old);
            Win32.DeleteObject(_bitmap);
            Win32.DeleteDC(_mem);
        }
    }

    /// <summary>Le triangle de la corne : le coin haut droit d'une touche qui a des caractères à revoir.</summary>
    private static void HomeDrawCorner(IntPtr hdc, Win32.RECT key, int size, uint color)
    {
        var points = new[]
        {
            new Win32.POINT { x = key.right - size, y = key.top },
            new Win32.POINT { x = key.right + 1, y = key.top },
            new Win32.POINT { x = key.right + 1, y = key.top + size + 1 },
        };
        var brush = Win32.CreateSolidBrush(color);
        var pen = Win32.CreatePen(0, 1, color);
        var oldBrush = Win32.SelectObject(hdc, brush);
        var oldPen = Win32.SelectObject(hdc, pen);
        Win32.Polygon(hdc, points, points.Length);
        Win32.SelectObject(hdc, oldPen);
        Win32.SelectObject(hdc, oldBrush);
        Win32.DeleteObject(pen);
        Win32.DeleteObject(brush);
    }

    /// <summary>Le clavier papier (quatre légendes par touche) et, par-dessus, ce que la progression
    /// en dit : une touche se remplit du bas selon le niveau moyen de ses caractères tapés, porte une
    /// corne si l'un d'eux est à revoir, un bord pâle s'il n'a jamais été tapé, et s'efface s'il n'y
    /// a rien à suivre. Chaque touche suivie a son infobulle ; un clic la choisit
    /// (<see cref="HomeMapClick"/>). <paramref name="selected"/> : la touche encadrée de
    /// « Votre carte ». Sans <paramref name="title"/>, l'aide occupe seule la ligne du haut.
    /// Rend le bas de la légende.</summary>
    private int DrawHomeMap(IntPtr hdc, Win32.RECT area, in HomePalette pal, HomeSnapshot snapshot,
        string? title, string hint, uint? selected)
    {
        var titleFont = HomeFont(AppFonts.Title, 19, TypeRamp.Semibold);
        int titleW = title == null ? -S(18) : PaperText.DrawLine(hdc, title, titleFont,
            new Win32.RECT { left = area.left, top = area.top, right = area.right, bottom = area.top + S(28) },
            pal.Ink, TextAlign.Left);
        var hintFont = HomeFont(AppFonts.Text, 13);
        int hintLeft = area.left + titleW + S(18);
        PaperText.DrawLine(hdc, HomeEllipsize(hdc, hint, hintFont, area.right - hintLeft), hintFont,
            new Win32.RECT { left = hintLeft, top = area.top + S(4), right = area.right, bottom = area.top + S(28) },
            pal.Muted, TextAlign.Left);

        int legendH = S(30);
        int kbTop = area.top + S(36);
        var (unitW, unitH) = PaperKeyboard.Size(1000f);
        float unit = Math.Min(S(58), Math.Min((area.right - area.left) * 1000f / unitW,
            (area.bottom - kbTop - legendH) * 1000f / unitH));
        var placement = PaperKeyboard.PlaceAt(area.left, kbTop, unit);
        var (kbW, kbH) = PaperKeyboard.Size(unit);
        PaperKeyboard.Draw(hdc, placement, _layout, PaperKeyboardMode.Map,
            new PaperKeyboardState { Profile = KeyboardRenderProfile.Full });

        _homeMapKeys.Clear();
        Win32.RECT? selectedRect = null;
        using (var blender = new HomeBlender(hdc))
        {
            foreach (var key in KeyboardRenderer.VisualKeys)
            {
                var rect = PaperKeyboard.ToRect(key, placement);
                snapshot.Keys.TryGetValue(key.Scancode, out var state);
                if (key.Scancode == 0 || state == null || state.Kind == HomeKeyKind.Nothing)
                {
                    // Rien à suivre : la touche s'efface dans le papier.
                    blender.Fill(rect, pal.Paper, pal.HighContrast ? (byte)150 : (byte)140);
                    continue;
                }
                _homeMapKeys.Add((key.Scancode, rect));
                if (key.Scancode == selected) selectedRect = rect;
                if (state.Kind == HomeKeyKind.Unseen)
                {
                    PaperDraw.Frame(hdc, rect, pal.Pale, pal.HighContrast ? Math.Max(2, S(2)) : 1);
                }
                else
                {
                    int height = rect.bottom - rect.top + 1;
                    int fill = Math.Max(Math.Max(2, S(3)), (int)Math.Round(height * state.Level));
                    var level = new Win32.RECT { left = rect.left, top = rect.bottom - fill + 1, right = rect.right, bottom = rect.bottom };
                    blender.Fill(level, pal.Action, pal.HighContrast ? (byte)150 : (byte)80);
                    PaperDraw.Fill(hdc, level.left, level.top, level.right + 1, level.top + Math.Max(2, S(2)), pal.Action);
                    if (state.Due > 0)
                        HomeDrawCorner(hdc, rect, Math.Max(S(11), (int)(unit * 0.22f)), pal.Ink);
                }
                AddHover(rect, HomeKeyTooltip(key, state, snapshot), preferAbove: false, compact: true);
            }
        }

        // La touche choisie : un cadre à la couleur d'action, détaché de la touche.
        if (selectedRect is { } chosen)
        {
            int thick = Math.Max(2, S(3));
            int off = Math.Max(1, S(2)) + thick;
            PaperDraw.Frame(hdc, new Win32.RECT { left = chosen.left - off, top = chosen.top - off, right = chosen.right + off, bottom = chosen.bottom + off },
                pal.Action, thick);
        }

        _homeMapZone = new Win32.RECT { left = area.left, top = kbTop, right = area.left + kbW, bottom = kbTop + kbH };
        AddClick(_homeMapZone, HomeMapClick, L.LessonsHome_MapKeysName);

        // Légende : une ligne, ce qui ne tient pas est abrégé.
        int legendTop = kbTop + kbH + S(10);
        var p = pal; // une copie : un paramètre « in » ne se capture pas dans une lambda
        var legend = new (string Text, Action<Win32.RECT> Swatch)[]
        {
            (L.LessonsHome_LegendLevel, r => { using var b = new HomeBlender(hdc);
                b.Fill(r, p.Action, 80); PaperDraw.Fill(hdc, r.left, r.top, r.right + 1, r.top + Math.Max(2, S(2)), p.Action); PaperDraw.Frame(hdc, r, p.Border); }),
            (L.LessonsHome_LegendDue, r => { PaperDraw.Frame(hdc, r, p.Border); HomeDrawCorner(hdc, r, S(8), p.Ink); }),
            (L.LessonsHome_LegendUnseen, r => PaperDraw.Frame(hdc, r, p.Pale)),
            (L.LessonsHome_LegendNothing, r => { PaperDraw.Frame(hdc, r, p.HighContrast ? p.Border : PaperTheme.KeyFadedBorder); }),
        };
        // Chaque entrée prend sa largeur réelle quand la ligne les tient toutes ; sinon parts égales.
        int sw = S(14), gap = S(20), lx = area.left;
        var textWidths = legend.Select(item => PaperText.Width(hdc, item.Text, hintFont)).ToArray();
        int natural = textWidths.Sum() + legend.Length * (sw + S(6)) + (legend.Length - 1) * gap;
        bool fits = natural <= area.right - area.left;
        int share = Math.Max(S(120), (area.right - area.left) / legend.Length);
        for (int i = 0; i < legend.Length; i++)
        {
            var item = legend[i];
            int width = fits ? sw + S(6) + textWidths[i] + gap : share;
            var swatch = new Win32.RECT { left = lx, top = legendTop + S(4), right = lx + sw, bottom = legendTop + S(4) + sw };
            item.Swatch(swatch);
            string text = fits ? item.Text : HomeEllipsize(hdc, item.Text, hintFont, share - sw - S(16));
            PaperText.DrawLine(hdc, text, hintFont,
                new Win32.RECT { left = lx + sw + S(6), top = legendTop, right = lx + width, bottom = legendTop + S(22) },
                pal.Muted, TextAlign.Left);
            lx += width;
        }
        return legendTop + S(22);
    }

    private string HomeLevelText(int box) => box < 0 ? L.LessonsHome_LevelNever : L.LessonsHome_LevelOf(box);

    /// <summary>L'infobulle d'une touche : le nombre de caractères tapés sur ceux qu'elle suit, puis
    /// les caractères couche par couche (à revoir d'abord).</summary>
    private string HomeKeyTooltip(VirtualKeyboard.VisualKey key, HomeKeyState state, HomeSnapshot snapshot)
    {
        var lines = new List<string> { L.LessonsHome_KeyTooltipTitle(L.Keyboard_KeyCap(key.Label), state.Seen, state.Followed) };
        int listed = 0;
        foreach (var layer in LessonsHomeLogic.KeyDetail(HomeKeyCatalog.Shared, snapshot.Entries, key.Scancode, snapshot.Now, 4))
            foreach (var line in layer.Lines)
            {
                if (listed >= 8) break;
                lines.Add(L.LessonsHome_KeyTooltipLine(HomeGlyph(line.Character), HomeLevelText(line.Box), line.Due));
                listed++;
            }
        if (state.Followed > listed)
            lines.Add(L.LessonsHome_KeyTooltipMore(state.Followed - listed));
        return string.Join("\n", lines);
    }

    // ── Votre carte (maquette lecons-innov-b-carte, lecture d'Aujourd'hui : QCM du 12/10) ──

    private void DrawHomeMapPage(IntPtr hdc, Win32.RECT body, in HomePalette pal, HomeSnapshot snapshot)
    {
        uint? key = _homeMapKey is { } chosen && snapshot.Keys.ContainsKey(chosen)
            ? chosen
            : LessonsHomeLogic.DefaultKey(snapshot.Keys);
        _homeMapShownKey = key;
        int bandH = S(200);
        var map = new Win32.RECT { left = body.left, top = body.top, right = body.right, bottom = body.bottom - bandH - S(14) };
        // Le clavier plafonne en largeur : la bande prend la place qu'il laisse.
        int mapBottom = DrawHomeMap(hdc, map, pal, snapshot, null, L.LessonsHome_MapClickHint, key);
        var band = new Win32.RECT { left = body.left, top = Math.Min(map.bottom, mapBottom) + S(14), right = body.right, bottom = body.bottom };
        DrawHomeKeyBand(hdc, band, pal, snapshot, key);
        _surfaceView = HomeSurface(L.LessonsHome_YourMap, HomeMapSummary(snapshot, key), body);
    }

    /// <summary>La gravure d'une touche, telle que le clavier la nomme (« ù », « 2 »).</summary>
    private static string HomeKeyCap(uint scancode)
    {
        foreach (var key in KeyboardRenderer.VisualKeys)
            if (key.Scancode == scancode) return L.Keyboard_KeyCap(key.Label);
        return "?";
    }

    /// <summary>La touche morte qu'arme ce temps, ou null pour un temps qui tape un caractère.</summary>
    private static string? HomeDeadKeyOf(GestureStep step)
    {
        foreach (var (deadKey, (key, layer)) in CharacterIndex.Shared.DeadKeyActivations)
            if (key == step.KeyCode && layer.Contains("Shift", StringComparison.Ordinal) == step.Shift
                && layer.Contains("AltGr", StringComparison.Ordinal) == step.AltGr)
                return deadKey;
        return null;
    }

    /// <summary>Le nom d'une touche morte en casse de phrase (« Accent aigu »).</summary>
    private static string HomeDeadKeyName(string deadKey)
    {
        string name = VirtualKeyboard.GetDeadKeyDisplayName(deadKey).ToLowerInvariant();
        return name.Length == 0 ? name : char.ToUpperInvariant(name[0]) + name[1..];
    }

    /// <summary>La séance d'une touche, gardée tant que la touche, le jour, la langue et son état
    /// restent (BuildKeySession parcourt tous les textes des leçons).</summary>
    private HomeSession HomeKeySession(HomeSnapshot snapshot, uint scancode, HomeKeyState state)
    {
        int seed = LessonsHomeLogic.DaySeed(DateTimeOffset.Now);
        string key = $"{scancode}|{L.Language}|{seed}|{state.Seen}|{state.Due}|{state.Level:F3}";
        if (_homeKeySessionCache is not { } cached || cached.Key != key)
        {
            var session = LessonsHomeLogic.BuildKeySession(_catalog, HomeKeyCatalog.Shared, snapshot.Entries, scancode,
                snapshot.Now, L.LessonsHome_KeySessionInstruction, seed);
            _homeKeySessionCache = cached = (key, session);
        }
        return cached.Session;
    }

    private void HomeStartKeySession(uint scancode)
    {
        var snapshot = HomeBuildSnapshot();
        if (!snapshot.Keys.TryGetValue(scancode, out var state)) return;
        var session = HomeKeySession(snapshot, scancode, state);
        if (session.Exercise is not { } exercise) return;
        HomeStartComposed(exercise, L.LessonsHome_YourMap, L.LessonsHome_KeyTitle(HomeKeyCap(scancode)), session.Covered);
    }

    private static string HomeKeySessionNote(HomeSession session) => session.Exercise == null
        ? L.LessonsHome_KeySessionNone
        : L.LessonsHome_KeySessionNote(session.Characters.Where(c => c != session.NewCharacter).ToList(), session.NewCharacter);

    /// <summary>
    /// La bande du bas : la touche choisie (gravure, ce qu'elle suit, la séance), puis une colonne
    /// par couche qui suit quelque chose, ses caractères les plus utiles avec leur boîte en cinq crans.
    /// </summary>
    private void DrawHomeKeyBand(IntPtr hdc, Win32.RECT band, in HomePalette pal, HomeSnapshot snapshot, uint? scancode)
    {
        PaperDraw.RoundBox(hdc, band, pal.Surface, pal.Ink, S(3));
        int pad = S(14);
        if (scancode is not { } sc || !snapshot.Keys.TryGetValue(sc, out var state))
        {
            HomeParagraph(hdc, new Win32.RECT { left = band.left + pad, top = band.top + pad, right = band.right - pad, bottom = band.bottom - pad },
                new[] { new TextRun(L.LessonsHome_MapEmpty) }, pal);
            return;
        }
        string cap = HomeKeyCap(sc);
        var session = HomeKeySession(snapshot, sc, state);

        // Colonne de tête : gravure et titre, ce que la touche suit, la séance.
        string buttonText = L.LessonsHome_KeySessionButton;
        int buttonW = HomeButtonWidth(hdc, buttonText);
        int headW = Math.Max(S(230), buttonW + 2 * pad);
        int x0 = band.left + pad, x1 = band.left + headW - pad;
        var capFont = HomeFont(AppFonts.Mono, 16, TypeRamp.Semibold);
        int capW = Math.Max(S(30), PaperText.Width(hdc, cap, capFont) + S(12));
        var capRect = new Win32.RECT { left = x0, top = band.top + S(12), right = x0 + capW, bottom = band.top + S(42) };
        PaperDraw.RoundBox(hdc, capRect, pal.Surface, pal.Ink, S(3));
        PaperText.DrawLine(hdc, cap, capFont, capRect, pal.Ink, TextAlign.Center);
        var titleFont = HomeFont(AppFonts.Title, 22, TypeRamp.Semibold);
        int tx = capRect.right + S(8);
        PaperText.DrawLine(hdc, HomeEllipsize(hdc, L.LessonsHome_KeyTitle(cap), titleFont, x1 - tx), titleFont,
            new Win32.RECT { left = tx, top = capRect.top - S(2), right = x1, bottom = capRect.bottom + S(2) }, pal.Ink, TextAlign.Left);
        HomeParagraph(hdc, new Win32.RECT { left = x0, top = capRect.bottom + S(8), right = x1, bottom = capRect.bottom + S(48) },
            new[] { new TextRun(L.LessonsHome_KeySummary(state.Followed, state.Seen, state.Due)) }, pal);

        var note = new Win32.RECT { left = x0, top = band.bottom - S(10) - S(38), right = x1, bottom = band.bottom - S(10) };
        if (session.Exercise != null)
        {
            var button = new Win32.RECT { left = x0, top = note.top - S(4) - S(34), right = x0 + buttonW, bottom = note.top - S(4) };
            HomeButton(hdc, button, buttonText, L.LessonsHome_KeySessionName(cap), () => HomeStartKeySession(sc), primary: true, pal);
        }
        HomeParagraph(hdc, note, new[] { new TextRun(HomeKeySessionNote(session)) }, pal);

        // Une colonne par couche.
        // Autant de lignes que la bande en tient (titre, « puis la lettre », pied : S(84)), trois au moins.
        int maxLines = Math.Max(LessonsHomeLogic.DetailLines, (band.bottom - band.top - S(84)) / S(30));
        var layers = LessonsHomeLogic.KeyDetail(HomeKeyCatalog.Shared, snapshot.Entries, sc, snapshot.Now, maxLines);
        int colLeft = band.left + headW;
        int colW = (band.right - colLeft) / Math.Max(1, layers.Count);
        for (int i = 0; i < layers.Count; i++)
        {
            var col = new Win32.RECT { left = colLeft + i * colW, top = band.top, right = i == layers.Count - 1 ? band.right : colLeft + (i + 1) * colW, bottom = band.bottom };
            PaperDraw.Fill(hdc, col.left, band.top + 1, col.left + 1, band.bottom - 1, pal.Border);
            DrawHomeLayerColumn(hdc, col, pal, layers[i]);
        }
    }

    private void DrawHomeLayerColumn(IntPtr hdc, Win32.RECT col, in HomePalette pal, HomeLayerDetail layer)
    {
        int pad = S(14);
        int x0 = col.left + pad, x1 = col.right - pad;
        var small = HomeFont(AppFonts.Text, 12);

        // Titre : la touche morte armée, ou la couche ; puis le premier temps en abrégé.
        var headFont = HomeFont(AppFonts.Text, 14, TypeRamp.Semibold);
        string? deadKey = HomeDeadKeyOf(layer.Step);
        string name = deadKey != null ? HomeDeadKeyName(deadKey) : L.LessonsHome_LayerName(layer.Layer);
        var first = new[] { layer.Step };
        int gestureW = TablatureRenderer.DrawShortGesture(hdc, first, false, HomeScale, 0, 0, draw: false);
        string shown = HomeEllipsize(hdc, name, headFont, x1 - x0 - gestureW - S(8));
        int nameW = PaperText.DrawLine(hdc, shown, headFont,
            new Win32.RECT { left = x0, top = col.top + S(10), right = x1, bottom = col.top + S(32) }, pal.Ink, TextAlign.Left);
        if (x0 + nameW + S(8) + gestureW <= x1)
            TablatureRenderer.DrawShortGesture(hdc, first, false, HomeScale, x0 + nameW + S(8) + gestureW, col.top + S(21));

        int y = col.top + S(36);
        if (deadKey != null)
        {
            PaperText.DrawLine(hdc, L.LessonsHome_ThenLetter, small,
                new Win32.RECT { left = x0, top = y, right = x1, bottom = y + S(16) }, pal.Muted, TextAlign.Left);
            y += S(18);
        }
        y += S(2);

        var glyphFont = HomeFont(AppFonts.Title, 20, TypeRamp.Semibold);
        var lineFont = HomeFont(AppFonts.Text, 13);
        var lineBold = HomeFont(AppFonts.Text, 13, TypeRamp.Semibold);
        int rowH = S(30), notchW = S(5), notchH = S(10), notchGap = Math.Max(1, S(1));
        int gaugeW = GestureMastery.MaxBox * notchW + (GestureMastery.MaxBox - 1) * notchGap;
        PaperDraw.Fill(hdc, x0, y, x1, y + 1, pal.Border);
        foreach (var line in layer.Lines)
        {
            PaperText.DrawLine(hdc, HomeGlyph(line.Character), glyphFont,
                new Win32.RECT { left = x0, top = y, right = x0 + S(26), bottom = y + rowH }, pal.Ink, TextAlign.Center);
            int gx = x1 - gaugeW, gy = y + (rowH - notchH) / 2;
            for (int k = 0; k < GestureMastery.MaxBox; k++)
            {
                var notch = new Win32.RECT { left = gx + k * (notchW + notchGap), top = gy, right = gx + k * (notchW + notchGap) + notchW, bottom = gy + notchH };
                if (k < line.Box) PaperDraw.Fill(hdc, notch, pal.Action);
                else PaperDraw.Frame(hdc, notch, pal.Pale);
            }
            int textLeft = x0 + S(32), textRight = gx - S(6);
            int used = 0;
            if (line.Then.Length > 0)
                used = PaperText.DrawLine(hdc, HomeEllipsize(hdc, L.LessonsHome_Then(line.Then), lineFont, textRight - textLeft), lineFont,
                    new Win32.RECT { left = textLeft, top = y, right = textRight, bottom = y + rowH }, pal.Muted, TextAlign.Left);
            if (line.Due)
            {
                string tag = used == 0 ? L.LessonsHome_DueTag.TrimStart(' ', '·').TrimStart() : L.LessonsHome_DueTag;
                PaperText.DrawLine(hdc, HomeEllipsize(hdc, tag, lineBold, textRight - textLeft - used), lineBold,
                    new Win32.RECT { left = textLeft + used, top = y, right = textRight, bottom = y + rowH }, pal.Ink, TextAlign.Left);
            }
            y += rowH;
            PaperDraw.Fill(hdc, x0, y - 1, x1, y, pal.Border);
        }

        string more = layer.Hidden > 0 ? L.LessonsHome_LayerMore(layer.Hidden) : layer.Seen == 0 ? L.LessonsHome_LayerNoneTyped : "";
        if (more.Length > 0)
            PaperText.DrawLine(hdc, HomeEllipsize(hdc, more, small, x1 - x0), small,
                new Win32.RECT { left = x0, top = y + S(6), right = x1, bottom = y + S(22) }, pal.Muted, TextAlign.Left);
    }

    private string HomeMapSummary(HomeSnapshot snapshot, uint? scancode)
    {
        var lines = new List<string>
        {
            L.LessonsHome_MapSpoken(snapshot.Keys.Count(pair => pair.Value.Kind == HomeKeyKind.Seen),
                snapshot.Keys.Count, snapshot.Keys.Count(pair => pair.Value.Due > 0)),
        };
        if (scancode is { } sc && snapshot.Keys.TryGetValue(sc, out var state))
        {
            lines.Add(L.LessonsHome_KeyTitle(HomeKeyCap(sc)) + ". " + L.LessonsHome_KeySummary(state.Followed, state.Seen, state.Due));
            foreach (var layer in LessonsHomeLogic.KeyDetail(HomeKeyCatalog.Shared, snapshot.Entries, sc, snapshot.Now))
            {
                string? deadKey = HomeDeadKeyOf(layer.Step);
                lines.Add(deadKey != null ? HomeDeadKeyName(deadKey) : L.LessonsHome_LayerName(layer.Layer));
                foreach (var line in layer.Lines)
                    lines.Add(L.LessonsHome_KeyTooltipLine(HomeGlyph(line.Character), HomeLevelText(line.Box), line.Due));
                if (layer.Hidden > 0) lines.Add(L.LessonsHome_LayerMore(layer.Hidden));
            }
            lines.Add(HomeKeySessionNote(HomeKeySession(snapshot, sc, state)));
        }
        lines.Add(L.LessonsHome_MapKeysHelp);
        return string.Join("\n", lines);
    }

    // ── Résumé pour le lecteur d'écran ───────────────────────────────────

    private string HomeTodaySummary(HomeSnapshot snapshot)
    {
        var lines = new List<string> { L.LessonsHome_ReviewTitle };
        if (snapshot.Due.Count == 0)
            lines.Add(L.LessonsHome_ReviewNone);
        else
        {
            lines.Add(L.LessonsHome_ReviewCount(snapshot.Due.Count));
            foreach (var entry in snapshot.Due.Take(LessonsHomeLogic.ReviewCharacters))
            {
                var steps = Gesture.Of(entry.Character);
                string gesture = string.Join(" ", Gesture.Sentence(steps).Select(run => run.Text)).Trim();
                lines.Add(L.LessonsHome_DueSpoken(entry.Character, gesture));
            }
            if (snapshot.Review.Words.Count > 0)
                lines.Add(L.LessonsHome_ReviewWords(string.Join(", ", snapshot.Review.Words)));
        }
        var (module, lesson, exerciseIndex) = HomeResumePosition();
        lines.Add(L.LessonsHome_ResumeTitle);
        lines.Add(L.LessonsHome_SpokenResume(module.Title, lesson.Title, exerciseIndex + 1, lesson.Exercises.Count));
        lines.Add(L.LessonsHome_YourText);
        lines.Add(L.LessonsHome_MapSpoken(snapshot.Keys.Count(pair => pair.Value.Kind == HomeKeyKind.Seen),
            snapshot.Keys.Count, snapshot.Keys.Count(pair => pair.Value.Due > 0)));
        return string.Join("\n", lines);
    }
}
