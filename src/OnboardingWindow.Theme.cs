using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace AZERTYGlobal;

/// <summary>
/// Contrôles de la fenêtre d'accueil peints à la charte — reste de CH3 de la refonte v1.2.0.
///
/// La passe 2 avait mis Onboarding à la charte pour la couleur et la typographie, mais ses trois
/// boutons de navigation et ses trois cases de l'étape 3 restaient peints par Windows :
/// **1,44 %** de pixels système mesurés sur ses 18 captures, contre 0,12 % pour Paramètres à la
/// même date. Ce fichier est à Onboarding ce que <c>SettingsWindow.Theme.cs</c> est à Paramètres,
/// et il en reprend les trois conséquences :
///
///   1. <c>BS_AUTOCHECKBOX</c> disparaît — l'état vit dans <see cref="_checkedState"/>, et
///      <see cref="SetCheck"/> / <see cref="GetCheck"/> remplacent <c>BM_SETCHECK</c> et
///      <c>BM_GETCHECK</c> partout dans la fenêtre.
///   2. Un clic ne bascule plus rien tout seul : les trois cases gagnent un cas dans
///      <c>WM_COMMAND</c>, alors que deux d'entre elles n'étaient lues qu'à la fermeture.
///   3. Le survol n'est pas rapporté dans <c>DRAWITEMSTRUCT.itemState</c> : il se piste par
///      sous-classement et <c>TrackMouseEvent</c>.
///
/// Une quatrième conséquence est propre à cette fenêtre : <c>BS_DEFPUSHBUTTON</c> quitte
/// « Suivant » avec le style système. Il ne coûte rien — la fenêtre n'a pas de boucle
/// <c>IsDialogMessage</c>, donc ce drapeau n'a jamais fait qu'épaissir une bordure, et Entrée n'y
/// a jamais validé quoi que ce soit.
///
/// ⚠️ Ce que la migration retire aussi, ici comme dans Paramètres : un contrôle owner-draw ne
/// publie plus son état coché aux outils d'accessibilité. Le libellé reste exposé, l'état non.
/// </summary>
sealed partial class OnboardingWindow
{
    // ═══════════════════════════════════════════════════════════════
    // L'état que Windows ne tient plus
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Case cochée. Indexée par handle plutôt que par identifiant de contrôle : c'est le handle
    /// que <c>DRAWITEMSTRUCT</c> apporte.
    /// </summary>
    private readonly Dictionary<IntPtr, bool> _checkedState = new();

    /// <summary>Contrôle sous le curseur, ou <c>IntPtr.Zero</c>.</summary>
    private IntPtr _hoveredControl;

    private Win32.SUBCLASSPROC? _hoverSubclassProc;

    /// <summary>
    /// Vrai quand « Essayer maintenant » est à l'écran, ce qui n'arrive qu'à l'étape 1 et
    /// seulement tant que les exercices ne sont pas complétés. C'est l'unique entrée de
    /// <see cref="KindOf"/> : suivi ici plutôt que lu par <c>IsWindowVisible</c>, qui rend faux
    /// pour tout enfant d'une fenêtre encore masquée — le cas de chaque rendu du banc.
    /// </summary>
    private bool _tryButtonShown;

    /// <summary>
    /// Les six contrôles que cette fenêtre peint elle-même, dans un ordre stable : l'indice sert
    /// d'identifiant de sous-classe, et le détachement doit retrouver le même. Le décalage de 100
    /// les tient à l'écart des identifiants déjà posés sur ces mêmes boutons par
    /// <c>_buttonArrowSubclassProc</c> (20 à 22) et sur les liens par <c>_linkSubclassProc</c>.
    /// </summary>
    private IntPtr[] OwnerDrawControls => new[]
    {
        _hWndBtnNext, _hWndBtnPrev, _hWndBtnTry,
        _hWndChkAutoStart, _hWndChkDontShow, _hWndChkTraining,
    };

    private const uint HoverSubclassIdBase = 100;

    private bool GetCheck(IntPtr control) =>
        control != IntPtr.Zero && _checkedState.TryGetValue(control, out bool value) && value;

    private void SetCheck(IntPtr control, bool value)
    {
        if (control == IntPtr.Zero)
            return;
        if (_checkedState.TryGetValue(control, out bool current) && current == value)
            return;

        _checkedState[control] = value;
        Win32.InvalidateRect(control, IntPtr.Zero, true);
    }

