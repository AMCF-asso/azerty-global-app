// Primitives de contrôle owner-draw — refonte graphique v1.2.0, chantier CH0.
//
// L'application mélange aujourd'hui deux générations de contrôles : des boutons à relief
// classiques, des cases et des radios système, des boutons « ▲ »/« ▼ » en guise de spinners,
// à côté de contrôles plats corrects dessinés à la main. Ce grand écart est le premier
// marqueur « non professionnel » relevé par l'audit du 2026-08-28. Ce fichier porte la
// seconde moitié : un seul jeu de primitives, que les fenêtres consomment de deux façons.
//
//   - Un contrôle enfant réel (BUTTON) passe en BS_OWNERDRAW, et le parent traite WM_DRAWITEM
//     en appelant la primitive avec le DC et le rectangle que Windows lui donne. C'est la voie
//     à préférer : elle garde l'ordre de tabulation, le focus clavier et les sémantiques de
//     clic du système, qu'un contrôle entièrement peint à la main perdrait tous les trois.
//   - Une zone dessinée dans le WM_PAINT du parent appelle la même primitive directement.
//
// Aucune de ces fonctions ne détruit ce qu'elle sélectionne : les brosses, stylos et polices
// viennent des caches de Theme, qui les garde vivants jusqu'à la fin du processus.
//
// Deux choses restent candidates jusqu'au chantier CH1, où elles s'arrêtent sur les fenêtres
// témoins, à l'échelle réelle du poste et sur les deux thèmes : la table d'états ci-dessous
// et les dimensions de base. Elles sont écrites ici pour être vues, pas pour être figées.

namespace AZERTYGlobal;

/// <summary>
/// État d'un contrôle au moment où on le peint. Combinable : un bouton peut être survolé et
/// avoir le focus. La précédence est fixée par les tables de peinture, pas par l'appelant.
/// </summary>
[Flags]
internal enum ControlState
{
    None = 0,

    /// <summary>Curseur au-dessus. Un contrôle désactivé ne montre jamais cet état.</summary>
    Hovered = 1,

    /// <summary>Bouton maintenu enfoncé.</summary>
    Pressed = 2,

    /// <summary>Focus clavier. Ajoute l'anneau, sans rien changer d'autre.</summary>
    Focused = 4,

    /// <summary>Inactif. L'emporte sur tous les autres états.</summary>
    Disabled = 8,

    /// <summary>Case cochée, radio sélectionnée.</summary>
    Checked = 16,
}

/// <summary>Poids visuel d'un bouton. Un écran n'a qu'un seul bouton primaire.</summary>
internal enum ButtonKind
{
    /// <summary>L'action que l'écran propose : fond plein à l'accent.</summary>
    Primary,

    /// <summary>Tous les autres : surface, bordure, texte à l'encre.</summary>
    Secondary,
}

/// <summary>
/// Les quatre couleurs qui décrivent un contrôle à un instant donné. Séparer cette table du
/// rendu la rend testable sans fenêtre ni DC : la suite éprouve les combinaisons état par état
/// et prouve qu'aucune ne fait sortir une couleur de la palette.
/// </summary>
/// <param name="Fill">Fond du contrôle.</param>
/// <param name="Border">Trait du contour, dessiné entièrement à l'intérieur du contrôle.</param>
/// <param name="BorderWidth">Largeur du trait en pixels à 96 DPI. 1 pour un contour ordinaire,
/// 2 pour un contour qui porte un état.</param>
/// <param name="Text">Texte et glyphes.</param>
internal readonly record struct ControlPaint(uint Fill, uint Border, int BorderWidth, uint Text);

/// <summary>
/// Tables d'états et rendu GDI des contrôles. Toutes les dimensions entrent en pixels à
/// 96 DPI et passent par <see cref="Scale"/> : l'application est PerMonitorV2, deux fenêtres
/// peuvent vivre au même instant sur deux écrans d'échelles différentes.
/// </summary>
static class ThemeControls
{
    // ═══════════════════════════════════════════════════════════════
    // Géométrie de la charte
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Rayon des contrôles. Les cartes et panneaux restent à 0.</summary>
    internal const int BaseRadius = 3;

