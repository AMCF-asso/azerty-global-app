using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

public class TextInsertionServiceTests
{
    [Fact]
    public void TryInsert_ActivatesTargetBeforeEmission()
    {
        var calls = new List<string>();
        var target = (IntPtr)42;
        var service = new TextInsertionService(
            text => calls.Add("emit:" + text),
            hwnd =>
            {
                calls.Add("focus:" + hwnd);
                return true;
            });

        Assert.True(service.TryInsert(target, "α"));
        Assert.Equal(new[] { "focus:42", "emit:α" }, calls);
    }

    [Fact]
    public void TryInsert_FocusFailure_DoesNotEmit()
    {
        bool emitted = false;
        var service = new TextInsertionService(_ => emitted = true, _ => false);

        Assert.False(service.TryInsert((IntPtr)42, "α"));
        Assert.False(emitted);
    }

    [Fact]
    public void TryInsert_TargetWindowGone_ReportsFailureForFallback()
    {
        bool emitted = false;
        // Chemin d'activation réel : IsWindow refuse un HWND qui n'existe plus,
        // TryInsert doit retourner false pour déclencher la copie de secours.
        var service = new TextInsertionService(_ => emitted = true);

        Assert.False(service.TryInsert(new IntPtr(0x0BADF00D), "α"));
        Assert.False(emitted);
    }

    [Theory]
    [InlineData(0, "α")]
    [InlineData(42, "")]
    public void TryInsert_InvalidInput_DoesNotActivate(long hwnd, string text)
    {
        bool activated = false;
        var service = new TextInsertionService(_ => { }, _ => activated = true);

        Assert.False(service.TryInsert((IntPtr)hwnd, text));
        Assert.False(activated);
    }
}
