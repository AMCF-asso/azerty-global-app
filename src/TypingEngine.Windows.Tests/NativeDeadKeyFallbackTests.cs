using Xunit;

namespace TypingEngine.Windows.Tests;

/// <summary>
/// Témoin du § 3.4 de la revue de code du 2026-09-21.
///
/// <c>KeyMapper.BuildNativeComboInputs</c> renonce à la combo native quand la touche du
/// layout sous-jacent est elle-même une touche morte — « ^ », « ¨ », « ~ », « ` » sur un
/// AZERTY traditionnel : envoyer la combo ferait entrer Windows en composition et le
/// caractère ne s'afficherait pas. L'appelant retombe alors sur Alt+code, qui contourne la
/// composition. C'est le repli du geste 19 de la recette VM.
///
/// ⛔ Ce repli n'avait **aucune** couverture dans sa branche utile : <c>MockWin32Api</c>
/// faisait répondre 1 à <c>ToUnicodeEx</c> en toutes circonstances, donc
/// <c>IsDeadKeyOnLayout</c> ne pouvait jamais valoir vrai sous test. Le mock sait désormais
/// déclarer des touches mortes natives (<c>NativeDeadKeys</c>), et ces témoins jouent les
/// deux côtés de la décision.
/// </summary>
public class NativeDeadKeyFallbackTests
{
    private static readonly IntPtr Hkl = (IntPtr)0x040C0000; // AZERTY traditionnel
    private const byte VK_CIRCONFLEXE = 0xDD;
    private const byte VK_Q = 0x41;

    /// <summary>Layout natif où « ^ » est une touche morte et « q » une lettre ordinaire.</summary>
    private static MockWin32Api AzertyTraditionnel()
    {
        var api = new MockWin32Api { CurrentHkl = Hkl };
        api.VkKeyScanScript[('^', Hkl)] = VK_CIRCONFLEXE;
        api.VkKeyScanScript[('q', Hkl)] = VK_Q;
        api.MapVirtualKeyScript[(VK_CIRCONFLEXE, 0u, Hkl)] = 0x1A;
        api.MapVirtualKeyScript[(VK_Q, 0u, Hkl)] = 0x1E;
        api.NativeDeadKeys.Add(VK_CIRCONFLEXE);
        return api;
    }

    private static KeyMapper Mapper(MockWin32Api api) => new(new Layout(), api);

    /// <summary>
    /// ⛔ La branche que la suite ne pouvait pas atteindre : la combo native est refusée,
    /// et aucun INPUT n'est produit — c'est ce refus qui fait basculer l'appelant sur
    /// Alt+code.
    /// </summary>
    [Fact]
    public void Une_touche_morte_du_layout_natif_refuse_la_combo_native()
    {
        var api = AzertyTraditionnel();
        var inputs = new List<Win32.INPUT>();

        bool construite = Mapper(api).BuildNativeComboInputs('^', Hkl, inputs);

        Assert.False(construite);
        Assert.Empty(inputs);
    }

    /// <summary>
    /// ⛔ La réciproque obligatoire : sans elle, un <c>BuildNativeComboInputs</c> qui
    /// refuserait **tout** rendrait le témoin ci-dessus vert pour la mauvaise raison — et
    /// le produit enverrait tout en Alt+code sans que rien ne rougisse.
    /// </summary>
    [Fact]
    public void Un_caractere_ordinaire_accepte_la_combo_native()
    {
        var api = AzertyTraditionnel();
        var inputs = new List<Win32.INPUT>();

        bool construite = Mapper(api).BuildNativeComboInputs('q', Hkl, inputs);

        Assert.True(construite);
        Assert.NotEmpty(inputs);
    }

    /// <summary>
    /// Le drapeau 0x04 est ce qui empêche l'interrogation de perturber l'état de composition
    /// du système : sans lui, demander « est-ce une touche morte ? » en armerait une.
    /// </summary>
    [Fact]
    public void L_interrogation_ne_perturbe_pas_l_etat_de_composition()
    {
        var api = AzertyTraditionnel();

        Mapper(api).BuildNativeComboInputs('^', Hkl, new List<Win32.INPUT>());

        Assert.All(api.ToUnicodeExFlags, flags => Assert.Equal(0x04u, flags & 0x04u));
    }

    /// <summary>
    /// La réponse est mise en cache par (vk, mods, hkl) : une frappe répétée n'interroge
    /// pas Windows à chaque fois. Le cache se mesure au nombre d'appels, la seule trace
    /// qu'en laisse le mock.
    /// </summary>
    [Fact]
    public void La_reponse_est_mise_en_cache()
    {
        var api = AzertyTraditionnel();
        var km = Mapper(api);

        km.BuildNativeComboInputs('^', Hkl, new List<Win32.INPUT>());
        int apresLaPremiere = api.ToUnicodeExFlags.Count;
        km.BuildNativeComboInputs('^', Hkl, new List<Win32.INPUT>());

        Assert.True(apresLaPremiere > 0, "la première frappe n'a pas interrogé le layout");
        Assert.Equal(apresLaPremiere, api.ToUnicodeExFlags.Count);
    }
}