    /// <summary>Anneau de focus : 2 px, à 2 px d'écart du contrôle. Il remplace le rectangle
    /// pointillé de Windows, que rien dans la charte ne peut colorer.</summary>
    internal const int BaseFocusRing = 2;
    internal const int BaseFocusGap = 2;

    /// <summary>Côté de la case et de la radio.</summary>
    internal const int BaseBoxSize = 16;

    /// <summary>Écart entre une case et son libellé — un cran de l'échelle 4/8/12/16/24/32/48.</summary>
    internal const int BaseBoxLabelGap = 8;

    /// <summary>Hauteur minimale d'un bouton, et son rembourrage horizontal.</summary>
    internal const int BaseButtonHeight = 32;
    internal const int BaseButtonPadding = 16;

    /// <summary>Rembourrage d'un onglet, et l'épaisseur du trait qui marque l'actif. Le trait
    /// vaut 2 px comme l'anneau de focus : à 1 px il disparaît sur un écran à 150 %, à 3 px il
    /// pèse plus que la bordure d'un bouton primaire.</summary>
    internal const int BaseTabPaddingX = 14;
    internal const int BaseTabPaddingY = 8;
    internal const int BaseTabUnderline = 2;

    /// <summary>
    /// Met une dimension de la charte à l'échelle d'un écran. Le minimum de 1 vaut pour les
    /// largeurs de trait : un trait arrondi à 0 disparaît, et une bordure absente est le seul
    /// cas où l'échelle changerait ce que le contrôle veut dire.
    /// </summary>
    internal static int Scale(int value, int dpi)
    {
        if (dpi <= 0)
            dpi = 96;

        int scaled = (int)Math.Round(value * dpi * Density / 96.0, MidpointRounding.AwayFromZero);
        return value > 0 ? Math.Max(1, scaled) : scaled;
    }

    /// <summary>
    /// Densité de l'application : un facteur global sur la **géométrie**, jamais sur le texte.
    ///
    /// Les polices viennent de <c>Theme.Font(role, dpi)</c>, qui ne passe pas par cette classe :
    /// baisser la densité resserre les marges, les hauteurs et l'anatomie des contrôles sans
    /// toucher à la taille des caractères. C'est la différence avec <c>ONBOARDING_UI_SCALE</c>,
    /// le facteur 0,75 par fenêtre supprimé le 2026-08-30, qui multipliait aussi les polices et
    /// rendait l'accueil illisible par rapport au reste de l'application.
    ///
    /// À 1, valeur par défaut, chaque expression du dépôt rend l'entier qu'elle rendait avant que
    /// ce champ existe.
    /// </summary>
    internal static float Density { get; private set; } = 0.90f;

    /// <summary>
    /// Force la densité jusqu'au <c>Dispose</c>, qui restaure la précédente. Sert au banc de
    /// captures, comme <c>ThemeWindow.OverrideDpiForTests</c> : rendre la matrice à plusieurs
    /// densités sans toucher aux réglages du poste.
    /// </summary>
    internal static IDisposable OverrideDensityForTests(float density)
    {
        var scope = new DensityScope(Density);
        Density = density <= 0 ? 1.0f : density;
        return scope;
    }

    private sealed class DensityScope : IDisposable
    {
        private readonly float _previous;
        private bool _disposed;

        internal DensityScope(float previous) => _previous = previous;

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            Density = _previous;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Tables d'états — pures
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Peinture d'un bouton. La précédence est désactivé, puis enfoncé, puis survolé : un
    /// contrôle inactif ne montre jamais de survol, et un bouton qu'on maintient enfoncé ne
    /// redevient pas « survolé » parce que le curseur n'a pas bougé.
    ///
    /// Le vocabulaire d'états est celui de la charte, et il n'emploie que des jetons existants :
    /// le fond passe de surface à action-fond puis à action, la bordure de texte-2 à action en
    /// 2 px. Le survol d'un bouton primaire est le seul cas qui ne pouvait pas suivre cette
    /// échelle, son fond étant déjà l'accent : il gagne un liseré intérieur de la couleur de
    /// son propre texte, garantie lisible sur ce fond par construction. [candidat — CH1]
    /// </summary>
    internal static ControlPaint ButtonPaint(ButtonKind kind, ControlState state, Palette p)
    {
        if (state.HasFlag(ControlState.Disabled))
            return new ControlPaint(p.Paper, p.Disabled, 1, p.Disabled);

        if (state.HasFlag(ControlState.Pressed))
            return new ControlPaint(p.ActionFill, p.Action, 2, p.OnActionFill);

        if (kind == ButtonKind.Primary)
        {
            return state.HasFlag(ControlState.Hovered)
                ? new ControlPaint(p.Action, p.OnAction, 2, p.OnAction)
                : new ControlPaint(p.Action, p.Action, 1, p.OnAction);
        }

        return state.HasFlag(ControlState.Hovered)
            ? new ControlPaint(p.ActionFill, p.TextSecondary, 1, p.OnActionFill)
            : new ControlPaint(p.Surface, p.TextSecondary, 1, p.Ink);
    }

