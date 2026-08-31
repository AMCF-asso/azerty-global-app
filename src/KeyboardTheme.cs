namespace AZERTYGlobal;

/// <summary>
/// État d'une touche à un instant donné. L'ordre n'a pas d'importance : c'est
/// <see cref="KeyboardTheme.Paint"/> qui arbitre les cumuls, et un seul état sort.
/// </summary>
internal enum KeyState
{
    /// <summary>Touche ordinaire, rien ne la désigne.</summary>
    Rest,

    /// <summary>Curseur au-dessus, ou touche contextuelle désignée par la recherche.</summary>
    Hovered,

    /// <summary>Physiquement enfoncée, ou cible de l'exercice en cours.</summary>
    Pressed,

    /// <summary>Modifieur tenu ou verrou posé : Maj, AltGr, Ctrl, Alt, Verr. Maj.</summary>
    ModifierActive,

    /// <summary>La frappe attendue a été manquée sur cette touche.</summary>
    Error,

    /// <summary>Hors-jeu pendant un exercice — le retour arrière des leçons.</summary>
    Disabled,
}

/// <summary>
/// Ce qu'un surlignage dit d'une touche. C'est l'axe que la charte du 2026-08-28 n'avait pas :
/// ses catégories de méthode classent par modifieur (AltGr, Maj, accès direct), alors que les
/// trois claviers actuels classent par <em>rang dans la séquence</em> — une touche morte puis
/// le caractère. Les deux axes coexistent, et seul celui-ci colore le fond d'une touche.
/// </summary>
internal enum KeyHighlight
{
    None,

    /// <summary>Le caractère cherché s'obtient sur cette touche, en une frappe.</summary>
    Direct,

    /// <summary>Cette touche arme une touche morte — rien ne s'affiche encore.</summary>
    DeadKeyActivation,

    /// <summary>Première frappe d'une séquence de deux.</summary>
    Step1,

    /// <summary>Seconde frappe, celle qui produit le caractère.</summary>
    Step2,
}

/// <summary>
/// Les trois tables candidates de surlignage, rendues côte à côte par la planche
/// <c>KeyboardStatesBoard</c> pour l'arrêt visuel de CH4. Deux d'entre elles disparaissent
/// aussitôt qu'Antoine a tranché : garder un choix mort dans le produit, c'est garder une
/// couleur que personne ne peut plus expliquer.
/// </summary>
internal enum HighlightScheme
{
    /// <summary>
    /// Le rang seul décide : étape 1 en avertissement, étape 2 en action. L'accès direct, qui
    /// est aussi une frappe finale, retombe donc sur la même couleur que l'étape 2 — et
    /// l'activation de touche morte sur celle de l'étape 1. Deux collisions assumées.
    /// </summary>
    ParEtape,

    /// <summary>
    /// Trois rôles distincts : une frappe suffit (succès), la séquence commence
    /// (avertissement), la séquence se termine (action). L'activation de touche morte est le
    /// début d'une séquence, donc identique à l'étape 1 — une seule collision, et elle dit vrai.
    /// </summary>
    DirectVsSequence,

    /// <summary>
    /// Un seul rôle coloré — action — et le rang s'écrit en chiffre sur la touche. C'est la
    /// règle des fondations appliquée à la lettre : le texte porte l'information, la couleur
    /// renforce. Prix à payer : un chiffre de plus à lire sur une touche déjà chargée.
    /// </summary>
    RoleUniqueNumerote,
}

/// <summary>
/// Les cinq couleurs qui décrivent une touche. Séparée du rendu pour la même raison que
/// <see cref="ControlPaint"/> : la table s'éprouve sans fenêtre ni DC, état par état.
/// </summary>
/// <param name="Fill">Fond de la touche.</param>
/// <param name="Border">Contour, dessiné entièrement à l'intérieur.</param>
/// <param name="BorderWidth">Largeur du contour à 96 DPI : 1 ordinaire, 2 quand il porte un état.</param>
/// <param name="Label">Caractère principal, ou libellé d'une touche contextuelle.</param>
/// <param name="SubLabel">Sous-étiquettes : couches AltGr et Maj+AltGr, libellé bas.</param>
internal readonly record struct KeyPaint(uint Fill, uint Border, int BorderWidth, uint Label, uint SubLabel);

