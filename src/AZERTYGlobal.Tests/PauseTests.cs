using System.Reflection;
using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Pause refaite le 2026-09-28 (décisions d'Antoine) : sous-menu de durées, « Jusqu'à
/// demain » = le prochain 8 h, et une fenêtre « Personnaliser » en heures et minutes avec
/// les flèches Windows.
/// </summary>
public class PauseTests
{
    [Theory]
    [InlineData("2026-09-28 14:30", "2026-09-29 08:00")] // après 8 h : demain
    [InlineData("2026-09-28 08:00", "2026-09-29 08:00")] // à 8 h pile : demain, pas maintenant
    [InlineData("2026-09-28 07:59", "2026-09-28 08:00")] // avant 8 h : ce matin
    [InlineData("2026-09-28 00:10", "2026-09-28 08:00")]
    [InlineData("2026-12-31 23:00", "2027-01-01 08:00")]
    public void JusquADemain_EstLeProchain8h(string maintenant, string attendu)
    {
        Assert.Equal(DateTime.Parse(attendu), PauseSchedule.NextMorning(DateTime.Parse(maintenant)));
    }

    [Theory]
    [InlineData("fr", "2026-09-28 16:12", "16 h 12")]
    [InlineData("fr", "2026-09-28 08:00", "8 h")]
    [InlineData("fr", "2026-09-28 09:05", "9 h 05")]
    [InlineData("en", "2026-09-28 16:12", "4:12 PM")]
    [InlineData("en", "2026-09-28 08:00", "8:00 AM")]
    public void Heure_SuitLaTypographieDeLaLangue(string langue, string heure, string attendu)
    {
        string avant = L.Language;
        try
        {
            L.Language = langue;
            Assert.Equal(attendu, PauseSchedule.FormatClock(DateTime.Parse(heure)));
        }
        finally { L.Language = avant; }
    }

    [Theory]
    [InlineData("fr", "2026-09-28 23:30", "2026-09-29 08:00", "demain à 8 h")]
    [InlineData("fr", "2026-09-28 14:00", "2026-09-28 16:12", "à 16 h 12")]
    [InlineData("en", "2026-09-28 23:30", "2026-09-29 08:00", "tomorrow at 8:00 AM")]
    public void Reprise_DitDemainQuandLaDateChange(string langue, string maintenant, string fin, string attendu)
    {
        string avant = L.Language;
        try
        {
            L.Language = langue;
            Assert.Equal(attendu, PauseSchedule.DescribeResume(DateTime.Parse(maintenant), DateTime.Parse(fin)));
        }
        finally { L.Language = avant; }
    }

    [Theory]
    [InlineData(0, 0, null)]      // durée nulle : refusée
    [InlineData(0, 1, 1)]
    [InlineData(1, 30, 90)]
    [InlineData(0, 75, 75)]       // minutes au-delà de 59 à la frappe : 1 h 15
    [InlineData(23, 59, 1439)]
    [InlineData(23, 60, null)]    // au-delà de 23 h 59
    [InlineData(-1, 30, null)]
    public void Duree_BorneeDe1minA23h59(int heures, int minutes, int? attendu)
    {
        Assert.Equal(attendu, (int?)PauseDurationDialog.ParseDuration(heures, minutes)?.TotalMinutes);
    }

    [Fact]
    public void Personnaliser_ChaqueChampASesFlechesWindows()
    {
        using var dialogue = new PauseDurationDialog();
        typeof(PauseDurationDialog).GetMethod("CreateWindow", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(dialogue, new object[] { IntPtr.Zero });

        foreach (var (champ, fleches) in new[] { ("_hEditHours", "_hSpinHours"), ("_hEditMinutes", "_hSpinMinutes") })
        {
            IntPtr edit = Field(dialogue, champ);
            IntPtr spin = Field(dialogue, fleches);
            Assert.NotEqual(IntPtr.Zero, spin);
            // UDM_GETBUDDY : les flèches pilotent bien leur champ.
            Assert.Equal(edit, Win32.SendMessageW(spin, 0x046A, IntPtr.Zero, IntPtr.Zero));
        }
    }

    private static IntPtr Field(object o, string name) =>
        (IntPtr)o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(o)!;
}
