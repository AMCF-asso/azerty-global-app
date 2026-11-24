using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// CI rouge de la PR 26 (17/11/2026) : <c>TroncatureLibellesTests</c> en et fr à 192 DPI échouait sur le
/// runner (« demande 1007 px, dispose de 916 px ») et passait sur le poste d'Antoine. Windows plafonne
/// une fenêtre de premier niveau à l'écran réel (écran + cadre) : sur le runner, 1024 × 768, l'Accueil
/// porté à 192 DPI sous une zone de travail simulée de 3840 px gardait 1028 px de zone cliente au lieu
/// de 1440. Ce test rejoue ce plafond sur n'importe quel poste.
/// </summary>
public sealed class EcranReelPetitTests
{
    private static Win32.RECT ZoneSimulée => new() { left = 0, top = 0, right = 3840, bottom = 2160 - 96 };

    [Fact]
    public void L_Accueil_a_192_DPI_prend_sa_taille_meme_sur_un_ecran_reel_de_1024_x_768()
    {
        using var isolation = new BancCapture.Isolation();
        isolation.SetLanguage("en");
        using var zone = NativeWindow.OverrideWorkAreaForTests(ZoneSimulée);
        var accueil = new OnboardingWindow();
        try
        {
            IntPtr hwnd = BancCapture.Handle(accueil);
            BancCapture.SimulatePhysicalScreen(hwnd, 1024, 768); // le plafond du runner
            BancCapture.ApplyDpi(hwnd, 192);

            Win32.GetClientRect(hwnd, out var client);
            int voulue = (int)(960 * BancCapture.Field<float>(accueil, "_dpiScale") * 0.75f);
            Assert.Equal(2f, BancCapture.Field<float>(accueil, "_dpiScale"));
            Assert.True(client.right - client.left >= voulue,
                $"zone cliente de {client.right - client.left} px pour {voulue} voulus : "
                + "Windows plafonne la fenêtre à l'écran réel, le banc doit relever ce plafond");
        }
        finally { BancCapture.Teardown(accueil); }
    }

    [Fact]
    public void Sans_zone_simulee_le_plafond_de_Windows_reste_celui_de_l_ecran()
    {
        // Le relèvement ne vaut que sous une zone simulée : une fenêtre qui n'en a pas garde le
        // plafond de Windows (les bancs de capture et les tests de défilement en dépendent).
        using var isolation = new BancCapture.Isolation();
        isolation.SetLanguage("en");
        var accueil = new OnboardingWindow();
        try
        {
            IntPtr hwnd = BancCapture.Handle(accueil);
            BancCapture.AllowSimulatedScreenSize(hwnd);
            Win32.GetWindowRect(hwnd, out var avant);
            Win32.MoveWindow(hwnd, 0, 0, 20000, 900, true);
            Win32.GetWindowRect(hwnd, out var après);
            Assert.True(après.right - après.left < 20000, "sans zone simulée, Windows plafonne la fenêtre à l'écran");
            Assert.True(avant.right - avant.left > 0);
        }
        finally { BancCapture.Teardown(accueil); }
    }
}
