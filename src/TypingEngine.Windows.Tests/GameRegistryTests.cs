using Xunit;

namespace TypingEngine.Windows.Tests;

public class GameRegistryTests
{
    // ────────────────────────────────────────────────────────────────
    // IsAntiCheatProcess — niveau 1 sous-chaîne
    // ────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("valorant.exe")]
    [InlineData("VALORANT-Win64-Shipping.exe")]   // sous-chaîne dans nom long
    [InlineData("VALORANT.exe")]                   // casse différente
    [InlineData("FortniteClient-Win64-Shipping.exe")]
    [InlineData("r5apex.exe")]
    [InlineData("r5apex_dx12.exe")]
    [InlineData("BlackOps6.exe")]
    [InlineData("RobloxPlayerBeta.exe")]
    [InlineData("GenshinImpact.exe")]
    [InlineData("EscapeFromTarkov.exe")]
    [InlineData("cs2.exe")]
    [InlineData("CS2.EXE")]
    public void IsAntiCheatProcess_Level1_Substring_Returns_True(string processName)
    {
        Assert.True(GameRegistry.IsAntiCheatProcess(processName, null));
    }

    // ────────────────────────────────────────────────────────────────
    // IsAntiCheatProcess — niveau 1 match exact (cod.exe)
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public void IsAntiCheatProcess_CodExe_Returns_True()
    {
        Assert.True(GameRegistry.IsAntiCheatProcess("cod.exe", @"C:\Games\CallOfDuty\cod.exe"));
        Assert.True(GameRegistry.IsAntiCheatProcess("COD.EXE", null)); // casse insensible
    }

    [Fact]
    public void IsAntiCheatProcess_CodVariant_Returns_False()
    {
        // « cod » seul ne match pas en sous-chaîne. Seul `cod.exe` exact match.
        Assert.False(GameRegistry.IsAntiCheatProcess("hot-cod-recipe.exe", null));
    }

    // ────────────────────────────────────────────────────────────────
    // IsAntiCheatProcess — ace.exe avec/sans \Tencent\ dans le chemin
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public void IsAntiCheatProcess_AceExe_TencentPath_Returns_True()
    {
        Assert.True(GameRegistry.IsAntiCheatProcess(
            "ace.exe", @"C:\Program Files\Tencent\AntiCheat\ace.exe"));
    }

    [Fact]
    public void IsAntiCheatProcess_AceExe_OtherPath_Returns_False()
    {
        // Faux positif évité : ace.exe d'un autre éditeur n'est pas détecté
        Assert.False(GameRegistry.IsAntiCheatProcess(
            "ace.exe", @"C:\Program Files\SomeUtility\ace.exe"));
    }

    [Fact]
    public void IsAntiCheatProcess_AceExe_NoPath_Returns_False()
    {
        // Sans chemin, on ne peut pas confirmer que c'est Tencent ACE
        Assert.False(GameRegistry.IsAntiCheatProcess("ace.exe", null));
    }

    // ────────────────────────────────────────────────────────────────
    // IsAntiCheatProcess — The Finals (Discovery-d.exe / Discovery-e.exe / Discovery.exe + \Embark\)
    // ────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Discovery-d.exe", null)]      // post-patch 8.0.0 DAC, match exact
    [InlineData("Discovery-e.exe", null)]      // post-patch 8.0.0 EAC, match exact
    [InlineData("Discovery.exe", @"C:\Program Files\Embark\TheFinals\Discovery.exe")] // pre-patch 8.0.0 + path Embark
    public void IsAntiCheatProcess_TheFinals_KnownExe_Returns_True(string proc, string? path)
    {
        Assert.True(GameRegistry.IsAntiCheatProcess(proc, path));
    }

    [Fact]
    public void IsAntiCheatProcess_DiscoveryExe_OtherPath_Returns_False()
    {
        // Discovery.exe sans path Embark → faux positif evite
        Assert.False(GameRegistry.IsAntiCheatProcess(
            "Discovery.exe", @"C:\Tools\Discovery\Discovery.exe"));
    }

    // ────────────────────────────────────────────────────────────────
    // IsAntiCheatProcess — niveau 2
    // ────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("destiny2.exe")]
    [InlineData("RustClient.exe")]
    [InlineData("FC26.exe")]
    [InlineData("HaloInfinite.exe")]
    public void IsAntiCheatProcess_Level2_Returns_True(string processName)
    {
        Assert.True(GameRegistry.IsAntiCheatProcess(processName, null));
    }

