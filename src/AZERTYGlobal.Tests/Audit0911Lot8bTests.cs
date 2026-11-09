using System.Text;
using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Audit UX du 09/11, lot 8b (mineurs) : E28 (dialogue de pause), E30 (liens de l'accueil), E26 (pause),
/// E13, E14, W22, W23, W24.
/// </summary>
public sealed class Audit0911Lot8bTests
{
    private static string TextOf(IntPtr hwnd)
    {
        var buffer = new StringBuilder(Math.Max(16, Win32.GetWindowTextLengthW(hwnd) + 1));
        Win32.GetWindowTextW(hwnd, buffer, buffer.Capacity);
        return buffer.ToString();
    }

    // ── E28 : dialogue de pause ─────────────────────────────────────────────

    [Theory]
    [InlineData("1a2b", "12")]
    [InlineData("abc", "")]
    [InlineData("12 mn", "12")]
    [InlineData("-5", "5")]
    public void Pause_ChampsNumériques_UnCollageNonNumériqueNeGardeQueLesChiffres(string collé, string attendu)
    {
        using var isolation = new BancCapture.Isolation();
        var dialog = new PauseDurationDialog();
        try
        {
            BancCapture.Call(dialog, "CreateWindow", IntPtr.Zero);
            Assert.NotEqual(IntPtr.Zero, BancCapture.Handle(dialog));
            foreach (string champ in new[] { "_hEditHours", "_hEditMinutes" })
            {
                IntPtr edit = BancCapture.Field<IntPtr>(dialog, champ);
                // WM_SETTEXT contourne ES_NUMBER comme le fait un collage : EN_CHANGE suit.
                Win32.SetWindowTextW(edit, collé);
                Assert.Equal(attendu, TextOf(edit));
            }
        }
        finally { BancCapture.Teardown(dialog); }
    }

    [Fact]
    public void Pause_ChampsNumériques_LeCollageEstLimitéÀDeuxChiffres()
    {
        using var isolation = new BancCapture.Isolation();
        var dialog = new PauseDurationDialog();
        try
        {
            BancCapture.Call(dialog, "CreateWindow", IntPtr.Zero);
            IntPtr edit = BancCapture.Field<IntPtr>(dialog, "_hEditMinutes");
            Assert.Equal(2, (int)Win32.SendMessageW(edit, 0x00D5 /* EM_GETLIMITTEXT */, IntPtr.Zero, IntPtr.Zero));
        }
        finally { BancCapture.Teardown(dialog); }
    }
}
