using Xunit;

namespace TypingEngine.Windows.Tests;

/// <summary>
/// Machine d'état pure des couches maintenables : modes ponctuel et verrou par
/// processus, accord consommé en appui simple, fenêtre de double appui, champs
/// sécurisés, PID réutilisé.
/// </summary>
public class MaintainableLayerManagerTests
{
    private static readonly ForegroundProcessIdentity ProcessA = new(101, 1001);
    private static readonly ForegroundProcessIdentity ProcessB = new(202, 2002);

    private long _now = 1_000;

    private MaintainableLayerManager CreateEnabledManager()
    {
        var manager = new MaintainableLayerManager(() => _now);
        manager.ApplySettings(true, MaintainableLayerManager.SupportedLayers, 400);
        manager.SetForeground(ProcessA, secureInput: false);
        return manager;
    }

    [Fact]
    public void SimpleTap_CreatesOneShot()
    {
        var manager = CreateEnabledManager();

        Assert.True(manager.BeginTrigger("dk_greek", 0x2B));
        Assert.True(manager.EndTrigger(0x2B));

        Assert.Equal(MaintainableLayerMode.OneShot, manager.CurrentState.Mode);
        Assert.Equal("dk_greek", manager.CurrentState.LayerId);
    }

    [Fact]
    public void Chord_ArmsOneShotConsumedByItsOwnKeystroke()
    {
        var manager = CreateEnabledManager();

        manager.BeginTrigger("dk_greek", 0x2B);
        Assert.True(manager.PromotePendingTriggerToOneShot());
        Assert.Equal(MaintainableLayerMode.OneShot, manager.CurrentState.Mode);

        // La frappe de l'accord consomme le one-shot ; tant que le déclencheur
        // reste enfoncé, les frappes suivantes redeviennent ordinaires.
        manager.ConsumeOneShot();
        Assert.False(manager.PromotePendingTriggerToOneShot());
        Assert.Equal(MaintainableLayerMode.Inactive, manager.CurrentState.Mode);

        manager.EndTrigger(0x2B);
        Assert.Equal(MaintainableLayerMode.Inactive, manager.CurrentState.Mode);
    }

    [Fact]
    public void Chord_UnusedOneShotDoesNotSurviveTriggerRelease()
    {
        var manager = CreateEnabledManager();

        manager.BeginTrigger("dk_greek", 0x2B);
        Assert.True(manager.PromotePendingTriggerToOneShot());

        // La frappe de l'accord est partie en raccourci (Ctrl, Alt…) : le one-shot
        // n'a jamais servi et ne doit pas surprendre la frappe suivante.
        manager.EndTrigger(0x2B);
        Assert.Equal(MaintainableLayerMode.Inactive, manager.CurrentState.Mode);
    }

    [Fact]
    public void DoubleTap_LocksCurrentProcess()
    {
        var manager = CreateEnabledManager();

        Tap(manager, "dk_cyrillic", 0x2B);
        _now += 200;
        Tap(manager, "dk_cyrillic", 0x2B);

        Assert.Equal(MaintainableLayerMode.Locked, manager.CurrentState.Mode);
        Assert.Equal("dk_cyrillic", manager.CurrentState.LayerId);
    }

    [Fact]
    public void TapAfterDoubleTapWindow_RemainsOneShot()
    {
        var manager = CreateEnabledManager();

        Tap(manager, "dk_scientific", 0x0D);
        _now += 401;
        Tap(manager, "dk_scientific", 0x0D);

        Assert.Equal(MaintainableLayerMode.OneShot, manager.CurrentState.Mode);
    }

    [Fact]
    public void SameLayerTap_Unlocks()
    {
        var manager = CreateEnabledManager();
        Lock(manager, "dk_greek", 0x2B);

        _now += 500;
        Tap(manager, "dk_greek", 0x2B);

        Assert.Equal(MaintainableLayerMode.Inactive, manager.CurrentState.Mode);
    }

