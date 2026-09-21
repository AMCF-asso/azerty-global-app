using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Revue de code du 2026-09-21 (R2, R5) — les deux décisions pures que les fenêtres inscrites
/// à <c>IsDialogMessageW</c> prennent au clavier : quel <c>WM_GETDLGCODE</c> rendre pour une
/// touche donnée, et quoi faire d'un <c>WM_COMMAND</c> portant <c>IDOK</c> ou <c>IDCANCEL</c>.
///
/// Ce qui n'est pas éprouvé ici, et reste à la recette VM : que Windows envoie bien ces
/// messages, et que le focus sorte effectivement d'un lien sur Tab.
/// </summary>
public class DialogNavigationKeyboardTrapTests
{
    private const long DLGC_STATIC = 0x0100;
    private const long DLGC_WANTARROWS = 0x0001;
    private const int VK_TAB = 0x09;
    private const int VK_RETURN = 0x0D;
    private const int VK_ESCAPE = 0x1B;

    [Fact]
    public void Tab_EstRenduALaNavigation()
    {
        // ⛔ Le témoin qui compte (R2). Un lien qui garde Tab est un piège clavier : une fois
        // le focus dessus, ni Tab ni Maj+Tab n'en sortent.
        long code = DialogNavigation.DialogCodeKeepingTab(DLGC_STATIC, Win32.WM_KEYDOWN, VK_TAB);
        Assert.Equal(0, code & DialogNavigation.DLGC_WANTALLKEYS);
        Assert.Equal(DLGC_STATIC, code);
    }

    [Fact]
    public void AltTab_EstRenduAussi()
    {
        long code = DialogNavigation.DialogCodeKeepingTab(DLGC_STATIC, Win32.WM_SYSKEYDOWN, VK_TAB);
        Assert.Equal(0, code & DialogNavigation.DLGC_WANTALLKEYS);
    }

    [Theory]
    [InlineData(VK_RETURN)]
    [InlineData(VK_ESCAPE)]
    [InlineData(0x41)] // A
    public void AutreTouche_EstGardeeParLeControle(int vk)
    {
        // Entrée ouvre le lien, Échap ferme la fenêtre, une lettre est un raccourci de
        // capture : le contrôle les veut, IsDialogMessageW ne doit pas les convertir.
        long code = DialogNavigation.DialogCodeKeepingTab(DLGC_STATIC, Win32.WM_KEYDOWN, vk);
        Assert.NotEqual(0, code & DialogNavigation.DLGC_WANTALLKEYS);
    }

    [Fact]
    public void SansMessage_LeControleGardeToutesLesTouches()
    {
        // WM_GETDLGCODE peut arriver avec lParam nul (interrogation générale, pas une touche).
        long code = DialogNavigation.DialogCodeKeepingTab(DLGC_WANTARROWS, 0, 0);
        Assert.NotEqual(0, code & DialogNavigation.DLGC_WANTALLKEYS);
        Assert.NotEqual(0, code & DLGC_WANTARROWS); // le code de base est conservé, pas écrasé
    }

    [Fact]
    public void TabRelache_NEstPasUneTabulation()
    {
        // Témoin négatif : seul le keydown compte. Un WM_KEYUP portant VK_TAB ne doit pas
        // ouvrir une brèche dans WANTALLKEYS.
        const uint WM_KEYUP = 0x0101;
        long code = DialogNavigation.DialogCodeKeepingTab(DLGC_STATIC, WM_KEYUP, VK_TAB);
        Assert.NotEqual(0, code & DialogNavigation.DLGC_WANTALLKEYS);
    }

    [Fact]
    public void Echap_ArriveCommeIdCancel()
    {
        // R5 : les gestionnaires WM_KEYDOWN/VK_ESCAPE des fenêtres inscrites sont morts ;
        // c'est WM_COMMAND(IDCANCEL) qui porte la touche.
        Assert.True(DialogNavigation.IsEscapeCommand(DialogNavigation.IDCANCEL));
        Assert.False(DialogNavigation.IsEscapeCommand(DialogNavigation.IDOK));
        Assert.False(DialogNavigation.IsEscapeCommand(3120)); // IDC_TAB_STRIP
    }

    [Fact]
    public void Entree_PresseLeBoutonFocalise()
    {
        var ajouter = (IntPtr)0x51;
        var retirer = (IntPtr)0x52;
        Assert.Equal(ajouter, DialogNavigation.ButtonToPressOnEnter(DialogNavigation.IDOK, ajouter, new[] { ajouter, retirer }));
    }

    [Fact]
    public void Entree_SurUnAutreControle_NePresseRien()
    {
        // Focus sur une case à cocher ou un champ : Entrée ne doit pas déclencher un bouton
        // que l'utilisateur ne regarde pas.
        var ajouter = (IntPtr)0x51;
        var caseACocher = (IntPtr)0x77;
        Assert.Equal(IntPtr.Zero, DialogNavigation.ButtonToPressOnEnter(DialogNavigation.IDOK, caseACocher, new[] { ajouter }));
        Assert.Equal(IntPtr.Zero, DialogNavigation.ButtonToPressOnEnter(DialogNavigation.IDOK, IntPtr.Zero, new[] { ajouter }));
    }

    [Fact]
    public void Echap_NePresseJamaisUnBouton()
    {
        var ajouter = (IntPtr)0x51;
        Assert.Equal(IntPtr.Zero, DialogNavigation.ButtonToPressOnEnter(DialogNavigation.IDCANCEL, ajouter, new[] { ajouter }));
    }
}
