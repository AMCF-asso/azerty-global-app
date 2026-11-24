using Xunit;

namespace TypingEngine.Windows.Tests;

/// <summary>
/// Audit UX du 09/11, E13 (« Ctrl gauche + Alt gauche pris pour Alt Gr »). Sous Windows, Alt Gr se signale
/// comme Ctrl+Alt : la distinction se fait par le bit étendu (Alt droit étendu, précédé d'un Ctrl gauche
/// fantôme non étendu) et par Ctrl droit + Alt gauche (Alt Gr simulé, Bureau à distance). Un Ctrl gauche
/// et un Alt gauche physiques sont un raccourci, jamais un Alt Gr.
/// </summary>
public class AltGrDistinctionTests
{
    private const uint VK_LCONTROL = 0xA2, VK_RCONTROL = 0xA3, VK_LMENU = 0xA4, VK_RMENU = 0xA5;
    private const uint SC_CTRL = 0x1D, SC_ALT = 0x38;
    private const uint LLKHF_EXTENDED = 0x01;

    private static KeyMapper NouveauMapper() => new(new Layout(), new MockWin32Api());

    [Fact]
    public void CtrlGaucheEtAltGauche_SontUnRaccourci_PasAltGr()
    {
        var mapper = NouveauMapper();
        mapper.TrackModifiers(VK_LCONTROL, SC_CTRL, 0, true);
        mapper.TrackModifiers(VK_LMENU, SC_ALT, 0, true);

        Assert.True(mapper.CtrlDown);
        Assert.True(mapper.AltDown);
        Assert.False(mapper.AltGrDown);
    }

    [Fact]
    public void AltDroitEtendu_AvecCtrlGaucheFantome_EstAltGr()
    {
        var mapper = NouveauMapper();
        mapper.TrackModifiers(VK_LCONTROL, SC_CTRL, 0, true);
        mapper.TrackModifiers(VK_RMENU, SC_ALT, LLKHF_EXTENDED, true);

        Assert.True(mapper.AltGrDown);
    }

    [Fact]
    public void CtrlDroitEtAltGauche_EstAltGrSimule()
    {
        var mapper = NouveauMapper();
        mapper.TrackModifiers(VK_RCONTROL, SC_CTRL, LLKHF_EXTENDED, true);
        mapper.TrackModifiers(VK_LMENU, SC_ALT, 0, true);

        Assert.True(mapper.AltGrDown);
    }

    [Fact]
    public void LeCtrlEtendu_NEstJamaisLeCtrlGauche_PuisAltGauche_RestePasAltGr()
    {
        // VK_LCONTROL porté par une touche étendue (Ctrl droit signalé comme gauche) : ignoré, comme avant.
        var mapper = NouveauMapper();
        mapper.TrackModifiers(VK_LCONTROL, SC_CTRL, LLKHF_EXTENDED, true);
        mapper.TrackModifiers(VK_LMENU, SC_ALT, 0, true);

        Assert.False(mapper.AltGrDown);
    }

    [Fact]
    public void RelacherAltDroit_FinitAltGr_MemeSiLeCtrlFantomeResteTenu()
    {
        var mapper = NouveauMapper();
        mapper.TrackModifiers(VK_LCONTROL, SC_CTRL, 0, true);
        mapper.TrackModifiers(VK_RMENU, SC_ALT, LLKHF_EXTENDED, true);
        mapper.TrackModifiers(VK_RMENU, SC_ALT, LLKHF_EXTENDED, false);

        Assert.False(mapper.AltGrDown);
    }
}
