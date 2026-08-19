using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Comportement sobre du canal AMCF — lot B du plan v1.2.0, décisions D3 et D4 du
/// 2026-08-19. Chaque entrée est éprouvée dans les deux sens : absente du canal sobre, et
/// présente sur le canal Store. Le sens « présente » est celui qu'on oublie d'écrire, et
/// c'est lui qui attrape une condition inversée.
///
/// Ces tests lisent ce que le menu construit vraiment : <c>ShowContextMenu</c> boucle sur
/// <see cref="TrayApplication.FeedbackMenuEntries"/> et
/// <see cref="TrayApplication.FeedbackTopLevelEntries"/>, il ne porte plus les entrées en
/// dur. Un simple prédicat gardant des AppendMenuW aurait donné un test qui vérifie que
/// Store est Store, vert même sur un <c>if</c> inversé ou oublié.
///
/// Ce qui n'est pas couvert ici : la sortie tôt de <c>MaybeShowReviewPrompt</c>, privée et
/// inséparable de sa fenêtre. Elle n'utilise que le prédicat testé plus bas ; le fait
/// qu'aucune bulle ne s'affiche sur un vrai paquet AMCF attend les smoke tests du lot G.
/// </summary>
public class SoberChannelTests
{
    [Fact]
    public void FeedbackMenu_SurStore_PorteLesQuatreEntrees()
    {
        Assert.Equal(
            new[]
            {
                TrayApplication.IDM_SUPPORT, TrayApplication.IDM_FEEDBACK,
                TrayApplication.IDM_DISCORD, TrayApplication.IDM_BUG,
            },
            TrayApplication.FeedbackMenuEntries(DistributionChannel.Store));
    }

    /// <summary>D3 et D4 réunis : ce qui part, ce qui reste, et dans l'ordre.</summary>
    [Fact]
    public void FeedbackMenu_SurAmcf_NeGardeQueAvisEtBug()
    {
        Assert.Equal(
            new[] { TrayApplication.IDM_FEEDBACK, TrayApplication.IDM_BUG },
            TrayApplication.FeedbackMenuEntries(DistributionChannel.Amcf));
    }

    [Fact]
    public void FeedbackMenu_SurAmcf_NeContientNiSoutienNiDiscord()
    {
        var entrees = TrayApplication.FeedbackMenuEntries(DistributionChannel.Amcf);

        Assert.DoesNotContain(TrayApplication.IDM_SUPPORT, entrees);
        Assert.DoesNotContain(TrayApplication.IDM_DISCORD, entrees);
    }

    /// <summary>Le sous-menu perd deux entrées sur quatre : il ne devient ni vide, ni un
    /// menu à une seule ligne — ce que le plan demandait de vérifier.</summary>
    [Fact]
    public void FeedbackMenu_SurAmcf_GardeDeuxEntreesSurQuatre()
    {
        Assert.Equal(2, TrayApplication.FeedbackMenuEntries(DistributionChannel.Amcf).Length);
    }

    [Fact]
    public void NoterSurLeStore_PresenteSurStore()
    {
        Assert.Equal(
            new[] { TrayApplication.IDM_RATE_STORE },
            TrayApplication.FeedbackTopLevelEntries(DistributionChannel.Store));
    }

    [Fact]
    public void NoterSurLeStore_AbsenteSurAmcf()
    {
        Assert.Empty(TrayApplication.FeedbackTopLevelEntries(DistributionChannel.Amcf));
    }

    /// <summary>Décision D8 : l'EXE hors package garde le menu d'aujourd'hui, ses trois liens
    /// externes compris. Ce test rougit si le garde-fou passe de « == Amcf » à « ≠ Store »,
    /// formulation que le lot B du plan emploie et qui trancherait D8 au passage.</summary>
    [Fact]
    public void HorsPackage_GardeLeMenuDAujourdhui()
    {
        Assert.Equal(4, TrayApplication.FeedbackMenuEntries(DistributionChannel.Unpackaged).Length);
        Assert.Single(TrayApplication.FeedbackTopLevelEntries(DistributionChannel.Unpackaged));
    }

    [Fact]
    public void CanalSobre_EstLeCanalAmcfEtLuiSeul()
    {
        Assert.True(AppChannel.IsSober(DistributionChannel.Amcf));
        Assert.False(AppChannel.IsSober(DistributionChannel.Store));
        Assert.False(AppChannel.IsSober(DistributionChannel.Unpackaged));
    }
}
