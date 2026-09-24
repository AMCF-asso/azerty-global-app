using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Relance depuis Démarrer d'une instance packagée déjà vivante (audit du 24/09). La
/// seconde instance distingue une activation par Windows, qui doit mourir en silence, d'une
/// relance par l'utilisateur, qu'elle signale à l'instance vivante avant de sortir.
///
/// ⚠️ Limite assumée : FindWindowW, AllowSetForegroundWindow et la réception du message
/// par TrayApplication sont du Win32 à deux processus, prouvés par la recette.
/// </summary>
public class RelaunchSignalTests
{
    private const string Exe = @"C:\Program Files\WindowsApps\app.exe";

    [Theory]
    [InlineData("-Embedding")]
    [InlineData("/Embedding")]
    [InlineData("-embedding")]
    [InlineData("-ToastActivated")]
    [InlineData("----ToastActivated")]
    public void Activation_systeme_reconnue(string argument)
    {
        Assert.True(RelaunchSignal.IsSystemActivation(new[] { Exe, argument }));
    }

    [Fact]
    public void Relance_depuis_Demarrer_n_est_pas_une_activation_systeme()
    {
        // Démarrer lance l'exécutable sans argument : c'est le cas à signaler.
        Assert.False(RelaunchSignal.IsSystemActivation(new[] { Exe }));
    }

    [Fact]
    public void Le_chemin_de_l_executable_n_est_jamais_lu_comme_argument()
    {
        // Le premier élément est le chemin : un dossier nommé « Embedding » ne compte pas.
        Assert.False(RelaunchSignal.IsSystemActivation(new[] { "-Embedding" }));
        Assert.False(RelaunchSignal.IsSystemActivation(new[] { Exe, "--autre" }));
    }

    [Fact]
    public void Message_et_classe_derives_de_l_identite_produit()
    {
        Assert.StartsWith(ProductIdentity.Namespace, RelaunchSignal.MessageName);
        Assert.Equal(ProductIdentity.WindowClass("Wnd"), RelaunchSignal.TargetWindowClass);
    }
}