    /// <summary>
    /// Peinture de la case d'une case à cocher ou d'une radio — la boîte seule, pas le libellé.
    /// Cochée, elle se remplit de l'accent et son glyphe prend la couleur du texte sur accent,
    /// exactement comme un bouton primaire.
    /// </summary>
    internal static ControlPaint BoxPaint(ControlState state, Palette p)
    {
        if (state.HasFlag(ControlState.Disabled))
            return new ControlPaint(p.Paper, p.Disabled, 1, p.Disabled);

        if (state.HasFlag(ControlState.Checked))
        {
            return state.HasFlag(ControlState.Hovered)
                ? new ControlPaint(p.Action, p.OnAction, 2, p.OnAction)
                : new ControlPaint(p.Action, p.Action, 1, p.OnAction);
        }

        return state.HasFlag(ControlState.Hovered)
            ? new ControlPaint(p.ActionFill, p.TextSecondary, 1, p.OnActionFill)
            : new ControlPaint(p.Surface, p.TextSecondary, 1, p.Ink);
    }

    /// <summary>
    /// Cadre d'un champ de saisie. Le contrôle EDIT reste système et peint son propre fond par
    /// WM_CTLCOLOREDIT ; le parent ne dessine que ce contour, et le focus l'épaissit à l'accent
    /// plutôt que de compter sur le curseur clignotant seul.
    /// </summary>
    internal static ControlPaint FieldPaint(ControlState state, Palette p)
    {
        if (state.HasFlag(ControlState.Disabled))
            return new ControlPaint(p.Paper, p.Disabled, 1, p.Disabled);

        return state.HasFlag(ControlState.Focused)
            ? new ControlPaint(p.Surface, p.Action, 2, p.Ink)
            : new ControlPaint(p.Surface, p.TextSecondary, 1, p.Ink);
    }

    /// <summary>Libellé posé à côté d'un contrôle, sur le fond de la fenêtre.</summary>
    internal static uint LabelColor(ControlState state, Palette p) =>
        state.HasFlag(ControlState.Disabled) ? p.Disabled : p.Ink;

    /// <summary>
    /// Couleur d'un lien. Le survol ne change pas la couleur mais ajoute le soulignement : un
    /// lien qui change de teinte au survol demanderait une seconde nuance d'accent, que la
    /// charte n'a pas — et c'est précisément d'un survol de lien qu'est né l'orange fantôme de
    /// l'application actuelle.
    /// </summary>
    internal static uint LinkColor(ControlState state, Palette p) =>
        state.HasFlag(ControlState.Disabled) ? p.Disabled : p.Action;

    // ═══════════════════════════════════════════════════════════════
    // Rendu
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Bouton complet : fond, contour, libellé centré, et l'anneau de focus quand il l'a.
    /// <paramref name="rect"/> est le rectangle du contrôle lui-même ; l'anneau déborde de
    /// 4 px autour, que la mise en page doit avoir réservés.
    /// </summary>
    internal static void DrawButton(IntPtr hdc, Win32.RECT rect, string text, IntPtr font,
        ButtonKind kind, ControlState state, Palette palette, int dpi)
    {
        var paint = ButtonPaint(kind, state, palette);
        DrawRoundedBox(hdc, rect, paint, Scale(BaseRadius, dpi), dpi);
        DrawCenteredText(hdc, rect, text, font, paint.Text);

        if (state.HasFlag(ControlState.Focused) && !state.HasFlag(ControlState.Disabled))
            DrawFocusRing(hdc, rect, palette, dpi);
    }

