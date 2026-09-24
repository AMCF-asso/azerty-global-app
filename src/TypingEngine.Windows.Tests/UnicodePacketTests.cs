namespace TypingEngine.Windows.Tests;

/// <summary>
/// Audit 24/09 B1 — un VK_PACKET ne se remappe jamais.
///
/// VK_PACKET (0xE7) porte un caractère Unicode déjà décidé : Espanso, AutoHotkey
/// <c>SendText</c>, un gestionnaire de mots de passe, la dictée, une session RDP. Son
/// scanCode contient ce caractère, pas une position de touche. Depuis C5, un hôte distant
/// détecté faisait traiter ces injections comme physiques : « . » (U+002E) se lisait comme
/// la touche C (SC 0x2E). Le hook le laisse désormais passer avant tout traitement.
/// </summary>
public class UnicodePacketTests
{
    private const uint LLKHF_EXTENDED = 0x01;
    private const uint LLKHF_INJECTED = 0x10;
    private const uint VkA = 0x41;
    private static readonly IntPtr Marqueur = (IntPtr)unchecked((int)0xA1234567);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void VkPacket_PasseToujours_AvecOuSansHoteDistant(bool remoteHostPresent)
    {
        // Le cas du constat : injection étrangère, dwExtraInfo à 0, hôte distant présent.
        Assert.False(KeyboardHook.ShouldProcessKeystroke(KeyboardHook.VK_PACKET, IntPtr.Zero, LLKHF_INJECTED, Marqueur, remoteHostPresent));
        Assert.False(KeyboardHook.ShouldProcessKeystroke(KeyboardHook.VK_PACKET, IntPtr.Zero, LLKHF_INJECTED | LLKHF_EXTENDED, Marqueur, remoteHostPresent));
        // Sans bit d'injection non plus : le scanCode reste un caractère.
        Assert.False(KeyboardHook.ShouldProcessKeystroke(KeyboardHook.VK_PACKET, IntPtr.Zero, flags: 0, Marqueur, remoteHostPresent));
    }

    [Fact]
    public void ToucheOrdinaireInjectée_SurHoteDistant_ResteTraitée()
    {
        // ⛔ C5 ne doit pas régresser : les frappes Parsec arrivent injectées avec les
        // scan codes physiques du client, et doivent toujours être remappées.
        Assert.True(KeyboardHook.ShouldProcessKeystroke(VkA, IntPtr.Zero, LLKHF_INJECTED, Marqueur, remoteHostPresent: true));
        Assert.True(KeyboardHook.ShouldProcessKeystroke(VkA, IntPtr.Zero, LLKHF_INJECTED | LLKHF_EXTENDED, Marqueur, remoteHostPresent: true));
    }

    [Fact]
    public void ToucheOrdinaire_SuitShouldTreatAsPhysical()
    {
        // Hors VK_PACKET, la décision reste celle d'AG130-07 revu.
        Assert.True(KeyboardHook.ShouldProcessKeystroke(VkA, IntPtr.Zero, flags: 0, Marqueur, remoteHostPresent: false));
        Assert.False(KeyboardHook.ShouldProcessKeystroke(VkA, IntPtr.Zero, LLKHF_INJECTED, Marqueur, remoteHostPresent: false));
        Assert.False(KeyboardHook.ShouldProcessKeystroke(VkA, Marqueur, LLKHF_INJECTED, Marqueur, remoteHostPresent: true));
    }

    [Fact]
    public void LaConstanteEstBien0xE7()
    {
        // Les voisins 0xE6 (OEM) et 0xE8 (non attribué) ne sont pas des paquets Unicode.
        Assert.Equal(0xE7u, KeyboardHook.VK_PACKET);
        Assert.True(KeyboardHook.ShouldProcessKeystroke(0xE6, IntPtr.Zero, flags: 0, Marqueur, remoteHostPresent: false));
        Assert.True(KeyboardHook.ShouldProcessKeystroke(0xE8, IntPtr.Zero, flags: 0, Marqueur, remoteHostPresent: false));
    }
}
