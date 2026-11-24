using System.Text;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Accueil 1.3.0, décision d'Antoine du 11/11 : l'étape « Écrire le peul, le wolof, le bambara »
/// est retirée pour tous (la Prise en main demande déjà l'usage et en fait l'exercice). Ces
/// témoins fixent les trois étapes et le lien de l'étape Ressources qui la remplace.
/// </summary>
public class AccueilTroisEtapesTests : IDisposable
{
    public AccueilTroisEtapesTests() => L.Language = "fr";
    public void Dispose() => L.Language = "fr";

    [Fact]
    public void LAccueilCompteTroisEtapes_RessourcesEnDernier()
    {
        Assert.Equal(3, OnboardingStep.Count);
        Assert.Equal(0, OnboardingStep.Changes);
        Assert.Equal(1, OnboardingStep.HowTo);
        Assert.Equal(2, OnboardingStep.Resources);
        Assert.Equal(OnboardingStep.Resources, OnboardingStep.Last);
        Assert.Equal("Étape 3 sur 3", OnboardingStep.Label(OnboardingStep.Last));
        L.Language = "en";
        Assert.Equal("Step 2 of 3", OnboardingStep.Label(OnboardingStep.HowTo));
    }

    [Fact]
    public void AucuneEtapeNeParlePlusDesLanguesAfricaines()
    {
        for (int étape = 0; étape < OnboardingStep.Count; étape++)
        {
            string résumé = OnboardingWindow.StepAccessibleSummary(étape);
            Assert.DoesNotContain("peul", résumé, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("ɓ", résumé);
        }
    }

    [Fact]
    public void LeLienAfrique_NEstVisibleQuALaDerniereEtape_EtSuitLaLangue()
    {
        using var isolation = new BancCapture.Isolation();
        var fenêtre = new OnboardingWindow();
        try
        {
            fenêtre.Show();
            IntPtr lien = BancCapture.Field<IntPtr>(fenêtre, "_hWndLinkAfrica");
            Assert.NotEqual(IntPtr.Zero, lien);
            Assert.False(Win32.IsWindowVisible(lien), "le lien ne paraît pas à l'étape 1");

            while (BancCapture.Field<int>(fenêtre, "_currentStep") < OnboardingStep.Last)
                BancCapture.Call(fenêtre, "GoToNextStep");
            Assert.True(Win32.IsWindowVisible(lien));
            Assert.Equal("Écrire le peul, le wolof, le bambara", Texte(lien));

            BancCapture.Call(fenêtre, "GoToPreviousStep");
            Assert.False(Win32.IsWindowVisible(lien), "un retour en arrière le masque");

            L.Language = "en";
            BancCapture.Call(fenêtre, "RefreshLanguageTexts");
            Assert.Equal("Write Fula, Wolof and Bambara", Texte(lien));
        }
        finally { BancCapture.Teardown(fenêtre); }
    }

    [Fact]
    public void LeLienAfrique_OuvreLaPageDeLaLangueDeLInterface()
    {
        Assert.Equal("/afrique", L.Onboarding_LinkAfricaPath);
        Assert.EndsWith("/afrique", ProductIdentity.Url(L.Onboarding_LinkAfricaPath));
        L.Language = "en";
        Assert.Equal("/en/africa", L.Onboarding_LinkAfricaPath);
        Assert.EndsWith("/en/africa", ProductIdentity.Url(L.Onboarding_LinkAfricaPath));
    }

    private static string Texte(IntPtr hwnd)
    {
        var tampon = new StringBuilder(256);
        Win32.GetWindowTextW(hwnd, tampon, tampon.Capacity);
        return tampon.ToString();
    }
}