/// <summary>
/// Table de peinture des touches — le pendant de <see cref="ThemeControls"/> pour le clavier.
///
/// Elle existe parce que les trois claviers de l'application dessinent aujourd'hui le leur :
/// <c>KeyboardRenderer</c> (14 constantes, consommé par les seules Leçons), le clavier virtuel
/// (18) et le module d'essai (36), dont onze valeurs sont identiques à celles du renderer au
/// bit près — la trace d'un copier-coller, pas d'une décision.
///
/// ⛔ Aucune couleur ne s'écrit ici : tout sort de <see cref="Palette"/>. Une nuance absente de
/// la charte se demande, elle ne s'invente pas.
/// </summary>
static class KeyboardTheme
{
    /// <summary>Rayon d'une touche à 96 DPI. Même valeur que les contrôles.</summary>
    internal const int BaseRadius = ThemeControls.BaseRadius;

    /// <summary>Contour qui porte un état, en pixels à 96 DPI.</summary>
    internal const int StateBorderWidth = 2;

    /// <summary>
    /// Peinture d'une touche dans son état. Fonction pure — c'est elle que la suite éprouve.
    ///
    /// Un modifieur actif l'emporte sur le survol, et l'erreur sur tout le reste sauf
    /// l'inactivité : une touche hors-jeu ne peut pas être fautive.
    /// </summary>
    internal static KeyPaint Paint(KeyState state, Palette p) => state switch
    {
        KeyState.Rest => new(p.Surface, p.Border, 1, p.Ink, p.TextSecondary),
        KeyState.Hovered => new(p.ActionFill, p.Border, 1, p.OnActionFill, p.TextSecondary),
        KeyState.Pressed => new(p.ActionFill, p.Action, StateBorderWidth, p.OnActionFill, p.TextSecondary),
        KeyState.ModifierActive => new(p.Action, p.Action, 1, p.OnAction, p.OnAction),
        KeyState.Error => new(p.ErrorFill, p.Error, StateBorderWidth, p.Error, p.Error),
        KeyState.Disabled => new(p.Paper, p.Border, 1, p.Disabled, p.Disabled),
        _ => new(p.Surface, p.Border, 1, p.Ink, p.TextSecondary),
    };

    /// <summary>
    /// Peinture d'une touche surlignée, selon la table candidate demandée. Le contour porte
    /// toujours l'état : un surlignage qui ne tiendrait qu'au fond disparaîtrait en contraste
    /// élevé, où la palette entière bascule sur celle du système.
    /// </summary>
    internal static KeyPaint HighlightPaint(KeyHighlight highlight, Palette p, HighlightScheme scheme)
    {
        if (highlight == KeyHighlight.None)
            return Paint(KeyState.Rest, p);

        if (scheme == HighlightScheme.RoleUniqueNumerote)
            return Action(p);

        if (scheme == HighlightScheme.ParEtape)
        {
            return highlight switch
            {
                KeyHighlight.Step1 or KeyHighlight.DeadKeyActivation => Warning(p),
                _ => Action(p),
            };
        }

        return highlight switch
        {
            KeyHighlight.Direct => Success(p),
            KeyHighlight.Step1 or KeyHighlight.DeadKeyActivation => Warning(p),
            _ => Action(p),
        };
    }

    /// <summary>
    /// Vrai quand la table demandée écrit le rang de la frappe en chiffre sur la touche. Une
    /// seule table le fait, et c'est tout son propos.
    /// </summary>
    internal static bool ShowsRankBadge(HighlightScheme scheme) =>
        scheme == HighlightScheme.RoleUniqueNumerote;

    /// <summary>
    /// Largeur que la pastille de rang retire au caractère principal, marge comprise.
    /// Une seule table l'emploie, mais la mesure vaut pour toutes : c'est elle que
    /// <c>DrawKeyCap</c> reçoit en <c>labelLeftInset</c>.
    /// </summary>
    internal static int BadgeSize(int dpi) => ThemeControls.Scale(16, dpi);

    /// <summary>Rang à écrire dans le badge, ou 0 quand le surlignage n'en a pas.</summary>
    internal static int RankOf(KeyHighlight highlight) => highlight switch
    {
        KeyHighlight.Step1 or KeyHighlight.DeadKeyActivation => 1,
        KeyHighlight.Step2 => 2,
        _ => 0,
    };

    private static KeyPaint Action(Palette p) =>
        new(p.ActionFill, p.Action, StateBorderWidth, p.OnActionFill, p.TextSecondary);

    private static KeyPaint Warning(Palette p) =>
        new(p.WarningFill, p.Warning, StateBorderWidth, p.Ink, p.TextSecondary);

