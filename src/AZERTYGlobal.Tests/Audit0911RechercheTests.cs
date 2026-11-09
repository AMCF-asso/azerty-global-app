using System.Text;
using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Audit UX du 09/11, Recherche de caractère : W9 (Alt+F4 la détruisait), W10 (Entrée sans résultat
/// ne disait rien), E27 (la requête et ses résultats survivent à une perte de focus).
/// Vraie fenêtre, isolée et invisible par <see cref="BancCapture.Isolation"/>.
/// </summary>
public sealed class Audit0911RechercheTests : IDisposable
{
    private const uint WM_KEYDOWN = 0x0100;
    private const uint WM_ACTIVATE = 0x0006;
    private const int VK_RETURN = 0x0D;
    private readonly string _languageOrigin = L.Language;

    public void Dispose() => L.Language = _languageOrigin;

    private static CharacterSearch Create() =>
        new(new TextInsertionService(_ => default, _ => false, () => false));

    private static string TextOf(IntPtr hwnd)
    {
        var buffer = new StringBuilder(Math.Max(16, Win32.GetWindowTextLengthW(hwnd) + 1));
        Win32.GetWindowTextW(hwnd, buffer, buffer.Capacity);
        return buffer.ToString();
    }

    [Fact]
    public void AltF4_MasqueLaFenêtre_SansLaDétruire()
    {
        using var isolation = new BancCapture.Isolation();
        var search = Create();
        try
        {
            IntPtr hwnd = BancCapture.Handle(search);
            search.Show();

            Win32.SendMessageW(hwnd, Win32.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);

            Assert.True(Win32.IsWindow(hwnd), "WM_CLOSE a détruit la fenêtre : la recherche serait morte jusqu'au redémarrage");
            Assert.False(search.IsVisible);
            // Elle se rouvre.
            search.Show();
            Assert.True(Win32.IsWindow(BancCapture.Handle(search)));
        }
        finally { BancCapture.Teardown(search); }
    }

    [Fact]
    public void Entrée_ChampVide_DitPourquoiRienNeSePasse()
    {
        using var isolation = new BancCapture.Isolation();
        var search = Create();
        try
        {
            IntPtr edit = BancCapture.Field<IntPtr>(search, "_hEdit");
            Win32.SendMessageW(edit, WM_KEYDOWN, (IntPtr)VK_RETURN, IntPtr.Zero);
            Assert.True(BancCapture.Field<bool>(search, "_enterFoundNothing"));
            Assert.Equal(L.Search_EnterNothingEmpty, (string)BancCapture.Call(search, "NothingToInsertMessage")!);
        }
        finally { BancCapture.Teardown(search); }
    }

    [Fact]
    public void Entrée_RequêteSansRésultat_DitAutreChose_EtLaFrappeSuivanteEfface()
    {
        using var isolation = new BancCapture.Isolation();
        var search = Create();
        try
        {
            IntPtr edit = BancCapture.Field<IntPtr>(search, "_hEdit");
            Win32.SetWindowTextW(edit, "qzxwvjkqzxwv");
            Win32.SendMessageW(edit, WM_KEYDOWN, (IntPtr)VK_RETURN, IntPtr.Zero);
            Assert.True(BancCapture.Field<bool>(search, "_enterFoundNothing"));
            Assert.Equal(L.Search_EnterNothingNoResults, (string)BancCapture.Call(search, "NothingToInsertMessage")!);

            Win32.SetWindowTextW(edit, "e"); // une frappe : EN_CHANGE relance la recherche
            Assert.False(BancCapture.Field<bool>(search, "_enterFoundNothing"));
        }
        finally { BancCapture.Teardown(search); }
    }

    [Fact]
    public void Entrée_SurUnRésultat_NeDitPasRien()
    {
        using var isolation = new BancCapture.Isolation();
        var search = Create();
        try
        {
            IntPtr edit = BancCapture.Field<IntPtr>(search, "_hEdit");
            Win32.SetWindowTextW(edit, "e");
            Win32.SendMessageW(edit, WM_KEYDOWN, (IntPtr)VK_RETURN, IntPtr.Zero);
            Assert.False(BancCapture.Field<bool>(search, "_enterFoundNothing"));
        }
        finally { BancCapture.Teardown(search); }
    }

    [Fact]
    public void PerteDeFocus_LaRequêteEtLesRésultatsSurvivent_Réouverture()
    {
        using var isolation = new BancCapture.Isolation();
        var search = Create();
        try
        {
            IntPtr hwnd = BancCapture.Handle(search);
            IntPtr edit = BancCapture.Field<IntPtr>(search, "_hEdit");
            search.Show();
            Win32.SetWindowTextW(edit, "e");
            int results = BancCapture.Field<System.Collections.IList>(search, "_filteredResults").Count;
            Assert.True(results > 0);

            Win32.SendMessageW(hwnd, WM_ACTIVATE, IntPtr.Zero, IntPtr.Zero); // WA_INACTIVE
            Assert.False(search.IsVisible);

            search.Show();
            Assert.Equal("e", TextOf(edit));
            Assert.Equal(results, BancCapture.Field<System.Collections.IList>(search, "_filteredResults").Count);
        }
        finally { BancCapture.Teardown(search); }
    }
}
