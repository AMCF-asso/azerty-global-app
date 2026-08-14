using System.Reflection;
using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

public class QuickWinsTests
{
    [Fact]
    public void PrepareTrayIconForAdd_RestoresAllRegistrationFlags()
    {
        var method = typeof(TrayApplication).GetMethod(
            "PrepareTrayIconForAdd",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);

        object?[] args =
        {
            new Win32.NOTIFYICONDATAW { uFlags = 0x10 }
        };

        method.Invoke(null, args);

        var updated = Assert.IsType<Win32.NOTIFYICONDATAW>(args[0]);
        Assert.Equal(0x01u | 0x02u | 0x04u, updated.uFlags);
    }

    [Fact]
    public void UserOverride_IsNotClassifiedAsSecuritySuspension()
    {
        var method = typeof(TrayApplication).GetMethod(
            "IsSecuritySuspension",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var result = method.Invoke(null, new object[] { CompatibilitySuspendReason.UserOverride });

        Assert.Equal(false, result);
    }

    [Fact]
    public void StartupTaskEnabledByPolicy_IsReportedAsActive()
    {
        var method = typeof(AutoStart).GetMethod(
            "IsStartupTaskActive",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var result = method.Invoke(
            null,
            new object[] { Windows.ApplicationModel.StartupTaskState.EnabledByPolicy });

        Assert.Equal(true, result);
    }

    [Fact]
    public void SecondInstanceLog_DoesNotContainRawArguments()
    {
        var method = typeof(Program).GetMethod(
            "BuildSecondInstanceLogDetails",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var details = Assert.IsType<string>(method.Invoke(
            null,
            new object[]
            {
                true,
                new[] { @"C:\Users\Alice\AZERTY Global.exe", "secret-activation-token" }
            }));

        Assert.Contains("packaged=True", details);
        Assert.Contains("argCount=2", details);
        Assert.DoesNotContain("Alice", details);
        Assert.DoesNotContain("secret-activation-token", details);
    }
}
