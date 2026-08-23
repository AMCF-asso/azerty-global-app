using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

public class KeyMapperMaintainableLayerTests : IDisposable
{
    private const uint SC_TRIGGER_ALPHABET = 0x2B;
    private const uint SC_A = 0x10;
    private const uint SC_SPACE = 0x39;
    private const uint SC_LSHIFT = 0x2A;
    private const uint VK_LSHIFT = 0xA0;
    private const uint VK_A = 0x41;
    private const uint VK_SPACE = 0x20;
    private static readonly short KeyDown = unchecked((short)0x8000);

    private static readonly string[] AllLayers = { "dk_greek", "dk_cyrillic", "dk_scientific" };

    private readonly string _tempDir;

    public KeyMapperMaintainableLayerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "AZGML_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        ConfigManager.OverrideConfigPathForTests(Path.Combine(_tempDir, "config.json"));
        ConfigManager.SetMaintainableLayersEnabled(true);
        ConfigManager.SetMaintainableGreekEnabled(true);
        ConfigManager.SetMaintainableCyrillicEnabled(true);
        ConfigManager.SetMaintainableScientificEnabled(true);
        ConfigManager.SetMaintainableDoubleTapMilliseconds(500);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    [Fact]
    public void GreekSimpleTap_PreservesOneShotBehavior()
    {
        var (mapper, mock, monitor) = CreateMapper();
        using (monitor)
        {
            TapGreekTrigger(mapper, mock);
            Assert.Equal("dk_greek", mapper.ActiveDeadKey);
            Assert.Equal(MaintainableLayerMode.OneShot, mapper.MaintainableLayerState.Mode);

            mock.SendInputCalls.Clear();
            Assert.True(mapper.ProcessKey(VK_A, SC_A, 0, true));
            AssertUnicode(mock, 'α');
            Assert.Null(mapper.ActiveDeadKey);
        }
    }

    [Fact]
    public void GreekHeld_MapsSeveralCharactersUntilRelease()
    {
        var (mapper, mock, monitor) = CreateMapper();
        using (monitor)
        {
            PressGreekTrigger(mapper, mock);
            mock.SendInputCalls.Clear();

            Assert.True(mapper.ProcessKey(VK_A, SC_A, 0, true));
            mapper.ProcessKey(VK_A, SC_A, 0, false);
            Assert.True(mapper.ProcessKey(VK_A, SC_A, 0, true));
            Assert.Equal(MaintainableLayerMode.Held, mapper.MaintainableLayerState.Mode);
            Assert.Equal(2, mock.SendInputCalls.Count);

            ReleaseGreekTrigger(mapper, mock);
            Assert.Equal(MaintainableLayerMode.Inactive, mapper.MaintainableLayerState.Mode);
        }
    }

    [Fact]
    public void GreekDoubleTap_LocksAndSpaceStaysNormal()
    {
        var (mapper, mock, monitor) = CreateMapper();
        using (monitor)
        {
            TapGreekTrigger(mapper, mock);
            TapGreekTrigger(mapper, mock);
            Assert.Equal(MaintainableLayerMode.Locked, mapper.MaintainableLayerState.Mode);

            mock.SendInputCalls.Clear();
            Assert.True(mapper.ProcessKey(VK_SPACE, SC_SPACE, 0, true));
            AssertUnicode(mock, ' ');
            Assert.Equal(MaintainableLayerMode.Locked, mapper.MaintainableLayerState.Mode);
        }
    }

    [Fact]
    public void GreekOneShot_SpaceStillProducesIsolatedMicroSign()
    {
        var (mapper, mock, monitor) = CreateMapper();
        using (monitor)
        {
            TapGreekTrigger(mapper, mock);
            mock.SendInputCalls.Clear();

            Assert.True(mapper.ProcessKey(VK_SPACE, SC_SPACE, 0, true));
            AssertUnicode(mock, 'µ');
            Assert.Equal(MaintainableLayerMode.Inactive, mapper.MaintainableLayerState.Mode);
        }
    }

    [Fact]
    public void SecureField_SuspendsLayerButKeepsConfiguration()
    {
        var (mapper, mock, monitor) = CreateMapper();
        using (monitor)
        {
            TapGreekTrigger(mapper, mock);
            TapGreekTrigger(mapper, mock);
            Assert.Equal(MaintainableLayerMode.Locked, mapper.MaintainableLayerState.Mode);

            mock.ScriptedSecureInput = true;
            monitor.Recompute();
            mapper.RefreshForegroundContext();
            Assert.True(mapper.AdvancedFeaturesSuppressed);
            Assert.Equal(MaintainableLayerMode.Inactive, mapper.MaintainableLayerState.Mode);

            mock.ScriptedSecureInput = false;
            monitor.Recompute();
            mapper.RefreshForegroundContext();
            Assert.Equal(MaintainableLayerMode.Locked, mapper.MaintainableLayerState.Mode);
        }
    }

    private static (KeyMapper Mapper, MockWin32Api Mock, ForegroundMonitor Monitor) CreateMapper()
    {
        var mock = new MockWin32Api
        {
            ScriptedProcessName = "notepad.exe",
            ScriptedFullPath = @"C:\Windows\notepad.exe",
            ScriptedModules = new[] { "kernel32.dll" },
            ScriptedPid = 4242,
            ScriptedProcessStartTime = 123456789
        };
        var monitor = new ForegroundMonitor(mock, IntPtr.Zero);
        var mapper = new KeyMapper(LayoutLoader.LoadFromResource(), mock);
        mapper.SetForegroundMonitor(monitor);
        // Le moteur ne lit aucune configuration : pousser les réglages comme le
        // fait TrayApplication au démarrage.
        mapper.ApplyMaintainableLayerSettings(true, AllLayers, 500);
        mock.SendInputCalls.Clear();
        return (mapper, mock, monitor);
    }

    private static void TapGreekTrigger(KeyMapper mapper, MockWin32Api mock)
    {
        PressGreekTrigger(mapper, mock);
        ReleaseGreekTrigger(mapper, mock);
    }

    private static void PressGreekTrigger(KeyMapper mapper, MockWin32Api mock)
    {
        mock.AsyncKeyStateScript[(int)VK_LSHIFT] = KeyDown;
        mapper.TrackModifiers(VK_LSHIFT, SC_LSHIFT, 0, true);
        Assert.True(mapper.ProcessKey(0xDC, SC_TRIGGER_ALPHABET, 0, true));
    }

    private static void ReleaseGreekTrigger(KeyMapper mapper, MockWin32Api mock)
    {
        Assert.True(mapper.ProcessKey(0xDC, SC_TRIGGER_ALPHABET, 0, false));
        mock.AsyncKeyStateScript[(int)VK_LSHIFT] = 0;
        mapper.TrackModifiers(VK_LSHIFT, SC_LSHIFT, 0, false);
    }

    private static void AssertUnicode(MockWin32Api mock, char expected)
    {
        Assert.Single(mock.SendInputCalls);
        Assert.Equal(expected, mock.SendInputCalls[0][0].u.ki.wScan);
        Assert.True((mock.SendInputCalls[0][0].u.ki.dwFlags & 0x0004) != 0);
    }
}
