using Xunit;

namespace TypingEngine.Windows.Tests;

/// <summary>
/// Témoins du mécanisme (a) de l'écart 5 (AG130-08, audit du 2026-09-20).
///
/// AltGr est détecté comme RAlt+LCtrl mais était émis comme un <c>VK_RMENU</c> nu :
/// <c>wScan = 0</c>, sans <c>KEYEVENTF_EXTENDEDKEY</c>. Or la vraie touche AltGr est une
/// touche étendue — préfixe 0xE0, scan code 0x38. Émise nue, elle est indiscernable
/// d'Alt gauche pour toute cible qui lit le scan code (GLFW, SDL, Unity), et sur AZERTY
/// c'est le chemin du « @ ».
///
/// La faute vaut au relâchement autant qu'à l'appui : relâcher un RAlt non étendu après
/// l'avoir enfoncé étendu laisse la cible croire RAlt toujours enfoncée.
///
/// ⚠️ Périmètre : ces témoins portent sur <c>BuildVkComboInputs</c>, le chemin d'émission
/// d'un caractère. <c>BuildAltCodeInputs</c> garde délibérément <c>wScan = 0</c> sur les
/// modificateurs physiques qu'il neutralise, parce que c'est ce qui les distingue du Alt
/// synthétique de la séquence Alt+code — <c>BuildAltCodeInputs_AltGrSimulated_ReleasesRCtrlAndLAlt</c>
/// en dépend.
/// </summary>
public class AltGrEmissionTests
{
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;

    private const ushort VK_RMENU = 0xA5;
    private const ushort VK_LMENU = 0xA4;
    private const ushort VK_LCONTROL = 0xA2;
    private const ushort VK_LSHIFT = 0xA0;

    private const ushort SC_ALT = 0x38;
    private const ushort SC_LCONTROL = 0x1D;
    private const ushort SC_LSHIFT = 0x2A;

    private static KeyMapper NewMapper(MockWin32Api mock) => new(new Layout(), mock);

    private static List<Win32.INPUT> ComboAltGr()
    {
        var km = NewMapper(new MockWin32Api());
        var list = new List<Win32.INPUT>();
        // VK 0x30 (« 0 »), scan 0x0B : sur AZERTY, AltGr+0 est le « @ ».
        km.BuildVkComboInputs(0x30, 0x0B, needsShift: false, needsAltGr: true,
            needsCtrl: false, needsAlt: false, IntPtr.Zero, list);
        return list;
    }

    /// <summary>L'appui d'AltGr porte le scan code de la touche physique, pas 0.</summary>
    [Fact]
    public void AltGr_EstEmisAvecSonScanCode()
    {
        var press = Assert.Single(ComboAltGr(),
            ev => ev.u.ki.wVk == VK_RMENU && (ev.u.ki.dwFlags & KEYEVENTF_KEYUP) == 0);

        Assert.Equal(SC_ALT, press.u.ki.wScan);
    }

    /// <summary>
    /// Sans le bit étendu, la cible lit Alt gauche : c'est le mécanisme (a) de l'écart 5.
    /// </summary>
    [Fact]
    public void AltGr_EstEmisCommeToucheEtendue()
    {
        var press = Assert.Single(ComboAltGr(),
            ev => ev.u.ki.wVk == VK_RMENU && (ev.u.ki.dwFlags & KEYEVENTF_KEYUP) == 0);

        Assert.True((press.u.ki.dwFlags & KEYEVENTF_EXTENDEDKEY) != 0,
            "AltGr doit porter KEYEVENTF_EXTENDEDKEY, sinon la cible voit Alt gauche");
    }

    /// <summary>
    /// Le relâchement doit être étendu lui aussi, sinon il vise Alt gauche et laisse
    /// la cible avec un AltGr qu'elle croit toujours enfoncé.
    /// </summary>
    [Fact]
    public void RelachementDAltGr_EstEtenduEtPorteSonScanCode()
    {
        var release = Assert.Single(ComboAltGr(),
            ev => ev.u.ki.wVk == VK_RMENU && (ev.u.ki.dwFlags & KEYEVENTF_KEYUP) != 0);

        Assert.Equal(SC_ALT, release.u.ki.wScan);
        Assert.True((release.u.ki.dwFlags & KEYEVENTF_EXTENDEDKEY) != 0,
            "le relachement d'AltGr doit etre etendu, sinon RAlt reste enfoncee dans la cible");
    }

    /// <summary>
    /// Témoin négatif : les modificateurs de gauche ne sont PAS des touches étendues.
    /// Leur coller le bit étendu les transformerait en leurs jumelles de droite — une
    /// régression symétrique de celle qu'on corrige.
    /// </summary>
    [Fact]
    public void ModificateursDeGauche_NeSontPasEtendus()
    {
        var mock = new MockWin32Api();
        var km = NewMapper(mock);
        var list = new List<Win32.INPUT>();

        km.BuildVkComboInputs(0x41, 0x1E, needsShift: true, needsAltGr: false,
            needsCtrl: true, needsAlt: true, IntPtr.Zero, list);

        foreach (var ev in list)
        {
            if (ev.u.ki.wVk is VK_LSHIFT or VK_LCONTROL or VK_LMENU)
            {
                Assert.True((ev.u.ki.dwFlags & KEYEVENTF_EXTENDEDKEY) == 0,
                    $"VK 0x{ev.u.ki.wVk:X2} est un modificateur de gauche : jamais etendu");
            }
        }
    }

    /// <summary>Chaque modificateur émis porte le scan code de sa propre touche.</summary>
    [Theory]
    [InlineData(VK_LSHIFT, SC_LSHIFT)]
    [InlineData(VK_LCONTROL, SC_LCONTROL)]
    [InlineData(VK_LMENU, SC_ALT)]
    public void ChaqueModificateur_PorteSonPropreScanCode(ushort vk, ushort scanAttendu)
    {
        var km = NewMapper(new MockWin32Api());
        var list = new List<Win32.INPUT>();

        km.BuildVkComboInputs(0x41, 0x1E, needsShift: true, needsAltGr: false,
            needsCtrl: true, needsAlt: true, IntPtr.Zero, list);

        var evenements = list.FindAll(ev => ev.u.ki.wVk == vk);
        Assert.NotEmpty(evenements);
        foreach (var ev in evenements)
            Assert.Equal(scanAttendu, ev.u.ki.wScan);
    }
}