    [Fact]
    public void OtherLayerChord_OneShotOverridesLockThenReturnsToIt()
    {
        var manager = CreateEnabledManager();
        Lock(manager, "dk_greek", 0x2B);

        manager.BeginTrigger("dk_scientific", 0x0D);
        manager.PromotePendingTriggerToOneShot();
        Assert.Equal("dk_scientific", manager.CurrentState.LayerId);
        Assert.Equal(MaintainableLayerMode.OneShot, manager.CurrentState.Mode);

        manager.ConsumeOneShot();
        manager.EndTrigger(0x0D);
        Assert.Equal("dk_greek", manager.CurrentState.LayerId);
        Assert.Equal(MaintainableLayerMode.Locked, manager.CurrentState.Mode);
    }

    [Fact]
    public void OtherLayerDoubleTap_ReplacesLockedLayer()
    {
        var manager = CreateEnabledManager();
        Lock(manager, "dk_greek", 0x2B);

        _now += 500;
        Tap(manager, "dk_scientific", 0x0D);
        _now += 100;
        Tap(manager, "dk_scientific", 0x0D);

        Assert.Equal("dk_scientific", manager.CurrentState.LayerId);
        Assert.Equal(MaintainableLayerMode.Locked, manager.CurrentState.Mode);
    }

    [Fact]
    public void Lock_IsRestoredWhenReturningToSameProcess()
    {
        var manager = CreateEnabledManager();
        Lock(manager, "dk_greek", 0x2B);

        manager.SetForeground(ProcessB, secureInput: false);
        Assert.Equal(MaintainableLayerMode.Inactive, manager.CurrentState.Mode);

        manager.SetForeground(ProcessA, secureInput: false);
        Assert.Equal(MaintainableLayerMode.Locked, manager.CurrentState.Mode);
    }

    [Fact]
    public void SamePidWithDifferentStartTime_DoesNotReuseLock()
    {
        var manager = CreateEnabledManager();
        Lock(manager, "dk_greek", 0x2B);

        manager.SetForeground(new ForegroundProcessIdentity(ProcessA.ProcessId, 9999), secureInput: false);

        Assert.Equal(MaintainableLayerMode.Inactive, manager.CurrentState.Mode);
    }

    [Fact]
    public void SecureInput_SuspendsAndThenRestoresLock()
    {
        var manager = CreateEnabledManager();
        Lock(manager, "dk_cyrillic", 0x2B);

        manager.SetForeground(ProcessA, secureInput: true);
        Assert.Equal(MaintainableLayerMode.Inactive, manager.CurrentState.Mode);
        Assert.False(manager.BeginTrigger("dk_greek", 0x2B));

        manager.SetForeground(ProcessA, secureInput: false);
        Assert.Equal("dk_cyrillic", manager.CurrentState.LayerId);
        Assert.Equal(MaintainableLayerMode.Locked, manager.CurrentState.Mode);
    }

    [Fact]
    public void Escape_UnlocksAndConsumesDownAndUp()
    {
        var manager = CreateEnabledManager();
        Lock(manager, "dk_greek", 0x2B);

        Assert.True(manager.HandleEscape(isKeyDown: true));
        Assert.True(manager.HandleEscape(isKeyDown: false));
        Assert.False(manager.HandleEscape(isKeyDown: true));
    }

    [Fact]
    public void FeatureDisabled_DoesNotConsumeTrigger()
    {
        var manager = new MaintainableLayerManager(() => _now);
        manager.ApplySettings(false, MaintainableLayerManager.SupportedLayers, 400);
        manager.SetForeground(ProcessA, secureInput: false);

        Assert.False(manager.BeginTrigger("dk_greek", 0x2B));
    }

    private void Lock(MaintainableLayerManager manager, string layer, uint scanCode)
    {
        Tap(manager, layer, scanCode);
        _now += 100;
        Tap(manager, layer, scanCode);
    }

    private static void Tap(MaintainableLayerManager manager, string layer, uint scanCode)
    {
        Assert.True(manager.BeginTrigger(layer, scanCode));
        Assert.True(manager.EndTrigger(scanCode));
    }
}
