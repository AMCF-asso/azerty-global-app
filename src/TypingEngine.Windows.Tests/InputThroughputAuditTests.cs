using System.Diagnostics;
using Xunit.Abstractions;

namespace TypingEngine.Windows.Tests;

/// <summary>
/// Sonde reproductible du chemin critique de saisie. Les durées sont informatives :
/// les assertions portent sur l'intégrité des sorties, pas sur la vitesse de la machine.
/// </summary>
public sealed class InputThroughputAuditTests
{
    private const int Samples = 20_000;
    private const uint ScanCode = 0x10;
    private const uint VirtualKey = 0x51;
    private readonly ITestOutputHelper _output;

    public InputThroughputAuditTests(ITestOutputHelper output) => _output = output;

    [Fact]
    [Trait("Category", "Audit")]
    public void BurstPaths_PreserveEveryOutput_AndReportLatencyPercentiles()
    {
        MeasureSimpleUnicodePath();
        MeasureDeadKeyPath();
        MeasureNativeComboFallbackPath();
    }

    private void MeasureSimpleUnicodePath()
    {
        var layout = LayoutWithKey("a");
        var api = new MockWin32Api();
        var mapper = new KeyMapper(layout, api);

        WarmUp(mapper, api, ScanCode);
        var samples = Measure(Samples, () => TypeKey(mapper, ScanCode));

        Assert.Equal(Samples, api.SendInputCalls.Count);
        Assert.All(api.SendInputCalls, batch => Assert.Equal(2, batch.Length));
        WriteResult("unicode-simple", samples, physicalKeysPerSample: 1);
    }

    private void MeasureDeadKeyPath()
    {
        const uint deadKeyScanCode = 0x1A;
        var layout = LayoutWithKey("e");
        layout.Keys[deadKeyScanCode] = new KeyDefinition
        {
            Position = "C11",
            Scancode = deadKeyScanCode,
            Base = "dk_acute"
        };
        var deadKey = new DeadKeyDefinition { Name = "dk_acute" };
        deadKey.Table["e"] = "é";
        deadKey.Table[" "] = "´";
        layout.DeadKeys["dk_acute"] = deadKey;

        var api = new MockWin32Api();
        var mapper = new KeyMapper(layout, api);

        for (int i = 0; i < 1_000; i++) TypeDeadKeySequence(mapper, deadKeyScanCode);
        api.SendInputCalls.Clear();
        var samples = Measure(Samples, () => TypeDeadKeySequence(mapper, deadKeyScanCode));

        Assert.Equal(Samples, api.SendInputCalls.Count);
        Assert.All(api.SendInputCalls, batch => Assert.Equal((ushort)'é', batch[0].u.ki.wScan));
        WriteResult("dead-key-composition", samples, physicalKeysPerSample: 2);
    }

    private void MeasureNativeComboFallbackPath()
    {
        var layout = LayoutWithKey("Œ");
        var api = new MockWin32Api
        {
            ScriptedProcessName = "javaw.exe",
            ScriptedFullPath = @"C:\Java\javaw.exe",
            ScriptedModules = new[] { "SDL2.dll" },
            CurrentHkl = (IntPtr)0x040C040C
        };
        using var foreground = new ForegroundMonitor(api, IntPtr.Zero);
        var mapper = new KeyMapper(layout, api);
        mapper.SetForegroundMonitor(foreground);

        WarmUp(mapper, api, ScanCode);
        var samples = Measure(Samples, () => TypeKey(mapper, ScanCode));

        Assert.Equal(Samples, api.SendInputCalls.Count);
        Assert.All(api.SendInputCalls, batch => Assert.True(batch.Length >= 10));
        WriteResult("native-combo-altcode", samples, physicalKeysPerSample: 1);
    }

    private static Layout LayoutWithKey(string output)
    {
        var layout = new Layout();
        layout.Keys[ScanCode] = new KeyDefinition
        {
            Position = "D01",
            Scancode = ScanCode,
            Base = output
        };
        return layout;
    }

    private static void WarmUp(KeyMapper mapper, MockWin32Api api, uint scanCode)
    {
        for (int i = 0; i < 1_000; i++) TypeKey(mapper, scanCode);
        api.SendInputCalls.Clear();
    }

    private static void TypeKey(KeyMapper mapper, uint scanCode)
    {
        Assert.True(mapper.ProcessKey(VirtualKey, scanCode, 0, true));
        Assert.True(mapper.ProcessKey(VirtualKey, scanCode, 0, false));
    }

    private static void TypeDeadKeySequence(KeyMapper mapper, uint deadKeyScanCode)
    {
        Assert.True(mapper.ProcessKey(0xDD, deadKeyScanCode, 0, true));
        Assert.True(mapper.ProcessKey(0xDD, deadKeyScanCode, 0, false));
        TypeKey(mapper, ScanCode);
    }

    private static double[] Measure(int count, Action action)
    {
        var samples = new double[count];
        for (int i = 0; i < count; i++)
        {
            long start = Stopwatch.GetTimestamp();
            action();
            samples[i] = Stopwatch.GetElapsedTime(start).TotalMicroseconds;
        }
        Array.Sort(samples);
        return samples;
    }

    private void WriteResult(string path, double[] samples, int physicalKeysPerSample)
    {
        double totalMicroseconds = samples.Sum();
        double keysPerSecond =
            (double)physicalKeysPerSample * samples.Length * 1_000_000 / totalMicroseconds;
        _output.WriteLine(
            $"{path}: p50={Percentile(samples, 0.50):F3} µs; " +
            $"p95={Percentile(samples, 0.95):F3} µs; " +
            $"p99={Percentile(samples, 0.99):F3} µs; " +
            $"max={samples[^1]:F3} µs; throughput={keysPerSecond:F0} touches/s");
    }

    private static double Percentile(double[] sorted, double percentile)
    {
        int index = (int)Math.Ceiling(sorted.Length * percentile) - 1;
        return sorted[Math.Clamp(index, 0, sorted.Length - 1)];
    }
}
