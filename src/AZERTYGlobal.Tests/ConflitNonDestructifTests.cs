using System;
using System.Runtime.InteropServices;
using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Conflit de disposition (1.3.0) — le choix non destructif d'abord. La fenêtre peut surgir
/// pendant une frappe (bascule Ctrl+Maj) : « Quitter » était le bouton par défaut et le
/// premier arrêt de tabulation, si bien qu'une Entrée destinée au document pouvait arrêter
/// l'application. Deux liens de commande désormais, « Garder » en tête, par défaut, focalisé.
/// </summary>
public class ConflitNonDestructifTests
{
    private const int GWL_STYLE = -16;
    private const uint GW_CHILD = 5;
    private const uint GW_HWNDNEXT = 2;
    private const uint BS_TYPEMASK = 0x000F;

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

    private static uint ButtonType(IntPtr button) => (uint)GetWindowLongPtr(button, GWL_STYLE).ToInt64() & BS_TYPEMASK;

    [Fact]
    public void GarderEstLePremierLienEtLeBoutonParDéfaut()
    {
        using var fenêtre = new LayoutConflictWindow(false, () => { }, () => { });
        IntPtr garder = BancCapture.Field<IntPtr>(fenêtre, "_hWndBtnKeep");
        IntPtr quitter = BancCapture.Field<IntPtr>(fenêtre, "_hWndBtnQuit");

        Assert.NotEqual(IntPtr.Zero, garder);
        Assert.Equal(Win32.BS_DEFCOMMANDLINK, ButtonType(garder));
        Assert.Equal(Win32.BS_COMMANDLINK, ButtonType(quitter));
        // L'ordre des enfants est l'ordre de Tab d'IsDialogMessageW ; les textes (STATIC, sans
        // WS_TABSTOP) les précèdent pour la lecture et n'y prennent pas part.
        IntPtr premier = NextTabStop(GetWindow(BancCapture.Handle(fenêtre), GW_CHILD));
        Assert.Equal(garder, premier);
        Assert.Equal(quitter, NextTabStop(GetWindow(premier, GW_HWNDNEXT)));
    }

    private const long WS_TABSTOP = 0x00010000;

    private static IntPtr NextTabStop(IntPtr child)
    {
        while (child != IntPtr.Zero && (GetWindowLongPtr(child, GWL_STYLE).ToInt64() & WS_TABSTOP) == 0)
            child = GetWindow(child, GW_HWNDNEXT);
        return child;
    }

    /// <summary>
    /// Audit UX 1.3.0, #8 : titre, intro et question étaient peints en GDI et le Narrateur ne
    /// les lisait pas. Ce sont des STATIC, avant les liens dans l'ordre de lecture.
    /// </summary>
    [Fact]
    public void LesTextesSontDesContrôlesLisibles()
    {
        using var fenêtre = new LayoutConflictWindow(true, () => { }, () => { });
        var textes = new List<string>();
        for (IntPtr child = GetWindow(BancCapture.Handle(fenêtre), GW_CHILD); child != IntPtr.Zero; child = GetWindow(child, GW_HWNDNEXT))
        {
            var texte = new System.Text.StringBuilder(512);
            Win32.GetWindowTextW(child, texte, texte.Capacity);
            textes.Add(texte.ToString());
        }
        Assert.Equal(new[] { L.LayoutConflict_Title, L.LayoutConflict_IntroAtStartup, L.LayoutConflict_Question },
            textes.Take(3));
    }

    [Fact]
    public void ÀLOuverture_EntréeGardeLApplication()
    {
        bool quitté = false, gardé = false;
        using var invisibles = new FenetresInvisibles();
        using var fenêtre = new LayoutConflictWindow(false, () => quitté = true, () => gardé = true);
        IntPtr hwnd = BancCapture.Handle(fenêtre);
        IntPtr garder = BancCapture.Field<IntPtr>(fenêtre, "_hWndBtnKeep");
        IntPtr quitter = BancCapture.Field<IntPtr>(fenêtre, "_hWndBtnQuit");
        try
        {
            fenêtre.Show();
            Assert.Equal(garder, Win32.GetFocus());

            // Entrée arrive en IDOK (IsDialogMessageW) et presse le bouton focalisé.
            Win32.SendMessageW(hwnd, Win32.WM_COMMAND, (IntPtr)DialogNavigation.IDOK, IntPtr.Zero);
            Assert.True(gardé);
            Assert.False(quitté);
            Assert.Equal(IntPtr.Zero, DialogNavigation.ButtonToPressOnEnter(DialogNavigation.IDOK, hwnd, new[] { garder, quitter }));
        }
        finally
        {
            Win32.ShowWindow(hwnd, 0);
        }
    }

    [Fact]
    public void LesLiensOntUneNoteEtTiennentDansLaFenêtre()
    {
        using var fenêtre = new LayoutConflictWindow(true, () => { }, () => { });
        IntPtr hwnd = BancCapture.Handle(fenêtre);
        IntPtr quitter = BancCapture.Field<IntPtr>(fenêtre, "_hWndBtnQuit");
        Win32.GetClientRect(hwnd, out var client);
        Win32.GetWindowRect(quitter, out var lien);
        var bas = new Win32.POINT { x = lien.right, y = lien.bottom };
        Win32.ScreenToClient(hwnd, ref bas);

        // BCM_GETIDEALSIZE rend la note repliée : un lien d'une seule ligne ferait 40 px au plus.
        Assert.True(lien.bottom - lien.top > 50, $"lien de {lien.bottom - lien.top} px");
        Assert.True(bas.y <= client.bottom, $"lien jusqu'à {bas.y}, zone client {client.bottom}");
    }
}