    /// <summary>Case à cocher : la boîte, sa coche, puis le libellé à droite.</summary>
    internal static void DrawCheckBox(IntPtr hdc, Win32.RECT rect, string text, IntPtr font,
        ControlState state, Palette palette, int dpi)
    {
        var box = BoxRect(rect, dpi);
        var paint = BoxPaint(state, palette);
        DrawRoundedBox(hdc, box, paint, Scale(BaseRadius, dpi), dpi);

        if (state.HasFlag(ControlState.Checked))
            DrawCheckMark(hdc, box, paint.Text, dpi);

        DrawBoxLabel(hdc, rect, box, text, font, LabelColor(state, palette), dpi);

        if (state.HasFlag(ControlState.Focused) && !state.HasFlag(ControlState.Disabled))
            DrawFocusRing(hdc, rect, palette, dpi);
    }

    /// <summary>Radio : même anatomie que la case, en rond.</summary>
    internal static void DrawRadio(IntPtr hdc, Win32.RECT rect, string text, IntPtr font,
        ControlState state, Palette palette, int dpi)
    {
        var box = BoxRect(rect, dpi);
        var paint = BoxPaint(state, palette);
        DrawEllipse(hdc, box, paint, dpi);

        if (state.HasFlag(ControlState.Checked))
        {
            int inset = (box.right - box.left) / 4;
            var dot = new Win32.RECT
            {
                left = box.left + inset,
                top = box.top + inset,
                right = box.right - inset,
                bottom = box.bottom - inset,
            };
            DrawEllipse(hdc, dot, new ControlPaint(paint.Text, paint.Text, 1, paint.Text), dpi);
        }

        DrawBoxLabel(hdc, rect, box, text, font, LabelColor(state, palette), dpi);

        if (state.HasFlag(ControlState.Focused) && !state.HasFlag(ControlState.Disabled))
            DrawFocusRing(hdc, rect, palette, dpi);
    }

    /// <summary>
    /// Paire d'incrément et de décrément, qui remplace les deux boutons système « ▲ »/« ▼ » du
    /// dialogue de durée de pause — le contrôle le plus daté de l'application. Les deux moitiés
    /// portent leur propre état : on peut survoler l'une sans l'autre.
    /// </summary>
    internal static void DrawSpinner(IntPtr hdc, Win32.RECT rect, ControlState up,
        ControlState down, Palette palette, int dpi)
    {
        int middle = rect.top + (rect.bottom - rect.top) / 2;
        var upper = new Win32.RECT { left = rect.left, top = rect.top, right = rect.right, bottom = middle };
        var lower = new Win32.RECT { left = rect.left, top = middle, right = rect.right, bottom = rect.bottom };

        DrawSpinnerButton(hdc, upper, up, palette, dpi, pointingUp: true);
        DrawSpinnerButton(hdc, lower, down, palette, dpi, pointingUp: false);
    }

    /// <summary>
    /// Lien. Le soulignement est un filet plein sous la ligne de base, et non l'attribut
    /// souligné d'une police : la même police sert alors au lien et au texte courant, ce qui
    /// évite une entrée de plus dans le cache pour un trait d'un pixel.
    /// </summary>
    internal static void DrawLink(IntPtr hdc, Win32.RECT rect, string text, IntPtr font,
        ControlState state, Palette palette, int dpi)
    {
        uint color = LinkColor(state, palette);
        Win32.SetBkMode(hdc, Win32.TRANSPARENT);
        Win32.SelectObject(hdc, font);
        Win32.SetTextColor(hdc, color);

        var textRect = rect;
        Win32.DrawTextW(hdc, text, -1, ref textRect,
            Win32.DT_LEFT | Win32.DT_SINGLELINE | Win32.DT_VCENTER | Win32.DT_NOPREFIX);

        if (state.HasFlag(ControlState.Hovered) && !state.HasFlag(ControlState.Disabled))
        {
            int width = GdiHelpers.MeasureSingleLineWidth(hdc, font, text);
            int lineHeight = GdiHelpers.MeasureSingleLineHeight(hdc, font);
            int baseline = rect.top + ((rect.bottom - rect.top) + lineHeight) / 2;
            var underline = new Win32.RECT
            {
                left = rect.left,
                top = baseline,
                right = Math.Min(rect.right, rect.left + width),
                bottom = baseline + Scale(1, dpi),
            };
            Win32.FillRect(hdc, ref underline, Theme.Brush(color));
        }

        if (state.HasFlag(ControlState.Focused) && !state.HasFlag(ControlState.Disabled))
            DrawFocusRing(hdc, rect, palette, dpi);
    }

