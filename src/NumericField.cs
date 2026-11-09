using System.Text;

namespace AZERTYGlobal;

/// <summary>
/// Champs numériques (audit 09/11, E28). <c>ES_NUMBER</c> ne filtre que la frappe : un collage
/// de « abc » ou de « 12 mn » entrait tel quel, et le champ montrait autre chose que ce qu'il
/// comptait. Chaque champ numérique passe son texte par <see cref="Digits"/> à chaque
/// <c>EN_CHANGE</c> : ce qui n'est pas un chiffre est retiré, et le champ ne garde que ce qu'il
/// sait lire.
/// </summary>
internal static class NumericField
{
    private const uint EM_SETSEL = 0x00B1;
    private const uint EM_LIMITTEXT = 0x00C5;

    /// <summary>Les chiffres décimaux ASCII du texte, dans l'ordre. Ni espace, ni signe, ni
    /// chiffre d'un autre alphabet : <c>int.TryParse</c> les lit, la saisie ne les compte pas.</summary>
    internal static string Digits(string text)
    {
        var kept = new StringBuilder(text.Length);
        foreach (char c in text)
            if (c >= '0' && c <= '9')
                kept.Append(c);
        return kept.ToString();
    }

    /// <summary>Limite la longueur du champ, collage compris (<c>EM_LIMITTEXT</c> le tronque aussi).</summary>
    internal static void Limit(IntPtr edit, int maxChars) =>
        Win32.SendMessageW(edit, EM_LIMITTEXT, (IntPtr)maxChars, IntPtr.Zero);

    /// <summary>Retire du champ ce qui n'est pas un chiffre. Rend vrai si le texte a changé ; le
    /// curseur va alors en fin de texte. L'appelant tient son garde de réentrance
    /// (<c>SetWindowText</c> relance <c>EN_CHANGE</c>).</summary>
    internal static bool Sanitize(IntPtr edit)
    {
        var buffer = new StringBuilder(Math.Max(16, Win32.GetWindowTextLengthW(edit) + 1));
        Win32.GetWindowTextW(edit, buffer, buffer.Capacity);
        string text = buffer.ToString();
        string digits = Digits(text);
        if (digits == text) return false;
        Win32.SetWindowTextW(edit, digits);
        Win32.SendMessageW(edit, EM_SETSEL, (IntPtr)digits.Length, (IntPtr)digits.Length);
        return true;
    }
}
