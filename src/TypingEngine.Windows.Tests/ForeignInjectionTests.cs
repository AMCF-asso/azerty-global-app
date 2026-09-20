namespace TypingEngine.Windows.Tests;

/// <summary>
/// AG130-07 — une frappe injectée par un autre programme ne se remappe pas.
///
/// Le hook ne lisait que <c>LLKHF_EXTENDED</c> : AutoHotkey, le clavier visuel de Windows
/// et les outils d'accessibilité voyaient leurs frappes traitées comme physiques. Une macro
/// injecte le caractère qu'elle veut écrire, pas la touche qui l'aurait produit ; le clavier
/// visuel, lui, affiche la disposition native, si bien que remapper un clic ferait mentir
/// ses propres étiquettes.
///
/// Le marqueur de l'application reste la seule chose qui distingue nos injections des
/// autres — et la sortie est la même pour les deux, à un endroit différent du callback.
/// </summary>
public class ForeignInjectionTests
{
    private const uint LLKHF_EXTENDED = 0x01;
    private const uint LLKHF_INJECTED = 0x10;
    private static readonly IntPtr Marqueur = (IntPtr)unchecked((int)0xA1234567);

    [Fact]
    public void FrappePhysique_NEstPasUneInjection()
    {
        // dwExtraInfo à zéro, aucun bit d'injection : le cas de l'immense majorité
        // des frappes. Si ce témoin tombe, plus rien n'est remappé.
        Assert.False(KeyboardHook.IsForeignInjection(IntPtr.Zero, flags: 0, Marqueur));
        Assert.False(KeyboardHook.IsForeignInjection(IntPtr.Zero, LLKHF_EXTENDED, Marqueur));
    }

    [Fact]
    public void InjectionDUnTiers_EstÉcartée()
    {
        // AutoHotkey, clavier visuel, lecteur d'écran : peu importe qui, aucun flag Win32
        // ne le dit et la réponse est la même pour tous.
        Assert.True(KeyboardHook.IsForeignInjection(IntPtr.Zero, LLKHF_INJECTED, Marqueur));
        Assert.True(KeyboardHook.IsForeignInjection(IntPtr.Zero, LLKHF_INJECTED | LLKHF_EXTENDED, Marqueur));
    }

    [Fact]
    public void InjectionDUnTiersAvecUnAutreMarqueur_EstÉcartée()
    {
        // Un autre outil peut poser son propre dwExtraInfo. Ce n'est pas le nôtre,
        // donc ce n'est pas nous : la frappe passe.
        var sien = (IntPtr)unchecked((int)0xB0000001);
        Assert.True(KeyboardHook.IsForeignInjection(sien, LLKHF_INJECTED, Marqueur));
    }

    [Fact]
    public void NotreProprePInjection_NEstPasÉtrangère()
    {
        // ⛔ Le cas qui casserait tout : nos émissions portent LLKHF_INJECTED comme les
        // autres. Les confondre avec une injection étrangère ferait sortir le callback
        // au mauvais endroit — avant le traitement des touches mortes et du Verr.Maj
        // intelligent, qui reposent sur nos propres injections marquées.
        Assert.False(KeyboardHook.IsForeignInjection(Marqueur, LLKHF_INJECTED, Marqueur));
        Assert.False(KeyboardHook.IsForeignInjection(Marqueur, LLKHF_INJECTED | LLKHF_EXTENDED, Marqueur));
    }

    [Fact]
    public void LeMarqueurSeulSansBitDInjection_NeSuffitPas()
    {
        // Un tiers qui devinerait le marqueur ne contourne pas la garde par ce chemin :
        // sans LLKHF_INJECTED, l'événement se dit physique et suit la route physique.
        // (Le marqueur est de toute façon tiré au hasard à chaque démarrage — SEV-A3-01.)
        Assert.False(KeyboardHook.IsForeignInjection(Marqueur, flags: 0, Marqueur));
    }

    [Fact]
    public void LeBitLuEstBien0x10()
    {
        // Barrer la constante : 0x01 est LLKHF_EXTENDED, 0x02 LLKHF_LOWER_IL_INJECTED,
        // 0x20 LLKHF_ALTDOWN, 0x80 LLKHF_UP. Se tromper de bit rendrait la garde muette
        // ou, pire, écarterait les frappes étendues (flèches, pavé numérique).
        Assert.False(KeyboardHook.IsForeignInjection(IntPtr.Zero, 0x01, Marqueur));
        Assert.False(KeyboardHook.IsForeignInjection(IntPtr.Zero, 0x02, Marqueur));
        Assert.False(KeyboardHook.IsForeignInjection(IntPtr.Zero, 0x20, Marqueur));
        Assert.False(KeyboardHook.IsForeignInjection(IntPtr.Zero, 0x80, Marqueur));
        Assert.True(KeyboardHook.IsForeignInjection(IntPtr.Zero, 0x10, Marqueur));
    }
}
