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

    // S3 (candidat 1.3.0) : Entrée et Tab annulent la touche morte armée, la touche passe.
    // Entrée principale (0x1C), Entrée du pavé numérique (0xE0 0x1C, étendue) et Tab (0x0F).
    [Theory]
    [InlineData(0x0Du, 0x1Cu, 0u)]
    [InlineData(0x0Du, 0x1Cu, 1u)]
    [InlineData(0x09u, 0x0Fu, 0u)]
    public void ProcessKey_EnterOrTab_CancelsActiveDeadKeyAndPassesThrough(uint vk, uint scan, uint flags)
    {
        var mock = new MockWin32Api();
        var mapper = new KeyMapper(_layout, mock);

        mapper.ProcessKey(0, SC_C11, 0, true); // dk_acute actif
        mock.SendInputCalls.Clear();

        bool handled = mapper.ProcessKey(vk, scan, flags, true);

        Assert.False(handled); // Windows traite Entrée / Tab normalement
        Assert.Null(mapper.ActiveDeadKey);
        Assert.Empty(mock.SendInputCalls);

        // La frappe suivante ne doit plus être transformée par la DK annulée
        mapper.ProcessKey(0, SC_D03, 0, true);
        Assert.Equal("e", EmittedText(mock));
    }

    // E15 (audit 09/11) : Échap ou un raccourci Ctrl abandonne la touche morte armée, sans rien
    // écrire ; la lettre suivante sort nue.
    // RA3 (audit de régression 10/11) : mêmes attentes avec Alt gauche et Win tenus
    // (0 = Échap seul ; sinon le VK du modificateur tenu pendant la touche X).
    [Theory]
    [InlineData(0u, 0u, 0u)]        // Échap
    [InlineData(0xA2u, 0x1Du, 0u)]  // Ctrl gauche + X
    [InlineData(0xA4u, 0x38u, 0u)]  // Alt gauche + X
    [InlineData(0x5Bu, 0x5Bu, 1u)]  // Win gauche + X
    [InlineData(0x5Cu, 0x5Cu, 1u)]  // Win droite + X
    public void ProcessKey_EscapeOrCtrlShortcut_CancelsActiveDeadKey(uint modifierVk, uint modifierScan, uint modifierFlags)
    {
        var mock = new MockWin32Api();
        var mapper = new KeyMapper(_layout, mock);

        mapper.ProcessKey(0, SC_C11, 0, true); // dk_acute actif
        Assert.NotNull(mapper.ActiveDeadKey);
        mock.SendInputCalls.Clear();

        if (modifierVk != 0)
        {
            mock.AsyncKeyStateScript[(int)modifierVk] = KeyDown;
            mapper.TrackModifiers(modifierVk, modifierScan, modifierFlags, true);
            mapper.ProcessKey(0x58, 0x2D, 0, true); // modificateur + X
            Assert.Null(mapper.ActiveDeadKey);
            mapper.ProcessKey(0x58, 0x2D, 0, false);
            mock.AsyncKeyStateScript[(int)modifierVk] = 0;
            mapper.TrackModifiers(modifierVk, modifierScan, modifierFlags, false);
        }
        else
            mapper.ProcessKey(0x1B, 0x01, 0, true); // Échap

        Assert.Null(mapper.ActiveDeadKey);
        Assert.DoesNotContain("´", EmittedText(mock));
        mock.SendInputCalls.Clear();
        mapper.ProcessKey(0, SC_D03, 0, true);
        Assert.Equal("e", EmittedText(mock));
    }

    // RA3 : Alt Gr n'est pas un raccourci. Tenu avec une touche (Alt Gr réel : Alt droite
    // et son Ctrl gauche fantôme ; Alt Gr simulé : Ctrl droite + Alt gauche), il ne doit
    // pas abandonner la touche morte armée : l'accent est rendu avec le caractère.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ProcessKey_AltGrKey_KeepsActiveDeadKey(bool simulated)
    {
        var mock = new MockWin32Api();
        var mapper = new KeyMapper(_layout, mock);
        (uint Vk, uint Scan, uint Flags)[] modifiers = simulated
            ? new[] { (0xA3u, 0x1Du, 1u), (0xA4u, 0x38u, 0u) }   // Ctrl droite + Alt gauche
            : new[] { (0xA2u, 0x1Du, 0u), (0xA5u, 0x38u, 1u) };  // Ctrl fantôme + Alt droite

        mapper.ProcessKey(0, SC_C11, 0, true); // dk_acute actif
        Assert.Equal("dk_acute", mapper.ActiveDeadKey);
        mock.SendInputCalls.Clear();

        foreach (var (vk, scan, flags) in modifiers)
        {
            mock.AsyncKeyStateScript[(int)vk] = KeyDown;
            mapper.TrackModifiers(vk, scan, flags, true);
        }
        Assert.Equal("dk_acute", mapper.ActiveDeadKey);
        mapper.ProcessKey(0, SC_D03, 0, true); // Alt Gr + E = €
        mapper.ProcessKey(0, SC_D03, 0, false);
        foreach (var (vk, scan, flags) in modifiers)
        {
            mock.AsyncKeyStateScript[(int)vk] = 0;
            mapper.TrackModifiers(vk, scan, flags, false);
        }

        // L'accent n'a pas été jeté : il sort avec le caractère de la couche Alt Gr.
        Assert.Equal("´€", EmittedText(mock));
        Assert.Null(mapper.ActiveDeadKey);
    }

    [Fact]
    public void ProcessKey_Enter_WithoutDeadKey_StillPassesThroughUntouched()
    {
        var mock = new MockWin32Api();
        var mapper = new KeyMapper(_layout, mock);

        bool handled = mapper.ProcessKey(0x0D, 0x1C, 0, true);

        Assert.False(handled);
        Assert.Null(mapper.ActiveDeadKey);
        Assert.Empty(mock.SendInputCalls);
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
