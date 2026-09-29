using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace TypingEngine.Windows.Tests;

/// <summary>
/// Audit 25/09 (hors lot 8) — <c>ToUnicode</c> et <c>ToUnicodeEx</c> étaient déclarés sans
/// <c>CharSet.Unicode</c> : le tampon UTF-16 rempli par Windows était relu en ANSI. « É »
/// (0xC9) ne revenait juste qu'en page de code 1252 ; en 1251, 1253 ou UTF-8 bêta,
/// la détection du double remappage (Verr. Maj. + é → É) échouait en silence.
/// </summary>
public class Win32TextMarshallingTests
{
    private static IEnumerable<MethodInfo> ExternAvecTexte() =>
        typeof(Win32).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            .Where(m => m.GetCustomAttribute<DllImportAttribute>() is not null)
            .Where(m => m.GetParameters().Any(p =>
            {
                var t = p.ParameterType.IsByRef ? p.ParameterType.GetElementType()! : p.ParameterType;
                return t == typeof(string) || t == typeof(StringBuilder) || t == typeof(char);
            }));

    [Fact]
    public void ToutPInvokeQuiPasseDuTexte_EstEnUnicode()
    {
        var fautifs = ExternAvecTexte()
            .Where(m => m.GetCustomAttribute<DllImportAttribute>()!.CharSet != CharSet.Unicode)
            .Select(m => m.Name)
            .ToList();

        Assert.Contains(ExternAvecTexte(), m => m.Name == nameof(Win32.ToUnicodeEx));
        Assert.Empty(fautifs);
    }

    [Fact]
    public void ToUnicodeEx_RendUnSeulCaractereUtf16()
    {
        // Appel réel sur la disposition du fil : Maj + A donne « A » sur toute disposition latine.
        IntPtr hkl = Win32.GetKeyboardLayout(0);
        var state = new byte[256];
        state[0x10] = 0x80;
        var buf = new StringBuilder(8);

        int n = Win32.ToUnicodeEx(0x41, 0x1E, state, buf, buf.Capacity, 0x04, hkl);

        Assert.Equal(1, n);
        Assert.Equal("A", buf.ToString());
    }
}
