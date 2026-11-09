using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Invariants d'expérience, vérifiés sur toute la surface plutôt qu'écran par écran (audit UX
/// du 09/11 : Ctrl+A tapait « a » en mode Libre, « Suivant » restait mort à la fin du Défi).
/// </summary>
public class UxInvariantsTests : IDisposable
{
    private const uint VK_LSHIFT = 0xA0;
    private const uint VK_LCONTROL = 0xA2;
    private const uint VK_RCONTROL = 0xA3;
    private const uint VK_LMENU = 0xA4;
    private const uint VK_RMENU = 0xA5;
    private const uint VK_LWIN = 0x5B;
    private const uint VK_RWIN = 0x5C;
    private const uint LLKHF_EXTENDED = 0x01;

    private readonly string _tempDir;
    private readonly Layout _layout = LayoutLoader.LoadFromResource();

    public UxInvariantsTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "AZGUX_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        ConfigManager.OverrideConfigPathForTests(Path.Combine(_tempDir, "config.json"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    private static readonly (string Name, (uint Vk, uint Sc, uint Flags)[] Keys)[] ShortcutChords =
    {
        ("Ctrl gauche", new[] { (VK_LCONTROL, 0x1Du, 0u) }),
        ("Ctrl droit", new[] { (VK_RCONTROL, 0x1Du, LLKHF_EXTENDED) }),
        ("Ctrl+Maj", new[] { (VK_LCONTROL, 0x1Du, 0u), (VK_LSHIFT, 0x2Au, 0u) }),
        ("Alt gauche", new[] { (VK_LMENU, 0x38u, 0u) }),
        ("Alt+Maj", new[] { (VK_LMENU, 0x38u, 0u), (VK_LSHIFT, 0x2Au, 0u) }),
        ("Win gauche", new[] { (VK_LWIN, 0x5Bu, LLKHF_EXTENDED) }),
        ("Win droit", new[] { (VK_RWIN, 0x5Cu, LLKHF_EXTENDED) }),
        ("Ctrl gauche+Alt gauche", new[] { (VK_LCONTROL, 0x1Du, 0u), (VK_LMENU, 0x38u, 0u) }),
    };

    [Fact]
    public void NoShortcutChord_ExpectsTextFromAnyLayoutKey()
    {
        var failures = new List<string>();
        foreach (var (name, keys) in ShortcutChords)
        {
            var mapper = new KeyMapper(_layout, new MockWin32Api());
            foreach (var (vk, sc, flags) in keys)
                mapper.TrackModifiers(vk, sc, flags, true);

            foreach (uint scancode in _layout.Keys.Keys)
            {
                var buffer = new PhysicalTextInputBuffer(1000, () => 0);
                buffer.CaptureKey(_layout, scancode, mapper);
                if (buffer.HasPending)
                    failures.Add($"{name} + 0x{scancode:X2}");
            }
        }

        Assert.Empty(failures);
    }

    [Fact]
    public void AltGr_StillExpectsLayoutText()
    {
        // Alt Gr physique : Alt droit étendu, précédé du Ctrl gauche fantôme du pilote.
        var mapper = new KeyMapper(_layout, new MockWin32Api());
        mapper.TrackModifiers(VK_LCONTROL, 0x1D, 0, true);
        mapper.TrackModifiers(VK_RMENU, 0x38, LLKHF_EXTENDED, true);
        Assert.True(mapper.AltGrDown);

        int captured = 0;
        foreach (uint scancode in _layout.Keys.Keys)
        {
            string? expected = PhysicalTextInputBuffer.ExpectedText(_layout, scancode, false, true, false, null);
            if (string.IsNullOrEmpty(expected)) continue;
            var buffer = new PhysicalTextInputBuffer(1000, () => 0);
            buffer.CaptureKey(_layout, scancode, mapper);
            Assert.True(buffer.HasPending, $"Alt Gr + 0x{scancode:X2} n'attend plus « {expected} »");
            captured++;
        }

        Assert.True(captured > 20, $"seulement {captured} touches Alt Gr vérifiées");
    }

    [Theory]
    [InlineData('\u0001')] // Ctrl+A
    [InlineData('\u0003')] // Ctrl+C
    [InlineData('\u0016')] // Ctrl+V
    [InlineData('\u0018')] // Ctrl+X
    [InlineData('\u001A')] // Ctrl+Z
    [InlineData('\b')]     // Retour arrière
    [InlineData('\r')]     // Entrée
    [InlineData('\t')]     // Tab
    [InlineData('\u007F')] // Ctrl+Retour arrière
    public void ReceivedControlCharacter_IsNeverReplacedByPendingText(char control)
    {
        var buffer = new PhysicalTextInputBuffer(1000, () => 0);
        buffer.Enqueue("a");

        Assert.Equal(control, buffer.Resolve(control));
        // L'entrée attendue reste pour le WM_CHAR de sa propre frappe.
        Assert.Equal('a', buffer.Resolve('x'));
    }

    [Fact]
    public void ExpectedControlCharacter_IsConsumedByReceivedControl()
    {
        var buffer = new PhysicalTextInputBuffer(1000, () => 0);
        buffer.Enqueue("\r");
        buffer.Enqueue("b");

        Assert.Equal('\r', buffer.Resolve('\r'));
        Assert.Equal('b', buffer.Resolve('x'));
    }

    [Theory]
    [InlineData(true, false, false, false, true)]
    [InlineData(false, true, false, false, true)]
    [InlineData(false, false, true, false, true)]
    [InlineData(true, true, false, true, false)] // Alt Gr = Ctrl+Alt sous Windows
    [InlineData(true, false, false, true, false)]
    [InlineData(false, false, false, false, false)]
    public void ShortcutChord_ExcludesAltGr(bool ctrl, bool alt, bool win, bool altGr, bool expected)
    {
        Assert.Equal(expected, PhysicalTextInputBuffer.IsShortcutChord(ctrl, alt, win, altGr));
    }
}