    /// <summary>
    /// Contour d'un champ de saisie système. Dessiné autour du rectangle de l'EDIT et non
    /// dedans : le contrôle peint tout son client, et un trait posé à l'intérieur serait
    /// effacé à la première frappe.
    /// </summary>
    internal static void DrawFieldFrame(IntPtr hdc, Win32.RECT editRect, ControlState state,
        Palette palette, int dpi)
    {
        var paint = FieldPaint(state, palette);
        DrawOutline(hdc, FieldFrameRect(editRect, Scale(paint.BorderWidth, dpi)),
            paint.Border, Scale(paint.BorderWidth, dpi), Scale(BaseRadius, dpi));
    }

    /// <summary>
    /// Rectangle du cadre d'un champ. Il faut un pixel de plus à droite et en bas que la simple
    /// symétrie ne le suggère : <see cref="DrawOutline"/> trace son contour à
    /// <c>right - 1</c> et <c>bottom - 1</c>, si bien qu'un cadre posé sur
    /// <c>editRect ± width</c> ramenait ces deux traits sur la frontière du contrôle, où
    /// WS_CLIPCHILDREN les écrête. Mesuré le 2026-08-29 sur Couches maintenables : bord haut
    /// 89 px, bord gauche 34 px, bords droit et bas absents.
    /// </summary>
    internal static Win32.RECT FieldFrameRect(Win32.RECT editRect, int width) => new()
    {
        left = editRect.left - width,
        top = editRect.top - width,
        right = editRect.right + width + 1,
        bottom = editRect.bottom + width + 1,
    };

    /// <summary>
    /// Anneau de focus : 2 px d'accent, à 2 px d'écart du contrôle. Il remplace le rectangle
    /// pointillé de Windows, que rien dans la charte ne sait colorer, et il ne se dessine
    /// jamais sur un contrôle désactivé.
    /// </summary>
    internal static void DrawFocusRing(IntPtr hdc, Win32.RECT rect, Palette palette, int dpi)
    {
        int gap = Scale(BaseFocusGap, dpi);
        int width = Scale(BaseFocusRing, dpi);
        var ring = new Win32.RECT
        {
            left = rect.left - gap - width,
            top = rect.top - gap - width,
            right = rect.right + gap + width,
            bottom = rect.bottom + gap + width,
        };

        DrawOutline(hdc, ring, palette.Action, width, Scale(BaseRadius + BaseFocusGap, dpi));
    }

    // ═══════════════════════════════════════════════════════════════
    // Mesures
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Largeur qu'un bouton doit avoir pour porter son libellé sans le tronquer.</summary>
    internal static int MeasureButtonWidth(IntPtr hdc, IntPtr font, string text, int dpi) =>
        GdiHelpers.MeasureSingleLineWidth(hdc, font, text) + 2 * Scale(BaseButtonPadding, dpi);

    /// <summary>Hauteur d'un bouton : le plus grand entre la hauteur de charte et le texte
    /// plus son rembourrage, pour qu'une échelle de police plus grande ne le fasse pas
    /// déborder.</summary>
    internal static int MeasureButtonHeight(IntPtr hdc, IntPtr font, int dpi) =>
        Math.Max(Scale(BaseButtonHeight, dpi),
            GdiHelpers.MeasureSingleLineHeight(hdc, font) + Scale(BaseButtonPadding, dpi));

