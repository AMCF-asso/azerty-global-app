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
        KeyState.Rest => new(p.KeyFace, p.Border, 1, p.Ink, p.TextSecondary),
        KeyState.Hovered => new(p.ActionFill, p.Border, 1, p.OnActionFill, p.TextSecondary),
        KeyState.Pressed => new(p.ActionFill, p.Action, StateBorderWidth, p.OnActionFill, p.TextSecondary),
        KeyState.ModifierActive => new(p.Action, p.Action, 1, p.OnAction, p.OnAction),
        KeyState.Error => new(p.ErrorFill, p.Error, StateBorderWidth, p.Error, p.Error),
        KeyState.Disabled => new(p.Paper, p.Border, 1, p.Disabled, p.Disabled),
        _ => new(p.KeyFace, p.Border, 1, p.Ink, p.TextSecondary),
    };

    /// <summary>
    /// Peinture d'une touche surlignée — table B « direct vs séquence », arrêtée par Antoine le
    /// 2026-09-02 (CH4a, S4-3) parmi trois candidates. Trois rôles : une frappe suffit (succès),
    /// la séquence commence (avertissement), la séquence se termine (action). L'armement d'une
    /// touche morte est le début d'une séquence, donc identique à l'étape 1 — une seule
    /// collision, et elle dit vrai. Le contour porte toujours l'état : un surlignage qui ne
    /// tiendrait qu'au fond disparaîtrait en contraste élevé.
    /// </summary>
    internal static KeyPaint HighlightPaint(KeyHighlight highlight, Palette p) => highlight switch
    {
        KeyHighlight.None => Paint(KeyState.Rest, p),
        KeyHighlight.Direct => Success(p),
        KeyHighlight.Step1 or KeyHighlight.DeadKeyActivation => Warning(p),
        _ => Action(p),
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
}
