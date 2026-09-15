using Xunit;

namespace AZERTYGlobal.Tests;

public class Audit120BitmapTests
{
    [Theory]
    [InlineData(16, 32)]
    [InlineData(32, 128)]
    [InlineData(40, 240)]
    [InlineData(48, 288)]
    [InlineData(64, 512)]
    public void MasqueIcone_ReserveChaqueLigneAlignee(int size, int bytes)
    {
        Assert.Equal(bytes, GdiHelpers.MonochromeMaskByteCount(size, size));
    }
}
