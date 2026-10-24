using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// F6 (décision S1 du 2026-09-02, § 3) : la notation Store suit le canal Store, plus le fait
/// d'être packagé. Le paquet AMCF est packagé et n'a pas de fiche.
/// </summary>
public class StoreReviewChannelTests
{
    [Theory]
    [InlineData("Store", true)]
    [InlineData("Amcf", false)]
    [InlineData("Unpackaged", false)]
    public void SeulLeCanalStoreANoter(string canal, bool attendu) =>
        Assert.Equal(attendu, StoreReview.RatesOnStore(Enum.Parse<DistributionChannel>(canal)));

    /// <summary>Hors canal Store, la boîte de notation n'est même pas tentée : l'appelant
    /// ouvre son repli. Propriétaire non nul, pour que seul le canal décide.</summary>
    [Theory]
    [InlineData("Amcf")]
    [InlineData("Unpackaged")]
    public void HorsStore_LaBoîteNEstPasTentée(string canal)
    {
        using (AppChannel.OverrideForTests(Enum.Parse<DistributionChannel>(canal)))
            Assert.False(StoreReview.TryShow((IntPtr)1, "https://example.invalid/"));
    }
}
