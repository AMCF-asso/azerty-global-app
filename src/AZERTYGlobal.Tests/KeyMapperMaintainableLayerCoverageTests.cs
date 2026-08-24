using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Couverture complémentaire des couches maintenables : cyrillique et scientifique,
/// consommation du modificateur d'activation, raccourcis Ctrl/Alt/Win, changement
/// et disparition de processus, PID réutilisé, champs sécurisés, et invariance
/// des 26 autres touches mortes.
/// </summary>
public class KeyMapperMaintainableLayerCoverageTests : IDisposable
{
    private const uint SC_TRIGGER_ALPHABET = 0x2B;  // SC02B — Maj = grec, AltGr = cyrillique
    private const uint SC_TRIGGER_SCIENTIFIC = 0x0D; // SC00D — AltGr = scientifique
    private const uint SC_A = 0x10;
    private const uint SC_LSHIFT = 0x2A;
    private const uint SC_RALT = 0x38;
    private const uint SC_LALT = 0x38;
    private const uint SC_BACKSPACE = 0x0E;
    private const uint VK_TRIGGER_ALPHABET = 0xDC;  // VK_OEM_5
    private const uint VK_TRIGGER_SCIENTIFIC = 0xBB; // VK_OEM_PLUS
    private const uint VK_LSHIFT = 0xA0;
    private const uint VK_RMENU = 0xA5;
    private const uint VK_LMENU = 0xA4;
    private const uint VK_LWIN = 0x5B;
    private const uint VK_A = 0x41;
    private const uint KEYEVENTF_UNICODE = 0x0004;
    private static readonly short KeyDown = unchecked((short)0x8000);

    private static readonly string[] AllLayers = { "dk_greek", "dk_cyrillic", "dk_scientific" };

    private readonly string _tempDir;

    public KeyMapperMaintainableLayerCoverageTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "AZGMLC_" + Guid.NewGuid().ToString("N"));
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

    // ── Couches cyrillique et scientifique ──────────────────────────

    [Fact]
    public void CyrillicTap_ProducesCyrillicCharacter()
    {
        var (mapper, mock, monitor) = CreateMapper();
        using (monitor)
        {
            TapAltGrTrigger(mapper, mock, VK_TRIGGER_ALPHABET, SC_TRIGGER_ALPHABET);
            Assert.Equal("dk_cyrillic", mapper.MaintainableLayerState.LayerId);
            Assert.Equal(MaintainableLayerMode.OneShot, mapper.MaintainableLayerState.Mode);

            mock.SendInputCalls.Clear();
            Assert.True(mapper.ProcessKey(VK_A, SC_A, 0, true));
            AssertUnicode(mock, 'а'); // CYRILLIC SMALL LETTER A
        }
    }

    [Fact]
    public void ScientificHeld_MapsOnlyTheFirstSymbol()
    {
        var (mapper, mock, monitor) = CreateMapper();
        using (monitor)
        {
            // AltGr + = maintenu, puis frappe de 'a' → ∠ (accord = appui simple)
            PressAltGrTrigger(mapper, mock, VK_TRIGGER_SCIENTIFIC, SC_TRIGGER_SCIENTIFIC);
            mock.SendInputCalls.Clear();

            Assert.True(mapper.ProcessKey(VK_A, SC_A, 0, true));
            AssertUnicode(mock, '∠'); // ANGLE

            // Consommé par sa propre frappe : rien ne colle au déclencheur enfoncé.
            Assert.Equal(MaintainableLayerMode.Inactive, mapper.MaintainableLayerState.Mode);

            ReleaseAltGrTrigger(mapper, mock, VK_TRIGGER_SCIENTIFIC, SC_TRIGGER_SCIENTIFIC);
            Assert.Equal(MaintainableLayerMode.Inactive, mapper.MaintainableLayerState.Mode);
        }
    }

    // ── Modificateur d'activation consommé, nouveau Shift, Verr. Maj. ──

    [Fact]
    public void ActivationShift_IsConsumedWhileHeld()
    {
        var (mapper, mock, monitor) = CreateMapper();
        using (monitor)
        {
            // Maj reste enfoncée après l'activation : la couche sort la minuscule.
            mock.AsyncKeyStateScript[(int)VK_LSHIFT] = KeyDown;
            mapper.TrackModifiers(VK_LSHIFT, SC_LSHIFT, 0, true);
            Assert.True(mapper.ProcessKey(VK_TRIGGER_ALPHABET, SC_TRIGGER_ALPHABET, 0, true));

            mock.SendInputCalls.Clear();
            Assert.True(mapper.ProcessKey(VK_A, SC_A, 0, true));
            AssertUnicode(mock, 'α'); // pas Α : le Shift d'activation est consommé
        }
    }

    [Fact]
    public void NewShift_AfterActivation_ProducesUppercase()
    {
        var (mapper, mock, monitor) = CreateMapper();
        using (monitor)
        {
            TapShiftTrigger(mapper, mock); // one-shot grec, Shift relâché

            // Nouveau Shift : la couche doit produire la majuscule.
            mock.AsyncKeyStateScript[(int)VK_LSHIFT] = KeyDown;
            mapper.TrackModifiers(VK_LSHIFT, SC_LSHIFT, 0, true);
            mock.SendInputCalls.Clear();
            Assert.True(mapper.ProcessKey(VK_A, SC_A, 0, true));
            AssertUnicode(mock, 'Α'); // GREEK CAPITAL ALPHA
        }
    }

    [Fact]
    public void CapsLock_ProducesUppercaseInLayer()
    {
        var mock = NewForegroundMock();
        mock.KeyStateScript[0x14] = 1; // Verr. Maj. actif avant création
        using var monitor = new ForegroundMonitor(mock, IntPtr.Zero);
        var mapper = new KeyMapper(LayoutLoader.LoadFromResource(), mock);
        mapper.SetForegroundMonitor(monitor);
        mapper.ApplyMaintainableLayerSettings(true, AllLayers, 500);

        TapShiftTrigger(mapper, mock);
        mock.SendInputCalls.Clear();
        Assert.True(mapper.ProcessKey(VK_A, SC_A, 0, true));
        AssertUnicode(mock, 'Α');
    }

    // ── Raccourcis Ctrl / Alt / Windows pendant une couche verrouillée ──

    [Fact]
    public void CtrlShortcut_BypassesLockedLayer()
    {
        var (mapper, mock, monitor) = CreateMapper();
        using (monitor)
        {
            LockGreek(mapper, mock);

            mock.AsyncKeyStateScript[0xA2] = KeyDown; // VK_LCONTROL
            mapper.TrackModifiers(0xA2, 0x1D, 0, true);
            mock.SendInputCalls.Clear();

            Assert.True(mapper.ProcessKey(VK_A, SC_A, 0, true));

            // Ctrl+A part en virtual key (raccourci préservé), jamais en Unicode α.
            Assert.Single(mock.SendInputCalls);
            Assert.Equal((ushort)VK_A, mock.SendInputCalls[0][0].u.ki.wVk);
            Assert.Equal(0u, mock.SendInputCalls[0][0].u.ki.dwFlags & KEYEVENTF_UNICODE);
            Assert.Equal(MaintainableLayerMode.Locked, mapper.MaintainableLayerState.Mode);
        }
    }

    [Fact]
    public void AltShortcut_BypassesLockedLayer()
    {
        var (mapper, mock, monitor) = CreateMapper();
        using (monitor)
        {
            LockGreek(mapper, mock);

            mock.AsyncKeyStateScript[(int)VK_LMENU] = KeyDown;
            mapper.TrackModifiers(VK_LMENU, SC_LALT, 0, true);
            mock.SendInputCalls.Clear();

            // Alt+A : pass-through complet (menus, Alt+Tab…), aucun caractère émis.
            Assert.False(mapper.ProcessKey(VK_A, SC_A, 0, true));
            Assert.Empty(mock.SendInputCalls);
            Assert.Equal(MaintainableLayerMode.Locked, mapper.MaintainableLayerState.Mode);
        }
    }

    [Fact]
    public void WinShortcut_BypassesLockedLayer()
    {
        var (mapper, mock, monitor) = CreateMapper();
        using (monitor)
        {
            LockGreek(mapper, mock);

            // Win physiquement enfoncée : le hook la signale via TrackModifiers,
            // et l'état physique doit suivre — CleanupStaleModifiers relit
            // GetAsyncKeyState et remettrait un Win non scripté à zéro.
            mock.AsyncKeyStateScript[(int)VK_LWIN] = KeyDown;
            mapper.TrackModifiers(VK_LWIN, 0, 0, true);
            mock.SendInputCalls.Clear();

            // Win+A doit passer au système, pas produire α.
            Assert.False(mapper.ProcessKey(VK_A, SC_A, 0, true));
            Assert.Empty(mock.SendInputCalls);
            Assert.Equal(MaintainableLayerMode.Locked, mapper.MaintainableLayerState.Mode);

            mock.AsyncKeyStateScript[(int)VK_LWIN] = 0;
            mapper.TrackModifiers(VK_LWIN, 0, 0, false);
        }
    }

    // ── Changement, fermeture de processus, PID réutilisé ───────────

    [Fact]
    public void ProcessSwitch_SuspendsThenRestoresLock()
    {
        var (mapper, mock, monitor) = CreateMapper();
        using (monitor)
        {
            LockGreek(mapper, mock);

            // Passage à un autre processus : verrou invisible, frappes ordinaires.
            mock.ScriptedPid = 777;
            monitor.Recompute();
            mapper.RefreshForegroundContext();
            Assert.Equal(MaintainableLayerMode.Inactive, mapper.MaintainableLayerState.Mode);

            // Retour au processus d'origine : verrou restauré.
            mock.ScriptedPid = 4242;
            monitor.Recompute();
            mapper.RefreshForegroundContext();
            Assert.Equal(MaintainableLayerMode.Locked, mapper.MaintainableLayerState.Mode);
            Assert.Equal("dk_greek", mapper.MaintainableLayerState.LayerId);
        }
    }

    [Fact]
    public void DeadProcess_LockIsDropped()
    {
        var (mapper, mock, monitor) = CreateMapper();
        using (monitor)
        {
            LockGreek(mapper, mock);

            // Le processus disparaît : GetProcessTimes échoue, le verrou est purgé.
            mock.ScriptedProcessStartTime = 0;
            monitor.Recompute();
            mapper.RefreshForegroundContext();

            // Même identité revenue : le verrou n'existe plus.
            mock.ScriptedProcessStartTime = 123456789;
            monitor.Recompute();
            mapper.RefreshForegroundContext();
            Assert.Equal(MaintainableLayerMode.Inactive, mapper.MaintainableLayerState.Mode);
        }
    }

    [Fact]
    public void ReusedPid_DoesNotInheritLock()
    {
        var (mapper, mock, monitor) = CreateMapper();
        using (monitor)
        {
            LockGreek(mapper, mock);

            // Même PID, nouvel instant de création : autre instance du processus.
            mock.ScriptedProcessStartTime = 555_000_000;
            monitor.Recompute();
            mapper.RefreshForegroundContext();
            Assert.Equal(MaintainableLayerMode.Inactive, mapper.MaintainableLayerState.Mode);

            mock.SendInputCalls.Clear();
            Assert.False(mapper.ProcessKey(VK_A, SC_A, 0, true)); // pass-through ordinaire
            Assert.Empty(mock.SendInputCalls);
        }
    }

    // ── Champs sécurisés ────────────────────────────────────────────

    [Fact]
    public void SecureInput_KeepsOrdinaryRemappingAndDeadKeys()
    {
        var (mapper, mock, monitor) = CreateMapper();
        using (monitor)
        {
            mock.ScriptedSecureInput = true;
            monitor.Recompute();
            mapper.RefreshForegroundContext();
            Assert.True(mapper.AdvancedFeaturesSuppressed);

            // Maj+* redevient la touche morte ponctuelle classique (comportement
            // d'avant la fonctionnalité) : le remappage ordinaire est conservé.
            mock.AsyncKeyStateScript[(int)VK_LSHIFT] = KeyDown;
            mapper.TrackModifiers(VK_LSHIFT, SC_LSHIFT, 0, true);
            Assert.True(mapper.ProcessKey(VK_TRIGGER_ALPHABET, SC_TRIGGER_ALPHABET, 0, true));
            mapper.ProcessKey(VK_TRIGGER_ALPHABET, SC_TRIGGER_ALPHABET, 0, false);
            mock.AsyncKeyStateScript[(int)VK_LSHIFT] = 0;
            mapper.TrackModifiers(VK_LSHIFT, SC_LSHIFT, 0, false);

            Assert.Equal(MaintainableLayerMode.Inactive, mapper.MaintainableLayerState.Mode);
            Assert.Equal("dk_greek", mapper.ActiveDeadKey); // touche morte classique

            mock.SendInputCalls.Clear();
            Assert.True(mapper.ProcessKey(VK_A, SC_A, 0, true));
            AssertUnicode(mock, 'α');
            Assert.Null(mapper.ActiveDeadKey);
        }
    }

    // ── Les 26 autres touches mortes restent inchangées ─────────────

    [Fact]
    public void OtherDeadKeys_KeepClassicBehavior()
    {
        var (mapper, mock, monitor) = CreateMapper();
        using (monitor)
        {
            var layout = LayoutLoader.LoadFromResource();
            var others = layout.DeadKeys.Keys
                .Where(dk => !AllLayers.Contains(dk))
                .ToList();
            Assert.Equal(26, others.Count);

            foreach (var dkName in others)
            {
                var access = FindKeyProducing(layout, dkName);
                Assert.True(access != null, $"{dkName} inaccessible depuis le layout");
                var (scanCode, shift, altGr) = access!.Value;

                if (shift)
                {
                    mock.AsyncKeyStateScript[(int)VK_LSHIFT] = KeyDown;
                    mapper.TrackModifiers(VK_LSHIFT, SC_LSHIFT, 0, true);
                }
                if (altGr)
                {
                    mock.AsyncKeyStateScript[(int)VK_RMENU] = KeyDown;
                    mapper.TrackModifiers(VK_RMENU, SC_RALT, 0, true);
                }

                Assert.True(mapper.ProcessKey(0xDC, scanCode, 0, true), dkName);
                Assert.Equal(dkName, mapper.ActiveDeadKey);
                Assert.Equal(MaintainableLayerMode.Inactive, mapper.MaintainableLayerState.Mode);
                mapper.ProcessKey(0xDC, scanCode, 0, false);

                if (shift)
                {
                    mock.AsyncKeyStateScript[(int)VK_LSHIFT] = 0;
                    mapper.TrackModifiers(VK_LSHIFT, SC_LSHIFT, 0, false);
                }
                if (altGr)
                {
                    mock.AsyncKeyStateScript[(int)VK_RMENU] = 0;
                    mapper.TrackModifiers(VK_RMENU, SC_RALT, 0, false);
                }

                // Backspace annule la touche morte pour l'itération suivante.
                Assert.False(mapper.ProcessKey(0x08, SC_BACKSPACE, 0, true));
                Assert.Null(mapper.ActiveDeadKey);
            }
        }
    }

    // ── Migration de configuration ──────────────────────────────────

    [Fact]
    public void LegacyConfig_MigratesWithLayersDisabled()
    {
        var legacyPath = Path.Combine(_tempDir, "legacy-config.json");
        File.WriteAllText(legacyPath, "{\"notificationsEnabled\":true,\"launchCount\":12}");
        ConfigManager.OverrideConfigPathForTests(legacyPath);

        Assert.False(ConfigManager.MaintainableLayersEnabled); // opt-in volontaire
        Assert.True(ConfigManager.MaintainableGreekEnabled);   // pré-sélection inerte
        Assert.True(ConfigManager.MaintainableCyrillicEnabled);
        Assert.True(ConfigManager.MaintainableScientificEnabled);
        Assert.InRange(ConfigManager.MaintainableDoubleTapMilliseconds, 150, 1000);
        Assert.True(ConfigManager.MaintainableVisualFeedbackEnabled);
    }

    // ── Helpers ─────────────────────────────────────────────────────

    private static MockWin32Api NewForegroundMock() => new()
    {
        ScriptedProcessName = "notepad.exe",
        ScriptedFullPath = @"C:\Windows\notepad.exe",
        ScriptedModules = new[] { "kernel32.dll" },
        ScriptedPid = 4242,
        ScriptedProcessStartTime = 123456789
    };

    private static (KeyMapper Mapper, MockWin32Api Mock, ForegroundMonitor Monitor) CreateMapper()
    {
        var mock = NewForegroundMock();
        var monitor = new ForegroundMonitor(mock, IntPtr.Zero);
        var mapper = new KeyMapper(LayoutLoader.LoadFromResource(), mock);
        mapper.SetForegroundMonitor(monitor);
        // Le moteur ne lit aucune configuration : pousser les réglages comme le
        // fait TrayApplication au démarrage.
        mapper.ApplyMaintainableLayerSettings(true, AllLayers, 500);
        mock.SendInputCalls.Clear();
        return (mapper, mock, monitor);
    }

    private static void LockGreek(KeyMapper mapper, MockWin32Api mock)
    {
        TapShiftTrigger(mapper, mock);
        TapShiftTrigger(mapper, mock);
        Assert.Equal(MaintainableLayerMode.Locked, mapper.MaintainableLayerState.Mode);
    }

    private static void TapShiftTrigger(KeyMapper mapper, MockWin32Api mock)
    {
        mock.AsyncKeyStateScript[(int)VK_LSHIFT] = KeyDown;
        mapper.TrackModifiers(VK_LSHIFT, SC_LSHIFT, 0, true);
        Assert.True(mapper.ProcessKey(VK_TRIGGER_ALPHABET, SC_TRIGGER_ALPHABET, 0, true));
        Assert.True(mapper.ProcessKey(VK_TRIGGER_ALPHABET, SC_TRIGGER_ALPHABET, 0, false));
        mock.AsyncKeyStateScript[(int)VK_LSHIFT] = 0;
        mapper.TrackModifiers(VK_LSHIFT, SC_LSHIFT, 0, false);
    }

    private static void TapAltGrTrigger(KeyMapper mapper, MockWin32Api mock, uint vk, uint scanCode)
    {
        PressAltGrTrigger(mapper, mock, vk, scanCode);
        ReleaseAltGrTrigger(mapper, mock, vk, scanCode);
    }

    private static void PressAltGrTrigger(KeyMapper mapper, MockWin32Api mock, uint vk, uint scanCode)
    {
        mock.AsyncKeyStateScript[(int)VK_RMENU] = KeyDown;
        mapper.TrackModifiers(VK_RMENU, SC_RALT, 0, true);
        Assert.True(mapper.ProcessKey(vk, scanCode, 0, true));
    }

    private static void ReleaseAltGrTrigger(KeyMapper mapper, MockWin32Api mock, uint vk, uint scanCode)
    {
        Assert.True(mapper.ProcessKey(vk, scanCode, 0, false));
        mock.AsyncKeyStateScript[(int)VK_RMENU] = 0;
        mapper.TrackModifiers(VK_RMENU, SC_RALT, 0, false);
    }

    private static (uint ScanCode, bool Shift, bool AltGr)? FindKeyProducing(Layout layout, string dkName)
    {
        foreach (var (scanCode, keyDef) in layout.Keys)
        {
            foreach (var (shift, altGr) in new[]
                     { (false, false), (true, false), (false, true), (true, true) })
            {
                if (keyDef.GetOutput(shift, altGr, capsLock: false) == dkName)
                    return (scanCode, shift, altGr);
            }
        }
        return null;
    }

    private static void AssertUnicode(MockWin32Api mock, char expected)
    {
        Assert.Single(mock.SendInputCalls);
        Assert.Equal(expected, mock.SendInputCalls[0][0].u.ki.wScan);
        Assert.True((mock.SendInputCalls[0][0].u.ki.dwFlags & KEYEVENTF_UNICODE) != 0);
    }
}
