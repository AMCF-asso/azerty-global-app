using System.Globalization;
using System.Runtime.InteropServices;

namespace TypingEngine.Windows;

/// <summary>
/// D12 (tests autonomes Windows-MCP, 2026-11-04) : interrupteur de banc.
///
/// Dans la Windows Sandbox du banc, les frappes arrivent par SendInput, donc marquées
/// LLKHF_INJECTED : AG130-07 les écarte et rien n'est remappé (mesure S1 : 2 caractères
/// justes sur 25). L'interrupteur fait traiter ces injections étrangères comme des frappes
/// physiques, comme pendant une session distante (C5).
///
/// Deux verrous, tous deux requis :
/// <list type="bullet">
/// <item>la constante <c>AG_BANC_TEST</c>, définie par la seule configuration MSBuild
/// <c>Banc</c> (<c>dotnet publish -c Banc</c>, sortie <c>bin/Banc</c>, jamais empaquetée) ;</item>
/// <item>une valeur DWORD à 1 sous HKCU, posée par le kit du banc et lue une fois au démarrage.</item>
/// </list>
/// Hors build Banc, la valeur n'est même pas lue. VK_PACKET et nos propres injections restent
/// exclus dans tous les cas (KeyboardHook.ShouldProcessKeystroke).
/// </summary>
public static class BancSwitch
{
#if AG_BANC_TEST
    public static bool CompiledIn => true;
#else
    public static bool CompiledIn => false;
#endif

    /// <summary>
    /// Seule la valeur 1 allume l'interrupteur, et seulement s'il est compilé. Absente, 0, 2,
    /// négative ou d'un autre type (lue comme absente) : éteint.
    /// </summary>
    internal static bool Decide(bool compiledIn, int? registryValue) => compiledIn && registryValue == 1;

    /// <summary>
    /// État lu au démarrage. <see cref="Value"/> vaut null quand la valeur est absente, d'un
    /// autre type que DWORD, ou n'a pas été lue (interrupteur non compilé).
    /// </summary>
    public readonly record struct State(bool CompiledIn, bool Active, int? Value)
    {
        /// <summary>
        /// Ligne du journal de compatibilité. Elle distingue « non compilé » de « valeur non
        /// vue » : une app empaquetée pourrait ne pas voir une valeur HKCU écrite hors d'elle.
        /// Une valeur d'un autre type (REG_SZ « 1 ») est rejetée par le filtre : même libellé.
        /// </summary>
        public string Describe() =>
            "compiled=" + (CompiledIn ? "1" : "0")
            + " valeur=" + (Value is int v ? v.ToString(CultureInfo.InvariantCulture) : "absente-ou-non-dword")
            + " active=" + (Active ? "1" : "0");
    }

    /// <summary>
    /// Lit HKCU\<paramref name="subKey"/>, valeur <paramref name="valueName"/>, si et
    /// seulement si l'interrupteur est compilé. Appelé une fois, au démarrage, par l'hôte.
    /// </summary>
    public static State ReadAtStartup(string subKey, string valueName)
        => Evaluate(CompiledIn, () => ReadDword(HKEY_CURRENT_USER, subKey, valueName));

    /// <summary>La décision seule, lecteur injecté : le témoin vérifie qu'il n'est pas appelé.</summary>
    internal static State Evaluate(bool compiledIn, Func<int?> read)
    {
        if (!compiledIn) return new State(false, false, null);
        int? value = read();
        return new State(true, Decide(compiledIn, value), value);
    }

    internal static readonly IntPtr HKEY_CURRENT_USER = new(unchecked((int)0x80000001));
    internal static readonly IntPtr HKEY_LOCAL_MACHINE = new(unchecked((int)0x80000002));

    private const uint RRF_RT_REG_DWORD = 0x00000010;
    private const int ERROR_SUCCESS = 0;

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "RegGetValueW")]
    private static extern int RegGetValueDword(IntPtr hkey, string lpSubKey, string lpValue,
        uint dwFlags, IntPtr pdwType, out int pvData, ref uint pcbData);

    /// <summary>
    /// Une valeur DWORD, ou null (clé ou valeur absente, autre type). RegGetValueW ouvre, lit
    /// et referme en un appel ; le filtre de type fait échouer une valeur d'un autre type,
    /// comme dans PolicyManager.
    /// </summary>
    internal static int? ReadDword(IntPtr root, string subKey, string valueName)
    {
        uint size = sizeof(int);
        return RegGetValueDword(root, subKey, valueName, RRF_RT_REG_DWORD, IntPtr.Zero, out int value, ref size)
            == ERROR_SUCCESS ? value : null;
    }
}
