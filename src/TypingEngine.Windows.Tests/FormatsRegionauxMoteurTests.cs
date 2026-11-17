using System.Globalization;

namespace TypingEngine.Windows.Tests;

/// <summary>
/// Audit des formats régionaux (docs/audit-formats-regionaux.md, constat FR-01) : le chemin
/// Ctrl+lettre de <see cref="KeyMapper"/> met la lettre en capitale avec la culture du fil
/// (<c>char.ToUpper(baseChar[0])</c>). Sous un format régional turc ou azéri, « i » devient
/// « İ » (U+0130), sort de la branche A-Z et part par <c>VkKeyScanExW('i')</c>.
/// La culture n'est fixée que sur le fil du test (<see cref="CultureInfo.CurrentCulture"/>),
/// sans toucher aux réglages de la machine, et restaurée à la fin.
/// </summary>
public class FormatsRegionauxMoteurTests
{
    private const uint SC_D08 = 0x17;        // touche I en AZERTY Global (src/AZERTY Global 2026.json)
    private const uint SC_LCONTROL = 0x1D;
    private const ushort VK_I = 0x49;
    private const ushort VK_OEM_7 = 0xDE;
    private const ushort VK_LCONTROL = 0xA2;
    private static readonly IntPtr HklFrAzerty = (IntPtr)0x040C040C;
    private static readonly IntPtr HklTurcQ = (IntPtr)0x041F041F;
    private static readonly short KeyDown = unchecked((short)0x8000);

    /// <summary>Fixe la culture du fil pour la durée d'un test.</summary>
    private sealed class CultureDuFil : IDisposable
    {
        private readonly CultureInfo _avant = CultureInfo.CurrentCulture;
        public CultureDuFil(string nom) => CultureInfo.CurrentCulture = new CultureInfo(nom, useUserOverride: false);
        public void Dispose() => CultureInfo.CurrentCulture = _avant;
    }

    private static Layout DispositionAvecI() => new()
    {
        Keys =
        {
            [SC_D08] = new KeyDefinition { Position = "D08", Scancode = SC_D08, Base = "i", Shift = "I", Caps = "I", CapsShift = "i" }
        }
    };

    private static (KeyMapper, MockWin32Api, ForegroundMonitor) CtrlEnfonceAvecAuPremierPlan(IntPtr hkl, Action<MockWin32Api> scripter)
    {
        var mock = new MockWin32Api();
        scripter(mock);
        mock.AsyncKeyStateScript[VK_LCONTROL] = KeyDown;
        mock.CurrentHkl = hkl;
        mock.ScriptedProcessName = "notepad.exe";
        mock.ScriptedFullPath = @"C:\Windows\notepad.exe";
        mock.ScriptedModules = new[] { "kernel32.dll", "user32.dll" };
        mock.ForegroundWindow = (IntPtr)0x1234;
        var premierPlan = new ForegroundMonitor(mock, IntPtr.Zero);
        var mapper = new KeyMapper(DispositionAvecI(), mock);
        mapper.SetForegroundMonitor(premierPlan);
        mapper.TrackModifiers(VK_LCONTROL, SC_LCONTROL, 0, true);
        mock.SendInputCalls.Clear();
        return (mapper, mock, premierPlan);
    }

    /// <summary>Disposition Windows AZERTY français au premier plan : la touche I y donne déjà
    /// VK_I, donc Ctrl+I passe la frappe native (scancode préservé pour les jeux, GLFW, SDL).
    /// Sous tr-TR et az-Latn-AZ, la branche A-Z est manquée et le moteur injecte un VK_I
    /// synthétique à la place.</summary>
    [Theory]
    [InlineData("fr-FR")]
    [InlineData("en-US")]
    [InlineData("tr-TR")]
    [InlineData("az-Latn-AZ")]
    public void CtrlI_AzertyFrancaisAuPremierPlan_PasseLaFrappeNative_SousToutFormatRegional(string culture)
    {
        using var fil = new CultureDuFil(culture);
        var (mapper, mock, premierPlan) = CtrlEnfonceAvecAuPremierPlan(HklFrAzerty, m =>
        {
            m.MapVirtualKeyScript[(SC_D08, 1, HklFrAzerty)] = VK_I;
            m.VkKeyScanScript[('i', HklFrAzerty)] = (short)VK_I;
        });
        using (premierPlan)
        {
            bool traite = mapper.ProcessKey(VK_I, SC_D08, 0, true);

            Assert.False(traite, $"Ctrl+I injecté au lieu de passer la frappe native sous {culture}");
            Assert.Empty(mock.SendInputCalls);
        }
    }

    /// <summary>Disposition Windows turque Q au premier plan (prémisse kbdtuq, non mesurée sur
    /// la machine : la touche I donne VK_I et « ı », « i » est sur VK_OEM_7). Sous tr-TR, le
    /// moteur cherche « i » et envoie Ctrl+VK_OEM_7 : le raccourci Ctrl+I (italique) est perdu.</summary>
    [Theory]
    [InlineData("fr-FR")]
    [InlineData("tr-TR")]
    public void CtrlI_TurcQAuPremierPlan_ResteCtrlI_SousToutFormatRegional(string culture)
    {
        using var fil = new CultureDuFil(culture);
        var (mapper, mock, premierPlan) = CtrlEnfonceAvecAuPremierPlan(HklTurcQ, m =>
        {
            m.MapVirtualKeyScript[(SC_D08, 1, HklTurcQ)] = VK_I;
            m.VkKeyScanScript[('i', HklTurcQ)] = (short)VK_OEM_7;
            m.VkKeyScanScript[('I', HklTurcQ)] = (short)((1 << 8) | VK_I);
        });
        using (premierPlan)
        {
            mapper.ProcessKey(VK_I, SC_D08, 0, true);

            var vkEnvoyes = mock.SendInputCalls.SelectMany(lot => lot).Select(i => i.u.ki.wVk).ToArray();
            Assert.DoesNotContain(VK_OEM_7, vkEnvoyes);
        }
    }
}