    /// <summary>
    /// Un onglet. Quatrième forme de contrôle de la charte, et aucune couleur de plus :
    ///
    ///   - actif : fond de surface, encre, et un trait d'accent sur toute sa largeur en bas ;
    ///   - inactif : fond de papier, texte secondaire, pas de trait ;
    ///   - survolé et inactif : le fond passe à action-fond, la même transition qu'un bouton
    ///     secondaire survolé, pour que le clic se devine avant d'être tenté ;
    ///   - désactivé : texte à <c>Disabled</c>, jamais de survol.
    ///
    /// L'anneau de focus est celui de tous les autres contrôles : la barre d'onglets se
    /// parcourt au clavier comme le reste de la fenêtre.
    /// </summary>
    internal static void DrawTab(IntPtr hdc, Win32.RECT rect, string text, IntPtr font,
        bool active, ControlState state, Palette palette, int dpi)
    {
        bool disabled = state.HasFlag(ControlState.Disabled);
        uint fill = active ? palette.Surface
            : (!disabled && state.HasFlag(ControlState.Hovered)) ? palette.ActionFill
            : palette.Paper;
        uint ink = disabled ? palette.Disabled
            : active ? palette.Ink
            : palette.TextSecondary;

        GdiHelpers.FillSolidRect(hdc, rect, fill);
        DrawCenteredText(hdc, rect, text, font, ink);

        if (active)
        {
            int thickness = Scale(BaseTabUnderline, dpi);
            GdiHelpers.FillSolidRect(hdc, new Win32.RECT
            {
                left = rect.left,
                top = rect.bottom - thickness,
                right = rect.right,
                bottom = rect.bottom,
            }, disabled ? palette.Disabled : palette.Action);
        }

        if (state.HasFlag(ControlState.Focused) && !disabled)
        {
            int inset = FocusMargin(dpi);
            DrawFocusRing(hdc, new Win32.RECT
            {
                left = rect.left + inset,
                top = rect.top + inset,
                right = rect.right - inset,
                bottom = rect.bottom - inset,
            }, palette, dpi);
        }
    }

    /// <summary>Largeur d'un onglet : son libellé et son rembourrage. Les onglets ne sont pas
    /// à largeur égale — un intitulé court n'a aucune raison d'occuper la place du plus
    /// long.</summary>
    internal static int MeasureTabWidth(IntPtr hdc, IntPtr font, string text, int dpi) =>
        GdiHelpers.MeasureSingleLineWidth(hdc, font, text) + 2 * Scale(BaseTabPaddingX, dpi);

    /// <summary>Hauteur d'une barre d'onglets, trait de l'actif compris.</summary>
    internal static int MeasureTabHeight(IntPtr hdc, IntPtr font, int dpi) =>
        GdiHelpers.MeasureSingleLineHeight(hdc, font) + 2 * Scale(BaseTabPaddingY, dpi)
            + Scale(BaseTabUnderline, dpi);

    /// <summary>Largeur qu'une case ou une radio doit avoir pour porter son libellé sans le
    /// tronquer : l'anneau de focus des deux côtés, la boîte, son écart au libellé, et le texte.
    /// Compagnon de <see cref="MeasureButtonWidth"/>. Sans elle, une mise en page ne peut pas se
    /// dimensionner sur ses cases et retombe sur une constante, qui ment dès que la police ou la
    /// langue change — c'est exactement ce qui a tronqué quatre libellés de Paramètres.</summary>
    internal static int MeasureBoxRowWidth(IntPtr hdc, IntPtr font, string text, int dpi) =>
        2 * FocusMargin(dpi) + Scale(BaseBoxSize, dpi) + Scale(BaseBoxLabelGap, dpi)
            + GdiHelpers.MeasureSingleLineWidth(hdc, font, text);

    /// <summary>Place que l'anneau de focus réclame de chaque côté d'un contrôle. La mise en
    /// page doit la réserver, sinon l'anneau mord sur le voisin.</summary>
    internal static int FocusMargin(int dpi) => Scale(BaseFocusGap, dpi) + Scale(BaseFocusRing, dpi);

    // ═══════════════════════════════════════════════════════════════
    // Rendu — pièces communes
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Rectangle arrondi plein avec son contour. GDI centre le trait sur le chemin : sans le
    /// retrait d'une demi-largeur, la moitié extérieure d'un trait de 2 px déborderait du
    /// contrôle et mordrait sur le fond de la fenêtre.
    /// </summary>
    private static void DrawRoundedBox(IntPtr hdc, Win32.RECT rect, ControlPaint paint,
        int radius, int dpi)
    {
        int width = Scale(paint.BorderWidth, dpi);
        int inset = width / 2;
        var brush = Theme.Brush(paint.Fill);
        var pen = Theme.Pen(paint.Border, width);
        var oldBrush = Win32.SelectObject(hdc, brush);
        var oldPen = Win32.SelectObject(hdc, pen);

        Win32.RoundRect(hdc, rect.left + inset, rect.top + inset,
            rect.right - inset - 1, rect.bottom - inset - 1, radius, radius);

        Win32.SelectObject(hdc, oldPen);
        Win32.SelectObject(hdc, oldBrush);
    }

