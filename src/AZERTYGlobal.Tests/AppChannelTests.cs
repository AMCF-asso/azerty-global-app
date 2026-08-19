using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Détection du canal de distribution : les trois valeurs de l'énumération, le family name
/// littéral de l'identité Store, et le scope de test qui restaure le canal réel.
///
/// Les tests de classification éprouvent <c>AppChannel.Classify</c>, fonction pure : ils ne
/// touchent pas au canal du processus, donc ils ne peuvent pas en laisser un derrière eux.
/// C'est l'inverse du motif qui a rendu un test vert en isolation et rouge dans la suite
/// entière le 2026-08-18 — un statique de processus sans remise à zéro.
/// </summary>
public class AppChannelTests
{
    // Family name mesuré le 2026-08-19 sur le paquet installé, écrit en dur et non recomposé
    // depuis ProductIdentity : un test qui reconstruit la chaîne qu'il vérifie passe au vert
    // quel que soit le condensé réellement embarqué.
    private const string StoreFamilyName = "AZERTYGlobal.AZERTYGlobal_w9kghr08zmhbg";

    [Fact]
    public void StorePackageFamilyName_MatchesTheInstalledPackage()
    {
        Assert.Equal(StoreFamilyName, ProductIdentity.StorePackageFamilyName);
    }

    /// <summary>Seul test qui traverse le P/Invoke plutôt que la fonction pure : l'hôte de
    /// tests ne tourne jamais dans un package, donc la détection doit rendre Unpackaged. Il
    /// tombe si <c>GetCurrentPackageFamilyName</c> lève, si son code d'erreur est mal lu, ou si
    /// un autre test a laissé un canal forcé derrière lui.</summary>
    [Fact]
    public void Current_InTheTestHost_IsUnpackaged()
    {
        Assert.Equal(DistributionChannel.Unpackaged, AppChannel.Current);
    }

    [Fact]
    public void Classify_NoPackage_ReturnsUnpackaged()
    {
        Assert.Equal(DistributionChannel.Unpackaged,
            AppChannel.Classify(packaged: false, familyName: null));
    }

    [Fact]
    public void Classify_StoreFamilyName_ReturnsStore()
    {
        Assert.Equal(DistributionChannel.Store,
            AppChannel.Classify(packaged: true, familyName: StoreFamilyName));
    }

    [Fact]
    public void Classify_UnknownFamilyName_ReturnsAmcf()
    {
        // Même nom de paquet, autre éditeur : le condensé change, donc le canal aussi.
        Assert.Equal(DistributionChannel.Amcf,
            AppChannel.Classify(packaged: true, familyName: "AZERTYGlobal.AZERTYGlobal_2gvhbpqmwn8dr"));
    }

    /// <summary>Témoin de la direction d'échec : un family name qu'on n'a pas su lire donne le
    /// canal sobre, jamais le canal qui sollicite.</summary>
    [Fact]
    public void Classify_PackagedWithoutFamilyName_ReturnsAmcf()
    {
        Assert.Equal(DistributionChannel.Amcf,
            AppChannel.Classify(packaged: true, familyName: null));
    }

    /// <summary>Fixe le choix de comparaison : insensible à la casse, comme Windows compare les
    /// identités de paquet. Ce test rougit si la comparaison passe en ordinal strict.</summary>
    [Fact]
    public void Classify_StoreFamilyNameInAnotherCase_ReturnsStore()
    {
        Assert.Equal(DistributionChannel.Store,
            AppChannel.Classify(packaged: true, familyName: StoreFamilyName.ToUpperInvariant()));
    }

    [Fact]
    public void OverrideForTests_RestoresThePreviousChannelOnDispose()
    {
        var reel = AppChannel.Current;

        using (AppChannel.OverrideForTests(DistributionChannel.Amcf))
        {
            Assert.Equal(DistributionChannel.Amcf, AppChannel.Current);

            using (AppChannel.OverrideForTests(DistributionChannel.Store))
            {
                Assert.Equal(DistributionChannel.Store, AppChannel.Current);
            }

            Assert.Equal(DistributionChannel.Amcf, AppChannel.Current);
        }

        Assert.Equal(reel, AppChannel.Current);
    }

    [Fact]
    public void OverrideForTests_RestoresEvenWhenTheBodyThrows()
    {
        var reel = AppChannel.Current;

        // Typé explicitement : sans cela, Assert.Throws ne sait pas choisir entre
        // Action et Func<Task> pour une lambda qui se termine par un throw.
        Action corps = () =>
        {
            using (AppChannel.OverrideForTests(DistributionChannel.Amcf))
            {
                throw new InvalidOperationException("témoin");
            }
        };

        Assert.Throws<InvalidOperationException>(corps);

        Assert.Equal(reel, AppChannel.Current);
    }
}
