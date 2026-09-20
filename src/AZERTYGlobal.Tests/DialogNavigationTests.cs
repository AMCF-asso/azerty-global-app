using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// AG130-40 — la navigation clavier de dialogue, sur inscription volontaire.
///
/// Ce qui est éprouvé ici : qui reçoit <c>IsDialogMessageW</c> et qui ne le reçoit pas.
/// L'appel lui-même, et le fait que Tab déplace bien le focus, tiennent à Win32 et à une
/// boucle de messages vivante — c'est une recette VM, pas un témoin. Mais la règle qui
/// décide, elle, se barre : et c'est elle qui protège les surfaces de frappe.
///
/// ⛔ Ces témoins partagent un état statique. Ils portent <c>Collection</c> pour que xUnit
/// ne les joue pas en parallèle d'eux-mêmes, et chacun repart d'une table vide.
/// </summary>
[Collection("DialogNavigation")]
public class DialogNavigationTests : IDisposable
{
    private static readonly IntPtr Paramètres = (IntPtr)0x1001;
    private static readonly IntPtr ÀPropos = (IntPtr)0x1002;
    private static readonly IntPtr Exercices = (IntPtr)0x2001;

    public DialogNavigationTests() => DialogNavigation.ResetForTests();
    public void Dispose() => DialogNavigation.ResetForTests();

    private static IReadOnlySet<IntPtr> Inscrites(params IntPtr[] h) => new HashSet<IntPtr>(h);

    [Fact]
    public void FenêtreInscrite_PrendLaNavigationDeDialogue()
    {
        Assert.True(DialogNavigation.ShouldRouteAsDialog(Paramètres, Inscrites(Paramètres, ÀPropos)));
    }

    [Fact]
    public void SurfaceDeFrappeNonInscrite_GardeSesTouches()
    {
        // ⛔ Le témoin qui compte. LessonsWindow et LearningModule sont des exercices de
        // frappe : Tab et Entrée y sont des caractères à taper. IsDialogMessageW les
        // mangerait, et l'exercice cesserait de recevoir ce qu'on y tape.
        Assert.False(DialogNavigation.ShouldRouteAsDialog(Exercices, Inscrites(Paramètres, ÀPropos)));
    }

    [Fact]
    public void AucuneFenêtreInscrite_RienNEstRouté()
    {
        // L'état normal de l'application : icône de zone de notification, aucune fenêtre
        // ouverte. La boucle voit pourtant tous les messages du processus, timers compris.
        Assert.False(DialogNavigation.ShouldRouteAsDialog(Paramètres, Inscrites()));
    }

    [Fact]
    public void RacineNulle_NEstJamaisRoutée()
    {
        // GetAncestor rend IntPtr.Zero pour un message sans fenêtre — les messages de
        // timer postés par SetTimer(hWnd: Zero) en sont. Router sur Zero passerait un
        // handle invalide à IsDialogMessageW.
        Assert.False(DialogNavigation.ShouldRouteAsDialog(IntPtr.Zero, Inscrites(Paramètres, IntPtr.Zero)));
    }

    [Fact]
    public void Inscription_PuisDésinscription_RendLaTableVide()
    {
        DialogNavigation.Register(Paramètres);
        DialogNavigation.Register(ÀPropos);
        Assert.Equal(2, DialogNavigation.RegisteredCount);

        DialogNavigation.Unregister(Paramètres);
        Assert.Equal(1, DialogNavigation.RegisteredCount);

        DialogNavigation.Unregister(ÀPropos);
        Assert.Equal(0, DialogNavigation.RegisteredCount);
    }

    [Fact]
    public void FenêtreFermée_NeRoutePlus()
    {
        // ⛔ Windows recycle les HWND. Une fenêtre détruite mais restée inscrite ferait
        // router les messages de la fenêtre inconnue qui hérite du même handle.
        DialogNavigation.Register(Paramètres);
        DialogNavigation.Unregister(Paramètres);

        Assert.False(DialogNavigation.ShouldRouteAsDialog(Paramètres, Inscrites()));
    }

    [Fact]
    public void HandleNul_NeSInscritPas()
    {
        // CreateWindowExW rend Zero quand il échoue. L'inscrire polluerait la table d'une
        // entrée que toute racine nulle viendrait ensuite percuter.
        DialogNavigation.Register(IntPtr.Zero);
        Assert.Equal(0, DialogNavigation.RegisteredCount);
    }

    [Fact]
    public void DoubleInscription_NeCompteQuUneFois()
    {
        // Une fenêtre rouverte sans avoir été désinscrite ne doit pas laisser de doublon
        // qu'une seule désinscription ne nettoierait pas.
        DialogNavigation.Register(Paramètres);
        DialogNavigation.Register(Paramètres);
        Assert.Equal(1, DialogNavigation.RegisteredCount);

        DialogNavigation.Unregister(Paramètres);
        Assert.Equal(0, DialogNavigation.RegisteredCount);
    }

    [Fact]
    public void DésinscrireUneInconnue_NeCasseRien()
    {
        DialogNavigation.Register(Paramètres);
        DialogNavigation.Unregister(Exercices);
        Assert.Equal(1, DialogNavigation.RegisteredCount);
    }
}
