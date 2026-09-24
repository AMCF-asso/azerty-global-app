using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Audit 24/09 B4 — la recherche ouverte depuis le menu de l'icône n'insère pas dans le shell.
///
/// Au clic droit sur l'icône, le premier plan est <c>Shell_TrayWnd</c> (ou le panneau de
/// débordement) : il devenait la cible, l'insertion rendait <c>Inserted</c> et le caractère
/// n'arrivait nulle part, sans copie de secours. Une cible refusée vaut désormais zéro, ce
/// qui fait rendre NotInserted au service d'insertion et déclenche la copie notifiée.
/// </summary>
public class SearchTargetShellTests
{
    private static readonly IntPtr Recherche = (IntPtr)0x100;
    private static readonly IntPtr Ancienne = (IntPtr)0x200;
    private static readonly IntPtr Candidate = (IntPtr)0x300;

    [Theory]
    [InlineData("Shell_TrayWnd")]
    [InlineData("Shell_SecondaryTrayWnd")]
    [InlineData("NotifyIconOverflowWindow")]
    [InlineData("TopLevelWindowForOverflowXamlIsland")]
    [InlineData("shell_traywnd")]
    public void SurfaceDuShell_EstRefusée(string className)
    {
        Assert.True(CharacterSearch.IsRefusedTargetClass(className));
        Assert.Equal(IntPtr.Zero, CharacterSearch.ResolveInsertionTarget(
            Candidate, Recherche, Ancienne, _ => true, _ => className));
    }

    [Theory]
    [InlineData("Notepad")]
    [InlineData("Chrome_WidgetWin_1")]
    [InlineData("Shell_TrayWndX")]
    [InlineData(null)]
    public void FenêtreOrdinaire_DevientLaCible(string? className)
    {
        // Une classe illisible (null) ne suffit pas à refuser : la fenêtre existe.
        Assert.False(CharacterSearch.IsRefusedTargetClass(className));
        Assert.Equal(Candidate, CharacterSearch.ResolveInsertionTarget(
            Candidate, Recherche, Ancienne, _ => true, _ => className));
    }

    [Fact]
    public void CibleNulleOuDétruite_EffaceLAncienneCible()
    {
        // ⛔ Avant B4, l'ancienne cible survivait : une recherche ouverte depuis le menu
        // insérait dans la fenêtre d'une session précédente.
        Assert.Equal(IntPtr.Zero, CharacterSearch.ResolveInsertionTarget(
            IntPtr.Zero, Recherche, Ancienne, _ => true, _ => "Notepad"));
        Assert.Equal(IntPtr.Zero, CharacterSearch.ResolveInsertionTarget(
            Candidate, Recherche, Ancienne, _ => false, _ => "Notepad"));
    }

    [Fact]
    public void LaRechercheElleMême_GardeLaCibleConnue()
    {
        Assert.Equal(Ancienne, CharacterSearch.ResolveInsertionTarget(
            Recherche, Recherche, Ancienne, _ => true, _ => "AZERTYGlobalSearch"));
    }

    [Fact]
    public void CibleZero_DonneNotInserted_SansFocusNiÉmission()
    {
        // Le maillon suivant : c'est ce NotInserted qui autorise la copie de secours.
        int calls = 0;
        var service = new TextInsertionService(_ => { calls++; return new(1, 1); }, _ => { calls++; return true; });
        Assert.Equal(TextInsertionOutcome.NotInserted, service.Insert(IntPtr.Zero, "é"));
        Assert.Equal(0, calls);
    }
}
