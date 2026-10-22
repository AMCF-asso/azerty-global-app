// Contraste élevé des fenêtres papier — audit 1.3.0, A12 et A14 (lot 3a « fenêtres »).
using System.Runtime.InteropServices;

namespace AZERTYGlobal;

/// <summary>
/// Les couleurs d'une fenêtre papier, par rôle : celles de <see cref="PaperTheme"/>, ou celles
/// du thème système quand le contraste élevé de Windows est actif. Le modèle est la palette de
/// l'accueil (<c>OnboardingWindow.PaletteFor</c>) : fond et texte de fenêtre, lien en « hot
/// light », plein en couleurs de sélection. Les Paramètres la tenaient à moitié (onglets et
/// boutons au système, fond et texte papier) : un texte sombre sur un bouton noir était possible.
/// </summary>
internal readonly record struct PaperPalette(uint Paper, uint Surface, uint Ink, uint Muted, uint Border,
    uint Action, uint ActionFill, uint OnAction, uint Pale, uint PaleText, uint Error, uint Success,
    uint ActionTint, uint FunctionKey, bool HighContrast)
{
    // Indices de GetSysColor.
    private const int COLOR_WINDOW = 5;
    private const int COLOR_WINDOWTEXT = 8;
    private const int COLOR_HIGHLIGHT = 13;
    private const int COLOR_HIGHLIGHTTEXT = 14;
    private const int COLOR_GRAYTEXT = 17;
    private const int COLOR_HOTLIGHT = 26;

    /// <summary>La palette papier, hors contraste élevé.</summary>
    internal static readonly PaperPalette Default = new(PaperTheme.Paper, PaperTheme.Surface, PaperTheme.Ink,
        PaperTheme.TextSecondary, PaperTheme.Border, PaperTheme.Action, PaperTheme.Action, PaperTheme.OnAction,
        PaperTheme.Pale, PaperTheme.PaleText, PaperTheme.Error, PaperTheme.Success, PaperTheme.ActionTint,
        PaperTheme.FunctionKey, HighContrast: false);

    /// <summary>
    /// La palette, décidée sans toucher au réglage de Windows. En contraste élevé, tout ce qui
    /// se lit passe en texte de fenêtre sur fond de fenêtre ; l'action en « hot light » (liens,
    /// traits) et, pleine, en couleurs de sélection ; le texte désactivé ou à venir en gris
    /// système. Erreur et réussite n'ont pas de couleur système : elles prennent le texte, et
    /// l'état se lit à l'épaisseur du trait ou au message.
    /// </summary>
    internal static PaperPalette For(bool highContrast, Func<int, uint> sysColor)
    {
        if (!highContrast)
            return Default;
        uint window = sysColor(COLOR_WINDOW), text = sysColor(COLOR_WINDOWTEXT);
        uint hot = sysColor(COLOR_HOTLIGHT), gray = sysColor(COLOR_GRAYTEXT);
        return new PaperPalette(Paper: window, Surface: window, Ink: text, Muted: text, Border: text,
            Action: hot, ActionFill: sysColor(COLOR_HIGHLIGHT), OnAction: sysColor(COLOR_HIGHLIGHTTEXT),
            Pale: text, PaleText: gray, Error: text, Success: text, ActionTint: window, FunctionKey: window,
            HighContrast: true);
    }

    /// <summary>La palette du moment : à relire sur WM_SYSCOLORCHANGE et WM_SETTINGCHANGE.</summary>
    internal static PaperPalette Now() => For(AZERTYGlobal.HighContrast.IsOn(), index => GetSysColor(index));

    [DllImport("user32.dll")]
    private static extern uint GetSysColor(int index);
}

/// <summary>Le contraste élevé de Windows (SPI_GETHIGHCONTRAST), lu à la demande.</summary>
internal static class HighContrast
{
    private const uint SPI_GETHIGHCONTRAST = 0x0042;
    private const uint HCF_HIGHCONTRASTON = 0x0001;

    [StructLayout(LayoutKind.Sequential)]
    private struct HIGHCONTRAST
    {
        public uint cbSize;
        public uint dwFlags;
        public IntPtr lpszDefaultScheme;
    }

    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW")]
    private static extern bool SystemParametersInfoHighContrast(uint action, uint param, ref HIGHCONTRAST value, uint winIni);

    internal static bool IsOn()
    {
        var value = new HIGHCONTRAST { cbSize = (uint)Marshal.SizeOf<HIGHCONTRAST>() };
        return SystemParametersInfoHighContrast(SPI_GETHIGHCONTRAST, value.cbSize, ref value, 0)
            && (value.dwFlags & HCF_HIGHCONTRASTON) != 0;
    }
}