    private void ToggleCheck(IntPtr control) => SetCheck(control, !GetCheck(control));

    /// <summary>
    /// Rafraîchit les six contrôles que la fenêtre peint elle-même.
    ///
    /// <c>InvalidateRect</c> sur la fenêtre n'atteint pas ses enfants — et depuis que
    /// <c>WS_CLIPCHILDREN</c> est posé, la peinture du parent ne passe même plus dessus. Deux
    /// changements les concernent tous et ne viennent d'aucun d'eux : le passage d'une étape à
    /// l'autre, qui déplace l'accent d'un bouton à l'autre, et la bascule de thème de Windows,
    /// qui change chaque jeton.
    /// </summary>
    private void InvalidateOwnerDrawControls()
    {
        foreach (var control in OwnerDrawControls)
        {
            if (control != IntPtr.Zero)
                Win32.InvalidateRect(control, IntPtr.Zero, true);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Anatomie des boutons
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Hauteur du contrôle, marge de focus comprise. L'anneau se dessine à l'extérieur du bouton
    /// et <see cref="TryDrawItem"/> rend donc son contenu dans un rectangle rentré d'autant :
    /// sans cette réserve le libellé serait amputé, le défaut mesuré sur Paramètres le même jour.
    /// Les quatre <c>MoveWindow</c> de la barre de navigation la partagent, faute de quoi les
    /// boutons cesseraient d'être alignés entre eux.
    /// </summary>
    private int ButtonRowHeight() => S(BASE_BTN_H) + ThemeControls.FocusMargin(_dpi) * 2;

    /// <summary>
    /// Largeur **dessinée** du bouton pour ce libellé — celle qu'il paraît occuper, sans la marge
    /// de focus. <paramref name="minBase"/> est son plancher de charte, en unités 96 DPI.
    ///
    /// La largeur de « Précédent » cesse ici d'être une constante : elle valait 120 px pour une
    /// police rendue à 75 % de sa taille, et ce facteur est parti avec <c>bea44be</c>.
    /// </summary>
    private int ButtonRowWidth(string text, int minBase)
    {
        IntPtr hdc = Win32.GetDC(_hWnd);
        try
        {
            int textWidth = MeasureSingleLineWidth(hdc, _hFontButton, text);
            return Math.Max(S(minBase), textWidth + S(BASE_BTN_TEXT_PAD * 2));
        }
        finally
        {
            Win32.ReleaseDC(_hWnd, hdc);
        }
    }

    /// <summary>
    /// Place un bouton par la géométrie qu'il doit **paraître** occuper : le contrôle est décalé
    /// de la marge de focus sur les quatre côtés, et agrandi du double.
    ///
    /// Sans ce décalage, réserver la marge de focus déplacerait ce que l'œil voit. Mesuré en
    /// unités 96 DPI : « Suivant » se serait aligné 4 px en deçà de la marge de 28 px sur
    /// laquelle tout le reste de la fenêtre est aligné, et l'écart voulu de 12 px entre
    /// « Essayer maintenant » et lui serait devenu 20. La barre de navigation est le seul endroit
    /// de l'application où des contrôles owner-draw sont alignés sur une marge de fenêtre ; les
    /// cases de l'étape 3, posées dans un panneau et empilées entre elles, n'en ont pas besoin.
    /// </summary>
    private void MoveButton(IntPtr button, int x, int y, int paintedWidth)
    {
        int focus = ThemeControls.FocusMargin(_dpi);
        Win32.MoveWindow(button, x - focus, y - focus,
            paintedWidth + focus * 2, S(BASE_BTN_H) + focus * 2, true);
    }

    // ═══════════════════════════════════════════════════════════════
    // Anatomie d'une carte
    // ═══════════════════════════════════════════════════════════════
    //
    // Les trois peintres de carte — DrawStepCard, DrawStepCardWithRuns, DrawToggleStepCard —
    // portaient chacun leur copie de ces quatre valeurs, toutes en dur : titre de 24 px, pastille
    // de 34 × 24, interligne de 22, et un plancher de carte de 73 ou 78. Mesuré le 2026-08-30 :
    // ce plancher **était** la hauteur d'Onboarding. Cinq cartes à 73 font 365 px sur 724, et
    // c'est pourquoi réduire la taille du texte ne changeait rien — les cartes restaient à leur
    // plancher, avec juste plus de vide dedans.
    //
    // Elles se mesurent désormais sur les polices qu'elles rendent. Le seul plancher qui reste
    // est la pastille : une carte ne peut pas être plus courte que le numéro qu'elle porte.

    /// <summary>Hauteur de la ligne de titre d'une carte.</summary>
    private int CardTitleHeight(IntPtr hdc) => MeasureSingleLineHeight(hdc, _hFontBold) + S(4);

    /// <summary>Interligne des descriptions en fragments colorés.</summary>
    private int CardRunLineHeight(IntPtr hdc) => MeasureSingleLineHeight(hdc, _hFontText) + S(2);

    /// <summary>Pastille du numéro : deux chiffres de large, une ligne de haut.</summary>
    private int BadgeWidth(IntPtr hdc) => MeasureSingleLineWidth(hdc, _hFontSmall, "88") + S(14);

    private int BadgeHeight(IntPtr hdc) => MeasureSingleLineHeight(hdc, _hFontSmall) + S(6);

    /// <summary>
    /// Plancher d'une carte : sa pastille et le rembourrage vertical qui l'entoure. C'est le seul
    /// qui subsiste, et il n'est pas arbitraire — en dessous, la pastille sortirait de la carte.
    /// </summary>
    private int CardFloor(IntPtr hdc) => S(12) * 2 + BadgeHeight(hdc);

    // ═══════════════════════════════════════════════════════════════
    // Survol
    // ═══════════════════════════════════════════════════════════════

    private void AttachHoverTracking()
    {
        _hoverSubclassProc = HoverSubclassProc;
        var controls = OwnerDrawControls;
        for (int i = 0; i < controls.Length; i++)
        {
            if (controls[i] != IntPtr.Zero)
                Win32.SetWindowSubclass(controls[i], _hoverSubclassProc,
                    (UIntPtr)(HoverSubclassIdBase + (uint)i), IntPtr.Zero);
        }
    }

    private void DetachHoverTracking()
    {
        if (_hoverSubclassProc == null)
            return;

        var controls = OwnerDrawControls;
        for (int i = 0; i < controls.Length; i++)
        {
            if (controls[i] != IntPtr.Zero)
                Win32.RemoveWindowSubclass(controls[i], _hoverSubclassProc,
                    (UIntPtr)(HoverSubclassIdBase + (uint)i));
        }
        _hoverSubclassProc = null;
    }

    /// <summary>Windows ne rapporte pas le survol d'un contrôle owner-draw : il faut le suivre.
    /// <c>TrackMouseEvent</c> est réarmé à chaque entrée, il ne vaut que pour une sortie.</summary>
    private IntPtr HoverSubclassProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam,
        UIntPtr uIdSubclass, IntPtr dwRefData)
    {
        switch (msg)
        {
            case Win32.WM_MOUSEMOVE:
                if (_hoveredControl != hWnd)
                {
                    IntPtr previous = _hoveredControl;
                    _hoveredControl = hWnd;
                    if (previous != IntPtr.Zero)
                        Win32.InvalidateRect(previous, IntPtr.Zero, true);
                    Win32.InvalidateRect(hWnd, IntPtr.Zero, true);

                    var tme = new Win32.TRACKMOUSEEVENT
                    {
                        cbSize = (uint)Marshal.SizeOf<Win32.TRACKMOUSEEVENT>(),
                        dwFlags = Win32.TME_LEAVE,
                        hwndTrack = hWnd
                    };
                    Win32.TrackMouseEvent(ref tme);
                }
                break;

            case Win32.WM_MOUSELEAVE:
                if (_hoveredControl == hWnd)
                {
                    _hoveredControl = IntPtr.Zero;
                    Win32.InvalidateRect(hWnd, IntPtr.Zero, true);
                }
                break;
        }

        return Win32.DefSubclassProc(hWnd, msg, wParam, lParam);
    }

    // ═══════════════════════════════════════════════════════════════
    // Peinture des contrôles
    // ═══════════════════════════════════════════════════════════════

    private bool IsCheckBox(IntPtr control) =>
        control == _hWndChkAutoStart || control == _hWndChkDontShow
        || control == _hWndChkTraining;

    /// <summary>
    /// L'accent va à l'action que l'écran propose, et il n'y en a qu'un à l'écran.
    ///
    /// Arbitrage d'Antoine du 2026-08-30 : tant que « Essayer maintenant » est là, c'est lui —
    /// le wizard pousse le tutoriel, pas la page suivante. Sitôt qu'il disparaît (étape 1 une
    /// fois les exercices complétés, étapes 2 et 3), l'accent revient à « Suivant », qui devient
    /// « C'est parti ! » à la dernière étape. « Précédent » ne le porte jamais.
    /// </summary>
    private ButtonKind KindOf(IntPtr control)
    {
        if (control == _hWndBtnTry)
            return ButtonKind.Primary;
        if (control == _hWndBtnNext)
            return _tryButtonShown ? ButtonKind.Secondary : ButtonKind.Primary;
        return ButtonKind.Secondary;
    }

    /// <summary>
    /// Libellé d'un contrôle owner-draw. Lu dans <c>L</c> à chaque peinture plutôt que retenu :
    /// la langue change en cours de vie de la fenêtre — le drapeau du header la bascule — et un
    /// libellé mis en cache resterait dans l'ancienne.
    ///
    /// <c>SetWindowTextW</c> reste appelé sur ces contrôles bien que plus rien ne le peigne :
    /// c'est le texte que la fenêtre publie aux outils d'accessibilité.
    /// </summary>
    private string ControlText(IntPtr control)
    {
        // StepResources et non « 2 » : cette ligne est la douzième du fichier d'à côté, et la
        // seule que le passage à quatre étapes du 2026-08-30 avait oubliée. Le rendu l'a montrée
        // — le bouton de la dernière étape annonçait « Suivant » au lieu de « C'est parti ! ».
        if (control == _hWndBtnNext)
            return _currentStep == StepResources ? L.Onboarding_LetsGo : L.Onboarding_Next;
        if (control == _hWndBtnPrev) return L.Onboarding_Prev;
        if (control == _hWndBtnTry) return L.Onboarding_TryNow;
        if (control == _hWndChkAutoStart) return L.Onboarding_ChkAutoStart;
        if (control == _hWndChkDontShow) return L.Onboarding_ChkDontShow;
        if (control == _hWndChkTraining) return L.Onboarding_ChkTraining;
        return string.Empty;
    }

    private bool TryDrawItem(IntPtr lParam)
    {
        var dis = Marshal.PtrToStructure<Win32.DRAWITEMSTRUCT>(lParam);

        if (dis.CtlType != Win32.ODT_BUTTON || dis.hwndItem == IntPtr.Zero)
            return false;

        bool isCheckBox = IsCheckBox(dis.hwndItem);
        if (!isCheckBox && dis.hwndItem != _hWndBtnNext && dis.hwndItem != _hWndBtnPrev
            && dis.hwndItem != _hWndBtnTry)
            return false;

        var state = ControlState.None;
        if ((dis.itemState & Win32.ODS_DISABLED) != 0) state |= ControlState.Disabled;
        if ((dis.itemState & Win32.ODS_SELECTED) != 0) state |= ControlState.Pressed;
        if ((dis.itemState & Win32.ODS_FOCUS) != 0) state |= ControlState.Focused;
        if (_hoveredControl == dis.hwndItem) state |= ControlState.Hovered;
        if (GetCheck(dis.hwndItem)) state |= ControlState.Checked;

        // La marge de focus appartient au fond sur lequel le contrôle est posé : l'effacer avant
        // de rendre le contrôle, faute de quoi l'anneau se dessinerait sur un fond non peint.
        // Les cases sont dans le panneau Préférences de l'étape 3, les boutons sur le fond de la
        // fenêtre — deux jetons différents, et c'est le seul endroit qui en dépend.
        var full = dis.rcItem;
        Win32.FillRect(dis.hDC, ref full, Theme.Brush(isCheckBox ? CLR_PANEL_BG : CLR_BG));

        int focus = ThemeControls.FocusMargin(_dpi);
        var rect = new Win32.RECT
        {
            left = full.left + focus,
            top = full.top + focus,
            right = full.right - focus,
            bottom = full.bottom - focus,
        };

        string text = ControlText(dis.hwndItem);

        if (isCheckBox)
            ThemeControls.DrawCheckBox(dis.hDC, rect, text, _hFontBold, state, Theme.Current, _dpi);
        else
            ThemeControls.DrawButton(dis.hDC, rect, text, _hFontButton,
                KindOf(dis.hwndItem), state, Theme.Current, _dpi);

        return true;
    }
}
