using System.Runtime.CompilerServices;

namespace AZERTYGlobal;

/// <summary>
/// Couture de test (audit UX du 09/11, W24 : « troncature EN non vérifiée ») : chaque texte qui
/// passe par une coupe à points de suspension (les méthodes Ellipsize, et DrawText avec
/// DT_END_ELLIPSIS) est signalé à <see cref="Observer"/>, coupé ou non, avec la largeur qu'il
/// demande et celle dont il dispose. Le test ouvre les fenêtres, les repeint et refuse qu'une
/// chaîne d'interface soit coupée. Sans observateur, rien n'est mesuré : le produit ne paie qu'un
/// test de nullité.
/// </summary>
internal static class TruncationProbe
{
    /// <summary>Un passage par une coupe. <paramref name="Shown"/> est null quand DrawText coupe
    /// lui-même (le texte rendu n'est pas connu) ; <paramref name="File"/> et
    /// <paramref name="Line"/> désignent l'appel qui a demandé la coupe.</summary>
    internal readonly record struct Passage(string Text, string? Shown, int Needed, int Available, string File, int Line)
    {
        /// <summary>Le texte n'a pas tenu : coupé, ou effacé faute de toute place.</summary>
        public bool Cut => Text.Length > 0 && Needed > Available;
    }

    /// <summary>Observateur des tests ; null en production.</summary>
    internal static Action<Passage>? Observer;

    /// <summary>Après une méthode Ellipsize : <paramref name="shown"/> est ce qu'elle rend.</summary>
    internal static void Ellipsized(IntPtr hdc, string text, string shown, FontSpec font, int maxWidth, string file, int line)
    {
        if (Observer is not { } observer) return;
        int needed = text.Length == 0 ? 0 : PaperText.Width(hdc, text, font);
        observer(new Passage(text, shown, needed, Math.Max(0, maxWidth), file, line));
    }

    /// <summary>Avant un DrawText sur une ligne : la police est déjà choisie dans
    /// <paramref name="hdc"/>. Sans DT_END_ELLIPSIS, DrawText ne coupe pas : rien n'est signalé.</summary>
    internal static void DrawText(IntPtr hdc, string text, Win32.RECT rect, uint format,
        [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
        if (Observer is not { } observer || (format & Win32.DT_END_ELLIPSIS) == 0) return;
        var calc = new Win32.RECT { left = 0, top = 0, right = 0, bottom = 0 };
        if (text.Length > 0)
            Win32.DrawTextW(hdc, text, -1, ref calc, Win32.DT_CALCRECT | Win32.DT_SINGLELINE | Win32.DT_NOPREFIX);
        observer(new Passage(text, null, calc.right - calc.left, Math.Max(0, rect.right - rect.left), file, line));
    }
}