    // ────────────────────────────────────────────────────────────────
    // IsAntiCheatProcess — ajouts 1.3.0 (2026-10-23), un exe réel par jeu
    // (sources : operations/2026-10-23-anti-triche-131/tableau.md du dossier projet)
    // ────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("GTA5.exe")]                          // GTA V Legacy
    [InlineData("GTA5_Enhanced.exe")]                 // GTA V Enhanced (et FiveM Enhanced, même nom)
    [InlineData("GTA5_BE.exe")]                       // installeur BattlEye de GTA V
    [InlineData("PioneerGame.exe")]                   // ARC Raiders
    [InlineData("DayZ_x64.exe")]
    [InlineData("arma3_x64.exe")]
    [InlineData("ArmaReforgerSteam.exe")]
    [InlineData("DuneSandbox-Win64-Shipping.exe")]    // Dune: Awakening
    [InlineData("nightreign.exe")]                    // Elden Ring Nightreign
    [InlineData("Brawlhalla.exe")]
    [InlineData("LOSTARK.exe")]
    [InlineData("NewWorld.exe")]
    [InlineData("MapleStory.exe")]
    [InlineData("BPSR_STEAM.exe")]                    // Blue Protocol: Star Resonance
    [InlineData("Blue Protocol Star Resonance.exe")]
    [InlineData("ZenlessZoneZero.exe")]
    [InlineData("X6Game-Win64-Shipping.exe")]         // Infinity Nikki
    public void IsAntiCheatProcess_Ajouts130_Niveau1_Returns_True(string processName)
    {
        Assert.True(GameRegistry.IsAntiCheatProcess(processName, null));
    }

    [Theory]
    [InlineData("bf6.exe", null)]                     // Battlefield 6
    [InlineData("BFV.exe", null)]                     // Battlefield V, casse différente
    [InlineData("bf1.exe", null)]                     // Battlefield 1
    [InlineData("aces.exe", null)]                    // War Thunder
    [InlineData("BH3.exe", null)]                     // Honkai Impact 3rd
    [InlineData("UAGame.exe", null)]                  // Arena Breakout: Infinite
    [InlineData("Hemingway-Win64-Shipping.exe", null)] // SMITE 2
    [InlineData("TL.exe", @"C:\Program Files (x86)\Steam\steamapps\common\Throne and Liberty\TL\Binaries\Win64\TL.exe")]
    [InlineData("Client-Win64-Shipping.exe", @"D:\Wuthering Waves\Wuthering Waves Game\Client\Binaries\Win64\Client-Win64-Shipping.exe")]
    public void IsAntiCheatProcess_Ajouts130_MatchExact_Returns_True(string proc, string? path)
    {
        Assert.True(GameRegistry.IsAntiCheatProcess(proc, path));
    }

    [Theory]
    [InlineData("TL.exe", null)]                      // sans chemin, rien ne prouve Throne and Liberty
    [InlineData("TL.exe", @"C:\Tools\TL.exe")]
    [InlineData("Client-Win64-Shipping.exe", null)]   // nom générique d'Unreal Engine
    [InlineData("Client-Win64-Shipping.exe", @"C:\Games\AutreJeu\Client\Binaries\Win64\Client-Win64-Shipping.exe")]
    [InlineData("a1bf6c.exe", null)]                  // nom hexadécimal : bf6 n'est pas une sous-chaîne
    [InlineData("Hemingway Editor.exe", null)]        // éditeur de texte, pas SMITE 2
    public void IsAntiCheatProcess_Ajouts130_FauxPositifs_Returns_False(string proc, string? path)
    {
        Assert.False(GameRegistry.IsAntiCheatProcess(proc, path));
    }

    [Theory]
    [InlineData("eldenring.exe")]
    [InlineData("armoredcore6.exe")]
    [InlineData("BlackDesert64.exe")]
    [InlineData("PredecessorClient-Win64-Shipping.exe")]
    [InlineData("Paladins.exe")]
    [InlineData("DeadByDaylight-Win64-Shipping.exe")]
    [InlineData("HuntGame.exe")]                      // Hunt: Showdown 1896
    [InlineData("SquadGame.exe")]
    [InlineData("HLL-Win64-Shipping.exe")]            // Hell Let Loose
    [InlineData("HellLetLoose.exe")]
    [InlineData("InsurgencyClient-Win64-Shipping.exe")]
    [InlineData("SoTGame.exe")]                       // Sea of Thieves
    [InlineData("RocketLeague.exe")]
    [InlineData("RocketLeague_EAC.exe")]
    [InlineData("GZWClientSteam-Win64-Shipping.exe")] // Gray Zone Warfare
    [InlineData("mcc-win64-shipping.exe")]            // Halo: MCC
    [InlineData("TheDivision2.exe")]
    [InlineData("forhonor.exe")]
    [InlineData("MechaBreak.exe")]
    [InlineData("ONCE_HUMAN.exe")]
    [InlineData("HottaGame.exe")]                     // Tower of Fantasy
    [InlineData("ArkAscended_BE.exe")]                // ARK: Survival Ascended, seul nom attesté
    [InlineData("SparkingZero-Win64-Shipping.exe")]
    public void IsAntiCheatProcess_Ajouts130_Niveau2_Returns_True(string processName)
    {
        Assert.True(GameRegistry.IsAntiCheatProcess(processName, null));
    }

