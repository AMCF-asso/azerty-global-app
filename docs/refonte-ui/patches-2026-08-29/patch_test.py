# -*- coding: utf-8 -*-
"""Ajoute a ThemeWindowTests le temoin du fond de classe. Fichier en LF pur (verifie)."""
import sys
from pathlib import Path

PATH = Path(r"D:\My files\Keyboard Layouts\projects\azerty-global\components\microsoft-store"
            r"\src\AZERTYGlobal.Tests\ThemeWindowTests.cs")

USING_OLD = 'using System;\nusing AZERTYGlobal;\nusing Xunit;\n'
USING_NEW = 'using System;\nusing System.Runtime.InteropServices;\nusing AZERTYGlobal;\nusing Xunit;\n'

TAIL_OLD = '''    [Fact]
    public void EnableDarkTitleBar_SansFenetre_NeLevePas()
    {
        Win32.EnableDarkTitleBar(IntPtr.Zero);
    }
}
'''

TAIL_NEW = '''    [Fact]
    public void EnableDarkTitleBar_SansFenetre_NeLevePas()
    {
        Win32.EnableDarkTitleBar(IntPtr.Zero);
    }

    // ═══════════════════════════════════════════════════════════════
    // Fond de classe
    // ═══════════════════════════════════════════════════════════════

    private const uint OBJ_BRUSH = 2;

    [DllImport("gdi32.dll")]
    private static extern uint GetObjectType(IntPtr h);

    /// <summary>Garde le délégué en vie aussi longtemps que la classe de fenêtre du témoin.</summary>
    private static readonly Win32.WNDPROC TestWndProc = Win32.DefWindowProcW;

    /// <summary>
    /// Une brosse posée en fond de classe appartient au système, qui la détruit au
    /// désenregistrement de la classe. Le socle la prenait dans le cache de <see cref="Theme"/> :
    /// la fenêtre suivante recevait alors un handle mort, son fond restait blanc et ses étiquettes
    /// grises — mesuré sur Durée de pause le 2026-08-29, où GetObjectType tombe de 2 à 0. Le cache
    /// doit survivre au cycle complet d'une fenêtre.
    /// </summary>
    [Fact]
    public void FondDeClasse_NeConsommePasLaBrosseDuCacheDeTheme()
    {
        uint couleur = Theme.LightPalette.Paper;
        IntPtr brosse = Theme.Brush(couleur);
        Assert.Equal(OBJ_BRUSH, GetObjectType(brosse));

        const string classe = "AZERTYGlobal.Tests.FondDeClasse";
        IntPtr hInstance = Win32.GetModuleHandleW(null);
        var wc = new Win32.WNDCLASSEXW
        {
            cbSize = (uint)Marshal.SizeOf<Win32.WNDCLASSEXW>(),
            lpfnWndProc = TestWndProc,
            hInstance = hInstance,
            lpszClassName = classe,
        };
        Win32.RegisterClassExW(ref wc);

        IntPtr hwnd = Win32.CreateWindowExW(0, classe, string.Empty, Win32.WS_OVERLAPPED,
            0, 0, 10, 10, IntPtr.Zero, IntPtr.Zero, hInstance, IntPtr.Zero);
        Assert.NotEqual(IntPtr.Zero, hwnd);

        try
        {
            ThemeWindow.ApplyClassBackground(hwnd, couleur);
            ThemeWindow.ForgetClassBackground(hwnd);
        }
        finally
        {
            Win32.DestroyWindow(hwnd);
            Win32.UnregisterClassW(classe, hInstance);
        }

        Assert.Equal(OBJ_BRUSH, GetObjectType(brosse));
        Assert.Equal(brosse, Theme.Brush(couleur));
    }
}
'''


def main():
    data = PATH.read_bytes()
    crlf = data.count(b"\r\n")
    if crlf:
        print(f"REFUS : {crlf} CRLF")
        return 1
    text = data.decode("utf-8")
    for old in (USING_OLD, TAIL_OLD):
        if text.count(old) != 1:
            print(f"REFUS : ancre trouvee {text.count(old)} fois")
            return 1
    text = text.replace(USING_OLD, USING_NEW, 1).replace(TAIL_OLD, TAIL_NEW, 1)
    out = text.encode("utf-8")
    assert b"\r\n" not in out
    PATH.write_bytes(out)
    print(f"ecrit : {len(out)} octets, {out.count(chr(10).encode())} LF, 0 CRLF")
    return 0


if __name__ == "__main__":
    sys.exit(main())
