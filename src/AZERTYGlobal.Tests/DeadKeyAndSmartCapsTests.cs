using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Couverture des deux mécanismes les plus différenciants de la disposition, jusqu'ici
/// non testés (item « Tests unitaires » du TO-DO v1.0) :
/// - les touches mortes de ProcessKey (résolution, isolé + caractère, chaînage, Backspace) ;
/// - le Smart Caps Lock (GetOutput par couche caps et émission réelle).
/// Utilise le layout embarqué réel (AZERTY Global 2026.json) plutôt qu'un layout
/// synthétique : ces tests protègent aussi la cohérence des données de la disposition.
/// </summary>
public class DeadKeyAndSmartCapsTests : IDisposable
{
    // Scancodes AZERTY (set 1)
    private const uint SC_E02 = 0x03; // é / 2 / É en caps
    private const uint SC_D03 = 0x12; // e / E / €
    private const uint SC_C11 = 0x28; // dk_acute / dk_grave (touche ù de l'AZERTY trad)
    private const uint SC_B08 = 0x33; // . / ; / >
    private const uint SC_LSHIFT = 0x2A;

    private const uint VK_BACK = 0x08;
    private const ushort VK_LSHIFT = 0xA0;
    private static readonly short KeyDown = unchecked((short)0x8000);

    private readonly string _tempDir;
    private readonly Layout _layout = LayoutLoader.LoadFromResource();

    public DeadKeyAndSmartCapsTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "AZGDK_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        ConfigManager.OverrideConfigPathForTests(Path.Combine(_tempDir, "config.json"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    private static string EmittedText(MockWin32Api mock)
    {
        // Reconstruit le texte émis via KEYEVENTF_UNICODE (wVk=0, keydown uniquement)
        var chars = new List<char>();
        foreach (var input in mock.AllInputs)
        {
            var ki = input.u.ki;
            if (ki.wVk == 0 && (ki.dwFlags & 0x0004) != 0 && (ki.dwFlags & 0x0002) == 0)
                chars.Add((char)ki.wScan);
        }
        return new string(chars.ToArray());
    }

    // ═══════════════════════════════════════════════════════════════
    // GetOutput — sémantique Smart Caps Lock (données réelles du layout)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void GetOutput_CapsLock_UppercasesLettersAndAccents()
    {
        Assert.Equal("E", _layout.Keys[SC_D03].GetOutput(shift: false, altGr: false, capsLock: true));
        Assert.Equal("É", _layout.Keys[SC_E02].GetOutput(shift: false, altGr: false, capsLock: true));
    }

    [Fact]
    public void GetOutput_CapsLockPlusShift_InvertsToLowercase()
    {
        Assert.Equal("e", _layout.Keys[SC_D03].GetOutput(shift: true, altGr: false, capsLock: true));
    }

    [Fact]
    public void GetOutput_CapsLock_DoesNotAffectNonLetterKeys()
    {
        // Point en accès direct : Verr. Maj. n'affecte que les lettres
        Assert.Equal(".", _layout.Keys[SC_B08].GetOutput(shift: false, altGr: false, capsLock: true));
        // Couche caps_shift absente sur é → repli sur la couche Shift (le chiffre 2)
        Assert.Equal("2", _layout.Keys[SC_E02].GetOutput(shift: true, altGr: false, capsLock: true));
    }

    // ═══════════════════════════════════════════════════════════════
    // ProcessKey — touches mortes
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void ProcessKey_DeadKeyThenLetter_EmitsAccentedCharacter()
    {
        var mock = new MockWin32Api();
        var mapper = new KeyMapper(_layout, mock);
        mock.SendInputCalls.Clear();

        bool dkHandled = mapper.ProcessKey(0, SC_C11, 0, true); // dk_acute
        Assert.True(dkHandled);
        Assert.Equal("dk_acute", mapper.ActiveDeadKey);
        Assert.Empty(mock.SendInputCalls); // rien n'est émis tant que la DK attend

        bool eHandled = mapper.ProcessKey(0, SC_D03, 0, true); // e
        Assert.True(eHandled);
        Assert.Null(mapper.ActiveDeadKey);
        Assert.Equal("é", EmittedText(mock));
    }

    [Fact]
    public void ProcessKey_DeadKeyThenUnmappedChar_EmitsIsolatedThenCharacter()
    {
        var mock = new MockWin32Api();
        var mapper = new KeyMapper(_layout, mock);
        mock.SendInputCalls.Clear();

        mapper.ProcessKey(0, SC_C11, 0, true); // dk_acute
        bool handled = mapper.ProcessKey(0, SC_B08, 0, true); // '.' — pas dans la table

        Assert.True(handled);
        Assert.Null(mapper.ActiveDeadKey);
        Assert.Equal("´.", EmittedText(mock)); // diacritique isolé puis caractère
    }