    // ────────────────────────────────────────────────────────────────
    // IsAntiCheatProcess — faux positifs à éviter
    // ────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("notepad.exe")]
    [InlineData("chrome.exe")]
    [InlineData("Code.exe")]
    [InlineData("explorer.exe")]
    [InlineData("Discord.exe")]
    [InlineData("steam.exe")]
    [InlineData("EpicGamesLauncher.exe")]
    [InlineData("Battle.net.exe")]
    [InlineData("WINWORD.EXE")]
    [InlineData("ms-teams.exe")]
    [InlineData("obs64.exe")]
    [InlineData("Spotify.exe")]
    [InlineData("")]
    public void IsAntiCheatProcess_Common_Apps_Returns_False(string processName)
    {
        Assert.False(GameRegistry.IsAntiCheatProcess(processName, @"C:\Path\" + processName));
    }

    [Fact]
    public void IsAntiCheatProcess_Null_Returns_False()
    {
        Assert.False(GameRegistry.IsAntiCheatProcess(null, null));
    }

    [Theory]
    [InlineData("parsecd.exe")]
    [InlineData("mstsc.exe")]
    [InlineData("msrdc.exe")]
    [InlineData("msrdcw.exe")]
    [InlineData("AnyDesk.exe")]
    [InlineData("TeamViewer.exe")]
    [InlineData("rustdesk.exe")]
    [InlineData("vmconnect.exe")]
    [InlineData("WindowsSandbox.exe")]
    [InlineData("WindowsSandboxClient.exe")]
    [InlineData("windowssandboxclient.exe")]
    [InlineData("WindowsSandboxRemoteSession.exe")]
    [InlineData("vmware.exe")]
    [InlineData("vmplayer.exe")]
    [InlineData("VirtualBoxVM.exe")]
    [InlineData("wfica32.exe")]
    [InlineData("CDViewer.exe")]
    [InlineData("vmware-remotemks.exe")]
    public void IsRemoteAccessProcess_KnownClient_ReturnsTrue(string processName)
    {
        Assert.True(GameRegistry.IsRemoteAccessProcess(processName));
    }

    // Gestionnaires et services de VM : on y tape du texte ordinaire (noms, chemins), le
    // remappage doit y rester actif ; seule la fenêtre de la console transmet des scan codes.
    [Theory]
    [InlineData("VirtualBox.exe")]
    [InlineData("vmware-vmx.exe")]
    [InlineData("vmms.exe")]
    [InlineData("notepad.exe")]
    [InlineData(null)]
    public void IsRemoteAccessProcess_VmManagerOrOther_ReturnsFalse(string? processName)
    {
        Assert.False(GameRegistry.IsRemoteAccessProcess(processName));
    }

    // ── Session de prise en main à distance (C5 du 24/09, revue le 30/10 : audit M1) ──

    private const int SessionUtilisateur = 1;
    private const int SessionServices = 0;

    private static GameRegistry.ProcessEntry P(string name, int session = SessionUtilisateur) => new(name, session);

    [Fact]
    public void RemoteSessionMarkers_SontLesSignauxDeSessionDecides()
    {
        Assert.Equal(
            new[] { "remoting_desktop.exe", "TeamViewer_Desktop.exe", "parsecd.exe" },
            GameRegistry.RemoteSessionMarkers.Select(m => m.Process));
    }

    [Fact]
    public void RemoteSessionMarkers_ExcluentServicesEtClientsRdp()
    {
        // Un service tourne dès l'installation (M1) ; l'entrée RDP côté hôte n'est pas injectée.
        foreach (var name in new[] { "TeamViewer_Service.exe", "TeamViewer.exe", "pservice.exe", "AnyDesk.exe",
                                     "rustdesk.exe", "remoting_host.exe", "mstsc.exe", "msrdc.exe", "msrdcw.exe" })
            Assert.DoesNotContain(GameRegistry.RemoteSessionMarkers,
                m => string.Equals(m.Process, name, StringComparison.OrdinalIgnoreCase));
    }

    public static IEnumerable<object[]> ServicesLancesSansSession() => new[]
    {
        new object[] { "TeamViewer", new[] { P("TeamViewer_Service", SessionServices), P("TeamViewer") } },
        new object[] { "AnyDesk (service)", new[] { P("AnyDesk", SessionServices), P("AnyDesk") } },
        new object[] { "Parsec (service seul)", new[] { P("pservice", SessionServices) } },
        new object[] { "Chrome Remote Desktop", new[] { P("remoting_host", SessionServices) } },
        new object[] { "RustDesk", new[] { P("rustdesk", SessionServices), P("rustdesk") } },
    };

    [Theory]
    [MemberData(nameof(ServicesLancesSansSession))]
    public void FindActiveRemoteSession_ServiceLanceSansSession_RendNull(string outil, GameRegistry.ProcessEntry[] hote)
    {
        var processus = new[] { P("explorer"), P("KeePass"), P("svchost", SessionServices) }.Concat(hote);
        Assert.True(GameRegistry.FindActiveRemoteSession(processus, SessionUtilisateur) == null, outil);
    }

    public static IEnumerable<object[]> SessionsOuvertes() => new[]
    {
        new object[] { "TeamViewer", new[] { P("TeamViewer_Service", SessionServices), P("TeamViewer"), P("TeamViewer_Desktop") } },
        new object[] { "Chrome Remote Desktop", new[] { P("remoting_host", SessionServices), P("remoting_desktop") } },
        new object[] { "Parsec", new[] { P("pservice", SessionServices), P("parsecd") } },
    };

    [Theory]
    [MemberData(nameof(SessionsOuvertes))]
    public void FindActiveRemoteSession_SessionOuverte_RendLOutil(string outil, GameRegistry.ProcessEntry[] hote)
    {
        var processus = new[] { P("explorer"), P("svchost", SessionServices) }.Concat(hote);
        Assert.Equal(outil, GameRegistry.FindActiveRemoteSession(processus, SessionUtilisateur));
    }

    [Theory]
    [InlineData("TeamViewer_Desktop")]      // Process.ProcessName, sans extension
    [InlineData("REMOTING_DESKTOP.EXE")]
    public void FindActiveRemoteSession_NomAvecOuSansExtension_SansCasse(string name)
    {
        Assert.NotNull(GameRegistry.FindActiveRemoteSession(new[] { P(name) }, SessionUtilisateur));
    }

    [Fact]
    public void FindActiveRemoteSession_MarqueurDUneAutreSession_RendNull()
    {
        // Session d'un autre utilisateur, ou session 0 : ses injections n'atteignent pas notre bureau.
        Assert.Null(GameRegistry.FindActiveRemoteSession(new[] { P("TeamViewer_Desktop", 2) }, SessionUtilisateur));
        Assert.Null(GameRegistry.FindActiveRemoteSession(new[] { P("parsecd", SessionServices) }, SessionUtilisateur));
    }

    [Fact]
    public void FindActiveRemoteSession_EntreesVides_RendNull()
    {
        Assert.Null(GameRegistry.FindActiveRemoteSession(null, SessionUtilisateur));
        Assert.Null(GameRegistry.FindActiveRemoteSession(Array.Empty<GameRegistry.ProcessEntry>(), SessionUtilisateur));
        Assert.Null(GameRegistry.FindActiveRemoteSession(
            new[] { new GameRegistry.ProcessEntry(null, SessionUtilisateur), P("") }, SessionUtilisateur));
    }

    [Fact]
    public void FindActiveRemoteSession_EnumerationReelle_NeLeveJamais()
    {
        // Énumération réelle : la valeur dépend du poste, seule l'absence d'exception compte.
        _ = GameRegistry.FindActiveRemoteSession();
    }

    // ────────────────────────────────────────────────────────────────
    // HasGameFrameworkLoaded
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public void HasGameFrameworkLoaded_GlfwPresent_Returns_True()
    {
        var modules = new[] { "kernel32.dll", "user32.dll", "glfw3.dll" };
        Assert.True(GameRegistry.HasGameFrameworkLoaded(modules));
    }

    [Fact]
    public void HasGameFrameworkLoaded_LwjglMinecraft_Returns_True()
    {
        var modules = new[] { "javaw.exe", "lwjgl_glfw.dll", "opengl32.dll" };
        Assert.True(GameRegistry.HasGameFrameworkLoaded(modules));
    }

    [Fact]
    public void HasGameFrameworkLoaded_UnityPlayer_Returns_True()
    {
        var modules = new[] { "kernel32.dll", "UnityPlayer.dll" }; // casse différente
        Assert.True(GameRegistry.HasGameFrameworkLoaded(modules));
    }

    [Fact]
    public void HasGameFrameworkLoaded_OnlyStandardDlls_Returns_False()
    {
        // D3D11/OpenGL ne suffisent pas (Notepad, Chrome les chargent aussi)
        var modules = new[] { "kernel32.dll", "user32.dll", "d3d11.dll", "opengl32.dll" };
        Assert.False(GameRegistry.HasGameFrameworkLoaded(modules));
    }

    [Fact]
    public void HasGameFrameworkLoaded_Empty_Returns_False()
    {
        Assert.False(GameRegistry.HasGameFrameworkLoaded(Array.Empty<string>()));
        Assert.False(GameRegistry.HasGameFrameworkLoaded(null));
    }
}
