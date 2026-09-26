using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Audit du 25/09, lot 8 (F-10) — la sous-classe commune des liens, <see cref="LinkBehavior"/>,
/// qui remplace les trois copies d'À propos, des Statistiques et de l'accueil.
///
/// Le lien est un vrai STATIC, enfant d'une fenêtre « message-only » (parent HWND_MESSAGE) :
/// rien ne s'affiche. Les messages sont envoyés au lien comme IsDialogMessageW et la souris
/// les enverraient. Le cadre de focus (K5) se peint et ne se vérifie qu'à l'écran : recette.
/// </summary>
public sealed class LinkBehaviorTests : IDisposable
{
    private static readonly IntPtr HWND_MESSAGE = new(-3);
    private const uint SS_NOTIFY = 0x0100;
    private const int LinkId = 42;
    private const int VK_TAB = 0x09;
    private const int VK_RETURN = 0x0D;
    private const int VK_ESCAPE = 0x1B;
    private const long DLGC_STATIC = 0x0100;

    private readonly string _className = "AZERTYGlobal.Tests.Liens." + Guid.NewGuid().ToString("N");
    private readonly Win32.WNDPROC _ownerProc;
    private readonly List<(IntPtr WParam, IntPtr LParam)> _commands = new();
    private readonly IntPtr _owner;
    private readonly IntPtr _link;

    public LinkBehaviorTests()
    {
        _ownerProc = (h, m, w, l) =>
        {
            if (m == Win32.WM_COMMAND)
                _commands.Add((w, l));
            return Win32.DefWindowProcW(h, m, w, l);
        };
        Assert.True(NativeWindow.RegisterClass(_className, _ownerProc));
        IntPtr module = Win32.GetModuleHandleW(null);
        _owner = Win32.CreateWindowExW(0, _className, string.Empty, 0, 0, 0, 0, 0,
            HWND_MESSAGE, IntPtr.Zero, module, IntPtr.Zero);
        _link = Win32.CreateWindowExW(0, "STATIC", "lien",
            Win32.WS_CHILD | SS_NOTIFY | Win32.WS_TABSTOP, 0, 0, 80, 20,
            _owner, (IntPtr)LinkId, module, IntPtr.Zero);
        Assert.NotEqual(IntPtr.Zero, _owner);
        Assert.NotEqual(IntPtr.Zero, _link);
    }

    public void Dispose()
    {
        Win32.DestroyWindow(_owner);
        NativeWindow.UnregisterClass(_className);
        GC.KeepAlive(_ownerProc);
    }

    private LinkBehavior Attach(Action? onEscape = null, bool staticDialogCode = true, Func<uint, bool>? swallow = null)
    {
        var links = new LinkBehavior(() => _owner, onEscape ?? (() => { }), focusFrame: true, staticDialogCode, swallow);
        links.Attach(_link, 7);
        return links;
    }

    private void Key(int vk) => Win32.SendMessageW(_link, Win32.WM_KEYDOWN, (IntPtr)vk, IntPtr.Zero);

    private long DialogCode(int vk)
    {
        var msg = new Win32.MSG { hwnd = _link, message = Win32.WM_KEYDOWN, wParam = (IntPtr)vk };
        IntPtr lParam = Marshal.AllocHGlobal(Marshal.SizeOf<Win32.MSG>());
        try
        {
            Marshal.StructureToPtr(msg, lParam, false);
            return Win32.SendMessageW(_link, Win32.WM_GETDLGCODE, (IntPtr)vk, lParam).ToInt64();
        }
        finally
        {
            Marshal.FreeHGlobal(lParam);
        }
    }

    [Fact]
    public void Survol_LeLienDevientActifPuisRedevientNormal()
    {
        var links = Attach();
        Win32.SendMessageW(_link, Win32.WM_MOUSEMOVE, IntPtr.Zero, IntPtr.Zero);
        Assert.Equal(_link, links.Hovered);
        Assert.True(links.IsActive(_link));

        Win32.SendMessageW(_link, Win32.WM_MOUSELEAVE, IntPtr.Zero, IntPtr.Zero);
        Assert.Equal(IntPtr.Zero, links.Hovered);
        links.Detach();
    }

    [Fact]
    public void Entrée_ActiveLeLienCommeUnClic()
    {
        var links = Attach();
        Key(VK_RETURN);
        Assert.Equal(new[] { ((IntPtr)LinkId, _link) }, _commands);
        links.Detach();
    }

    [Fact]
    public void Échap_FermeLaFenêtre()
    {
        // Le lien garde la touche (WANTALLKEYS) : IsDialogMessageW n'en fait pas un IDCANCEL.
        int fermetures = 0;
        var links = Attach(onEscape: () => fermetures++);
        Key(VK_ESCAPE);
        Assert.Equal(1, fermetures);
        Assert.Empty(_commands);
        links.Detach();
    }

    [Fact]
    public void Tab_EstRenduÀLaNavigation_LeResteEstGardéParLeLien()
    {
        var links = Attach();
        Assert.Equal(0, DialogCode(VK_TAB) & DialogNavigation.DLGC_WANTALLKEYS);
        Assert.NotEqual(0, DialogCode(VK_RETURN) & DialogNavigation.DLGC_WANTALLKEYS);
        links.Detach();
    }

    [Fact]
    public void CodeDeDialogue_PartDeLaRéponseDuStatic_OuDeZéro()
    {
        // À propos et les Statistiques gardent la réponse du STATIC ; l'accueil part de zéro.
        var links = Attach(staticDialogCode: true);
        Assert.Equal(DLGC_STATIC, DialogCode(VK_TAB) & DLGC_STATIC);
        links.Detach();

        links = Attach(staticDialogCode: false);
        Assert.Equal(0, DialogCode(VK_TAB));
        Assert.Equal(DialogNavigation.DLGC_WANTALLKEYS, DialogCode(VK_RETURN));
        links.Detach();
    }

    [Fact]
    public void SaisieEnPause_LeLienNeRépondPlus()
    {
        var links = Attach(swallow: msg => msg == Win32.WM_KEYDOWN);
        Key(VK_RETURN);
        Assert.Empty(_commands);
        links.Detach();
    }

    [Fact]
    public void Détaché_LeLienRedevientUnStaticOrdinaire()
    {
        var links = Attach();
        Assert.True(links.Contains(_link));
        links.Detach();

        Key(VK_RETURN);
        Assert.Empty(_commands);
        Assert.False(links.Contains(_link));
        Assert.False(links.TrySetHandCursor(_link));
    }

    [Fact]
    public void CodeDeDialogue_SansMessage_GardeToutesLesTouches()
    {
        Assert.Equal(DLGC_STATIC | DialogNavigation.DLGC_WANTALLKEYS,
            DialogNavigation.DialogCodeKeepingTab(DLGC_STATIC, IntPtr.Zero));
    }
}
