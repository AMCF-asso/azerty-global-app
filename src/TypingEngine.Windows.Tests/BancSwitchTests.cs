namespace TypingEngine.Windows.Tests;

/// <summary>
/// D12 (tests autonomes Windows-MCP, 2026-11-04) — interrupteur de banc.
///
/// Le banc tape par SendInput dans une Windows Sandbox ; ces frappes portent LLKHF_INJECTED et
/// AG130-07 les écarte. L'interrupteur les fait remapper, mais seulement dans une build de
/// configuration Banc ET avec la valeur HKCU à 1. Ces témoins fixent les deux verrous ; le
/// trajet dans le rappel du hook est éprouvé dans HookCallbackTests.
///
/// Aucun témoin n'écrit dans le registre : la lecture réelle est éprouvée sur des valeurs que
/// Windows publie sous HKLM, lisibles par tout compte.
/// </summary>
public class BancSwitchTests
{
#if AG_BANC_TEST
    private const bool BuildBanc = true;
#else
    private const bool BuildBanc = false;
#endif

    private const string CleWindows = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";

    [Fact]
    public void Constante_SuitLaConfiguration()
    {
        // ⛔ En Release et en Debug, faux. Rougit si AG_BANC_TEST fuit hors de la
        // configuration Banc du moteur : l'interrupteur serait alors dans le produit.
        Assert.Equal(BuildBanc, BancSwitch.CompiledIn);
    }

    [Theory]
    [InlineData(true, 1, true)]
    [InlineData(true, null, false)]
    [InlineData(true, 0, false)]
    [InlineData(true, 2, false)]
    [InlineData(true, -1, false)]
    [InlineData(false, 1, false)]
    [InlineData(false, null, false)]
    [InlineData(false, 0, false)]
    public void Decide_Matrice(bool compile, int? valeur, bool attendu)
    {
        Assert.Equal(attendu, BancSwitch.Decide(compile, valeur));
    }

    [Fact]
    public void NonCompile_LaValeurNEstJamaisLue()
    {
        int lectures = 0;
        var etat = BancSwitch.Evaluate(false, () => { lectures++; return 1; });
        Assert.Equal(0, lectures);
        Assert.Equal(new BancSwitch.State(false, false, null), etat);
    }

    [Fact]
    public void Compile_LaValeurDecide()
    {
        Assert.Equal(new BancSwitch.State(true, true, 1), BancSwitch.Evaluate(true, () => 1));
        Assert.Equal(new BancSwitch.State(true, false, null), BancSwitch.Evaluate(true, () => null));
        Assert.Equal(new BancSwitch.State(true, false, 0), BancSwitch.Evaluate(true, () => 0));
    }

    [Fact]
    public void Journal_DistingueNonCompileDeValeurNonVue()
    {
        Assert.Equal("compiled=0 valeur=absente-ou-non-dword active=0", new BancSwitch.State(false, false, null).Describe());
        Assert.Equal("compiled=1 valeur=absente-ou-non-dword active=0", new BancSwitch.State(true, false, null).Describe());
        Assert.Equal("compiled=1 valeur=1 active=1", new BancSwitch.State(true, true, 1).Describe());
        Assert.Equal("compiled=1 valeur=2 active=0", new BancSwitch.State(true, false, 2).Describe());
    }

    [Fact]
    public void LectureAuDemarrage_HorsBanc_RienNEstLu()
    {
        // Hors build Banc, rien n'est lu : l'état est celui du non-compilé, quels que soient
        // les arguments. En build Banc, seul CompiledIn est fixé ici (la clé du banc est
        // absente du poste de test) ; la lecture HKCU réelle s'éprouve dans la Sandbox.
        var etat = BancSwitch.ReadAtStartup(CleWindows, "CurrentMajorVersionNumber");
        Assert.Equal(BuildBanc, etat.CompiledIn);
        if (!BuildBanc)
            Assert.Equal(new BancSwitch.State(false, false, null), etat);
    }

    [Fact]
    public void LectureReelle_DwordPresent_AbsentEtAutreType()
    {
        // Windows 10 et 11 publient CurrentMajorVersionNumber (DWORD) et CurrentVersion
        // (REG_SZ, « 6.3 ») sous HKLM.
        var majeure = BancSwitch.ReadDword(BancSwitch.HKEY_LOCAL_MACHINE, CleWindows, "CurrentMajorVersionNumber");
        Assert.NotNull(majeure);
        Assert.True(majeure >= 10);
        Assert.Null(BancSwitch.ReadDword(BancSwitch.HKEY_LOCAL_MACHINE, CleWindows, "CurrentVersion"));
        Assert.Null(BancSwitch.ReadDword(BancSwitch.HKEY_LOCAL_MACHINE, CleWindows, "AZERTYGlobalValeurQuiNExistePas"));
        Assert.Null(BancSwitch.ReadDword(BancSwitch.HKEY_CURRENT_USER, @"Software\AZERTYGlobalCleQuiNExistePas", "InjectionPhysique"));
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    public void InjectionEtrangere_FrappeUtilisateur_SessionDistanteOuBanc(bool sessionDistante, bool banc, bool attendu)
    {
        Assert.Equal(attendu, KeyboardHook.ForeignInjectionIsUserInput(sessionDistante, banc));
    }
}