    private static KeyPaint Success(Palette p) =>
        new(p.SuccessFill, p.Success, StateBorderWidth, p.Ink, p.TextSecondary);

    /// <summary>
    /// Dessine une touche : fond, contour, caractère principal, sous-étiquette. Le contour est
    /// rentré de la moitié de sa largeur, GDI centrant le trait sur le chemin — sans quoi un
    /// contour de 2 px déborde d'un pixel et mord la touche voisine.
    /// </summary>
    internal static void DrawKeyCap(IntPtr hdc, Win32.RECT rect, KeyPaint paint,
        string label, string? subLabel, IntPtr labelFont, IntPtr subFont, int dpi,
        int labelLeftInset = 0)
    {
        int width = ThemeControls.Scale(paint.BorderWidth, dpi);
        int inset = width / 2;
        int radius = ThemeControls.Scale(BaseRadius, dpi) * 2;

        IntPtr brush = Theme.Brush(paint.Fill);
        IntPtr pen = Theme.Pen(paint.Border, width);
        IntPtr oldBrush = Win32.SelectObject(hdc, brush);
        IntPtr oldPen = Win32.SelectObject(hdc, pen);
        Win32.RoundRect(hdc, rect.left + inset, rect.top + inset,
            rect.right - inset, rect.bottom - inset, radius, radius);
        Win32.SelectObject(hdc, oldPen);
        Win32.SelectObject(hdc, oldBrush);

        Win32.SetBkMode(hdc, Win32.TRANSPARENT);

        var main = rect;
        if (!string.IsNullOrEmpty(subLabel))
            main.bottom -= (rect.bottom - rect.top) / 3;
        // La pastille de rang occupe le coin haut gauche : le caractère se centre
        // dans ce qui reste, sinon les deux se superposent.
        main.left += labelLeftInset;

        IntPtr oldFont = Win32.SelectObject(hdc, labelFont);
        Win32.SetTextColor(hdc, paint.Label);
        Win32.DrawTextW(hdc, label, label.Length, ref main,
            Win32.DT_CENTER | Win32.DT_VCENTER | Win32.DT_SINGLELINE | Win32.DT_NOPREFIX);

        if (!string.IsNullOrEmpty(subLabel))
        {
            var sub = rect;
            sub.top = main.bottom;
            sub.right -= ThemeControls.Scale(4, dpi);
            Win32.SelectObject(hdc, subFont);
            Win32.SetTextColor(hdc, paint.SubLabel);
            Win32.DrawTextW(hdc, subLabel, subLabel!.Length, ref sub,
                Win32.DT_RIGHT | Win32.DT_VCENTER | Win32.DT_SINGLELINE | Win32.DT_NOPREFIX);
        }

        Win32.SelectObject(hdc, oldFont);
    }

    /// <summary>
    /// Pastille chiffrée du rang, en haut à gauche de la touche. N'existe que pour la table
    /// <see cref="HighlightScheme.RoleUniqueNumerote"/>.
    /// </summary>
    internal static void DrawRankBadge(IntPtr hdc, Win32.RECT rect, int rank, Palette p,
        IntPtr font, int dpi)
    {
        if (rank <= 0)
            return;

        int size = BadgeSize(dpi);
        int pad = ThemeControls.Scale(3, dpi);
        var badge = new Win32.RECT
        {
            left = rect.left + pad,
            top = rect.top + pad,
            right = rect.left + pad + size,
            bottom = rect.top + pad + size,
        };

        IntPtr brush = Theme.Brush(p.Action);
        IntPtr pen = Theme.Pen(p.Action, 1);
        IntPtr oldBrush = Win32.SelectObject(hdc, brush);
        IntPtr oldPen = Win32.SelectObject(hdc, pen);
        Win32.Ellipse(hdc, badge.left, badge.top, badge.right, badge.bottom);
        Win32.SelectObject(hdc, oldPen);
        Win32.SelectObject(hdc, oldBrush);

        string text = rank.ToString(System.Globalization.CultureInfo.InvariantCulture);
        IntPtr oldFont = Win32.SelectObject(hdc, font);
        Win32.SetBkMode(hdc, Win32.TRANSPARENT);
        Win32.SetTextColor(hdc, p.OnAction);
        Win32.DrawTextW(hdc, text, text.Length, ref badge,
            Win32.DT_CENTER | Win32.DT_VCENTER | Win32.DT_SINGLELINE | Win32.DT_NOPREFIX);
        Win32.SelectObject(hdc, oldFont);
    }
}