    [Fact]
    public void ProcessKey_DeadKeyChaining_EmitsFirstIsolatedAndActivatesSecond()
    {
        var mock = new MockWin32Api();
        var mapper = new KeyMapper(_layout, mock);

        mapper.ProcessKey(0, SC_C11, 0, true); // dk_acute actif
        Assert.Equal("dk_acute", mapper.ActiveDeadKey);
        mock.SendInputCalls.Clear();

        // Maj + C11 = dk_grave, pressée pendant que dk_acute attend.
        // '´' n'est pas dans la table de dk_grave → émettre l'isolé, activer dk_grave.
        mock.AsyncKeyStateScript[VK_LSHIFT] = KeyDown;
        mapper.TrackModifiers(VK_LSHIFT, SC_LSHIFT, 0, true);
        bool handled = mapper.ProcessKey(0, SC_C11, 0, true);

        Assert.True(handled);
        Assert.Equal("dk_grave", mapper.ActiveDeadKey);
        Assert.Equal("´", EmittedText(mock));
    }

    [Fact]
    public void ProcessKey_Backspace_CancelsActiveDeadKeyAndPassesThrough()
    {
        var mock = new MockWin32Api();
        var mapper = new KeyMapper(_layout, mock);

        mapper.ProcessKey(0, SC_C11, 0, true); // dk_acute actif
        mock.SendInputCalls.Clear();

        bool handled = mapper.ProcessKey(VK_BACK, 0x0E, 0, true);

        Assert.False(handled); // Windows traite le Backspace normalement
        Assert.Null(mapper.ActiveDeadKey);
        Assert.Empty(mock.SendInputCalls);

        // La frappe suivante ne doit plus être transformée par la DK annulée
        mapper.ProcessKey(0, SC_D03, 0, true);
        Assert.Equal("e", EmittedText(mock));
    }

    // ═══════════════════════════════════════════════════════════════
    // ProcessKey — Smart Caps Lock (émission de bout en bout)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void ProcessKey_CapsLockActive_EmitsAccentedUppercaseOnEAcuteKey()
    {
        var mock = new MockWin32Api();
        mock.KeyStateScript[0x14] = 0x0001; // Verr. Maj. système actif
        var mapper = new KeyMapper(_layout, mock);
        mock.SendInputCalls.Clear();

        bool handled = mapper.ProcessKey(0, SC_E02, 0, true);

        Assert.True(handled);
        Assert.Equal("É", EmittedText(mock)); // É en un appui : promesse n°1 du produit
    }

    [Fact]
    public void ProcessKey_CapsLockPlusShift_FallsBackToShiftLayer()
    {
        var mock = new MockWin32Api();
        mock.KeyStateScript[0x14] = 0x0001;
        var mapper = new KeyMapper(_layout, mock);

        mock.AsyncKeyStateScript[VK_LSHIFT] = KeyDown;
        mapper.TrackModifiers(VK_LSHIFT, SC_LSHIFT, 0, true);
        mock.SendInputCalls.Clear();

        bool handled = mapper.ProcessKey(0, SC_E02, 0, true);

        Assert.True(handled);
        Assert.Equal("2", EmittedText(mock)); // caps_shift absent sur é → couche Shift
    }

    [Fact]
    public void ProcessKey_CapsLockActive_LeavesDirectPunctuationUnchanged()
    {
        var mock = new MockWin32Api();
        mock.KeyStateScript[0x14] = 0x0001;
        var mapper = new KeyMapper(_layout, mock);
        mock.SendInputCalls.Clear();

        bool handled = mapper.ProcessKey(0, SC_B08, 0, true);

        Assert.True(handled);
        Assert.Equal(".", EmittedText(mock)); // le point reste un point sous Verr. Maj.
    }

    [Fact]
    public void ProcessKey_DeadKeyWithCapsLock_EmitsAccentedUppercase()
    {
        var mock = new MockWin32Api();
        mock.KeyStateScript[0x14] = 0x0001;
        var mapper = new KeyMapper(_layout, mock);
        mock.SendInputCalls.Clear();

        mapper.ProcessKey(0, SC_C11, 0, true); // dk_acute
        bool handled = mapper.ProcessKey(0, SC_D03, 0, true); // E (caps)

        Assert.True(handled);
        Assert.Null(mapper.ActiveDeadKey);
        Assert.Equal("É", EmittedText(mock)); // ´ + E = É
    }
}
