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
    /// <remarks><paramref name="File"/> ne porte pas le dossier du poste de build : le projet
    /// le remplace par « /_/ » (PathMap, audit de régression 1.3.0, RP4).
    /// <paramref name="Overflow"/> : le texte n'est pas coupé à « … », il est écrit en entier
    /// et déborde de son cadre (<see cref="PaperText.DrawLine"/>, RA16).</remarks>
    internal readonly record struct Passage(string Text, string? Shown, int Needed, int Available, string File, int Line,
        bool Overflow = false)
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

    /// <summary>Une ligne écrite sans coupe dans un cadre (<see cref="PaperText.DrawLine"/>) :
    /// plus large que lui, elle déborde sur son voisin ou sort de la fenêtre sans « … »
    /// (audit de régression 1.3.0, RA16). <paramref name="needed"/> est la largeur déjà
    /// mesurée par l'appelant : rien n'est remesuré.</summary>
    internal static void Line(string text, int needed, int available, string file, int line)
    {
        if (Observer is not { } observer) return;
        observer(new Passage(text, text, needed, Math.Max(0, available), file, line, Overflow: true));
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