    private static void DrawEllipse(IntPtr hdc, Win32.RECT rect, ControlPaint paint, int dpi)
    {
        int width = Scale(paint.BorderWidth, dpi);
        int inset = width / 2;
        var oldBrush = Win32.SelectObject(hdc, Theme.Brush(paint.Fill));
        var oldPen = Win32.SelectObject(hdc, Theme.Pen(paint.Border, width));

        Win32.Ellipse(hdc, rect.left + inset, rect.top + inset,
            rect.right - inset - 1, rect.bottom - inset - 1);

        Win32.SelectObject(hdc, oldPen);
        Win32.SelectObject(hdc, oldBrush);
    }

    /// <summary>Contour seul, sans fond — anneau de focus et cadre de champ.</summary>
    private static void DrawOutline(IntPtr hdc, Win32.RECT rect, uint color, int width, int radius)
    {
        int inset = width / 2;
        var oldBrush = Win32.SelectObject(hdc, Win32.GetStockObject(Win32.NULL_BRUSH));
        var oldPen = Win32.SelectObject(hdc, Theme.Pen(color, width));

        Win32.RoundRect(hdc, rect.left + inset, rect.top + inset,
            rect.right - inset - 1, rect.bottom - inset - 1, radius, radius);

        Win32.SelectObject(hdc, oldPen);
        Win32.SelectObject(hdc, oldBrush);
    }

    private static void DrawCenteredText(IntPtr hdc, Win32.RECT rect, string text, IntPtr font,
        uint color)
    {
        if (string.IsNullOrEmpty(text))
            return;

        Win32.SetBkMode(hdc, Win32.TRANSPARENT);
        Win32.SelectObject(hdc, font);
        Win32.SetTextColor(hdc, color);

        var textRect = rect;
        Win32.DrawTextW(hdc, text, -1, ref textRect,
            Win32.DT_CENTER | Win32.DT_VCENTER | Win32.DT_SINGLELINE | Win32.DT_NOPREFIX
            | Win32.DT_END_ELLIPSIS);
    }

    /// <summary>Case ou radio : un carré de côté fixe, centré verticalement, calé à gauche.</summary>
    private static Win32.RECT BoxRect(Win32.RECT rect, int dpi)
    {
        int size = Scale(BaseBoxSize, dpi);
        int top = rect.top + ((rect.bottom - rect.top) - size) / 2;
        return new Win32.RECT
        {
            left = rect.left,
            top = top,
            right = rect.left + size,
            bottom = top + size,
        };
    }

    private static void DrawBoxLabel(IntPtr hdc, Win32.RECT rect, Win32.RECT box, string text,
        IntPtr font, uint color, int dpi)
    {
        if (string.IsNullOrEmpty(text))
            return;

        Win32.SetBkMode(hdc, Win32.TRANSPARENT);
        Win32.SelectObject(hdc, font);
        Win32.SetTextColor(hdc, color);

        var labelRect = new Win32.RECT
        {
            left = box.right + Scale(BaseBoxLabelGap, dpi),
            top = rect.top,
            right = rect.right,
            bottom = rect.bottom,
        };
        Win32.DrawTextW(hdc, text, -1, ref labelRect,
            Win32.DT_LEFT | Win32.DT_VCENTER | Win32.DT_SINGLELINE | Win32.DT_NOPREFIX
            | Win32.DT_END_ELLIPSIS);
    }

    /// <summary>Coche en deux segments, tracée au stylo plutôt qu'en caractère : le glyphe
    /// d'une police varie d'une machine à l'autre, le tracé non.</summary>
    private static void DrawCheckMark(IntPtr hdc, Win32.RECT box, uint color, int dpi)
    {
        int size = box.right - box.left;
        int width = Math.Max(1, Scale(2, dpi));
        var oldPen = Win32.SelectObject(hdc, Theme.Pen(color, width));

        int x1 = box.left + size / 4;
        int y1 = box.top + size / 2;
        int x2 = box.left + size * 7 / 16;
        int y2 = box.bottom - size * 5 / 16;
        int x3 = box.right - size / 4;
        int y3 = box.top + size * 5 / 16;

        Win32.MoveToEx(hdc, x1, y1, IntPtr.Zero);
        Win32.LineTo(hdc, x2, y2);
        Win32.LineTo(hdc, x3, y3);

        Win32.SelectObject(hdc, oldPen);
    }

