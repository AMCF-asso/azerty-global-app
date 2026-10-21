// Rampe typographique de l'interface — lot visuel 1.3.0 (QCM du 28/09).
namespace AZERTYGlobal;

/// <summary>
/// Les trois tailles du texte d'interface, celles de Windows 11 (px à 96 DPI) : légende 12,
/// corps 14, titre 20 ; deux graisses, normale 400 et semi-grasse 600. Les glyphes du
/// clavier et le caractère cible des Leçons ne sont pas du texte d'interface : ils gardent
/// leurs polices propres.
/// </summary>
static class TypeRamp
{
    internal const int Caption = 12;
    internal const int Body = 14;
    internal const int Title = 20;

    internal const int Regular = 400;
    /// <summary>Gravures du clavier papier seulement (AG Mono 500), pas le texte d’interface.</summary>
    internal const int Medium = 500;
    internal const int Semibold = 600;

    internal const string Family = "Segoe UI";

    /// <summary>
    /// Police Segoe UI de hauteur de caractère <paramref name="size"/> px à 96 DPI, portée à
    /// l'échelle <paramref name="scale"/> (1 à 96 DPI), en ClearType. L'arrondi est celui
    /// des fenêtres (troncature), pour qu'une taille donnée rende les mêmes pixels partout.
    /// </summary>
    internal static IntPtr Create(int size, int weight, float scale, bool underline = false, bool italic = false)
        => Win32.CreateFontW(-(int)(size * scale), 0, 0, 0, weight, italic ? 1u : 0u, underline ? 1u : 0u,
            0, 0, 0, 0, 5, 0, Family);

    /// <summary>
    /// A9 : la même police dans une famille embarquée (<see cref="AppFonts.Text"/>…), pour ce
    /// que <see cref="FontSpec"/> ne décrit pas (le soulignement d'un lien STATIC). La HFONT
    /// appartient à l'appelant, qui la détruit.
    /// </summary>
    internal static IntPtr CreateIn(string family, int size, int weight, float scale, bool underline = false)
    {
        AppFonts.EnsureLoaded();
        return Win32.CreateFontW(-(int)(size * scale), 0, 0, 0, weight, 0, underline ? 1u : 0u,
            0, 0, 0, 0, 5, 0, family);
    }
}