    /// <summary>
    /// Une moitié de compteur. Publique parce que certaines fenêtres portent leurs deux
    /// flèches comme deux contrôles distincts, chacun avec son propre état de survol : le
    /// dialogue de durée de pause en est le cas.
    /// </summary>
    internal static void DrawSpinnerButton(IntPtr hdc, Win32.RECT rect, ControlState state,
        Palette palette, int dpi, bool pointingUp)
    {
        var paint = ButtonPaint(ButtonKind.Secondary, state, palette);
        DrawRoundedBox(hdc, rect, paint, Scale(BaseRadius, dpi), dpi);

        int width = rect.right - rect.left;
        int height = rect.bottom - rect.top;
        int half = Math.Max(2, Math.Min(width, height) / 4);
        int centerX = rect.left + width / 2;
        int centerY = rect.top + height / 2;
        int offset = half / 2;

        var points = pointingUp
            ? new[]
            {
                new Win32.POINT { x = centerX - half, y = centerY + offset },
                new Win32.POINT { x = centerX + half, y = centerY + offset },
                new Win32.POINT { x = centerX, y = centerY - offset - half / 2 },
            }
            : new[]
            {
                new Win32.POINT { x = centerX - half, y = centerY - offset },
                new Win32.POINT { x = centerX + half, y = centerY - offset },
                new Win32.POINT { x = centerX, y = centerY + offset + half / 2 },
            };

        var oldBrush = Win32.SelectObject(hdc, Theme.Brush(paint.Text));
        var oldPen = Win32.SelectObject(hdc, Theme.Pen(paint.Text, 1));
        Win32.Polygon(hdc, points, points.Length);
        Win32.SelectObject(hdc, oldPen);
        Win32.SelectObject(hdc, oldBrush);
    }

    /// <summary>
    /// Compteur d'une valeur : un trait pour retrancher, une croix pour ajouter. Il remplace la
    /// paire de flèches empilées, qui ne pouvait pas dépasser la moitié de la hauteur de son
    /// champ — 26 × 14 px à 96 DPI, là où Windows demande 24 × 24 sous la souris. À pleine
    /// hauteur, de part et d'autre du champ, la même fenêtre offre quatre fois la cible, et
    /// « moins » et « plus » disent ce que le bouton fait d'une durée quand « haut » et « bas »
    /// décrivaient un déplacement dans une liste.
    /// </summary>
    internal static void DrawStepperButton(IntPtr hdc, Win32.RECT rect, ControlState state,
        Palette palette, int dpi, bool adding)
    {
        var paint = ButtonPaint(ButtonKind.Secondary, state, palette);
        DrawRoundedBox(hdc, rect, paint, Scale(BaseRadius, dpi), dpi);

        // Un compteur est WS_TABSTOP : sans anneau, on tabule dessus sans voir où l'on est.
        // Les flèches empilées qu'il remplace n'en portaient pas non plus.
        if (state.HasFlag(ControlState.Focused) && !state.HasFlag(ControlState.Disabled))
            DrawFocusRing(hdc, rect, palette, dpi);

        int arm = Math.Max(3, Math.Min(rect.right - rect.left, rect.bottom - rect.top) / 4);
        int centerX = rect.left + (rect.right - rect.left) / 2;
        int centerY = rect.top + (rect.bottom - rect.top) / 2;
        int thickness = Math.Max(1, Scale(2, dpi));
        IntPtr ink = Theme.Brush(paint.Text);

        var bar = new Win32.RECT
        {
            left = centerX - arm,
            top = centerY - thickness / 2,
            right = centerX + arm,
            bottom = centerY - thickness / 2 + thickness,
        };
        Win32.FillRect(hdc, ref bar, ink);

        if (!adding)
            return;

        var stem = new Win32.RECT
        {
            left = centerX - thickness / 2,
            top = centerY - arm,
            right = centerX - thickness / 2 + thickness,
            bottom = centerY + arm,
        };
        Win32.FillRect(hdc, ref stem, ink);
    }
}
