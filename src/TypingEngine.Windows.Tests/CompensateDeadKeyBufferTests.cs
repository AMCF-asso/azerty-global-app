using System.Text;

namespace TypingEngine.Windows.Tests;

/// <summary>
/// Audit du 25/09, M-05 : CompensateSystemDeadKey réutilise son état de clavier et son tampon au
/// lieu de les allouer à chaque caractère. Ce témoin fixe ce que ToUnicode reçoit : les bits de
/// Maj et d'AltGr de la frappe en cours, jamais ceux de la précédente, le reste de l'état à zéro,
/// et un tampon vide de 8 caractères.
/// </summary>
public sealed class CompensateDeadKeyBufferTests
{
    private const uint SC_A = 0x10, VK_A = 0x41;
    private const uint VK_LSHIFT = 0xA0, VK_RMENU = 0xA5;

    /// <summary>Relève chaque appel à ToUnicode ; le reste va au mock.</summary>
    private sealed class ApiEnregistreuse : IWin32Api
    {
        public readonly MockWin32Api Inner = new();
        public readonly List<(byte Shift, byte LShift, byte RAlt, byte LCtrl, int Autres, int Capacite, int Longueur)> Appels = new();

        public int ToUnicode(uint vk, uint scan, byte[] state, StringBuilder buffer, int capacity, uint flags)
        {
            int autres = state.Sum(b => (int)b) - state[0x10] - state[0xA0] - state[0xA5] - state[0xA2];
            Appels.Add((state[0x10], state[0xA0], state[0xA5], state[0xA2], autres, capacity, buffer.Length));
            buffer.Append('x'); // Windows écrit dans le tampon : il doit être vide à l'appel suivant.
            return Inner.ToUnicode(vk, scan, state, buffer, capacity, flags);
        }

        public short VkKeyScanExW(char ch, IntPtr hkl) => Inner.VkKeyScanExW(ch, hkl);
        public uint MapVirtualKeyExW(uint code, uint mapType, IntPtr hkl) => Inner.MapVirtualKeyExW(code, mapType, hkl);
        public short GetKeyState(int vk) => Inner.GetKeyState(vk);
        public short GetAsyncKeyState(int vk) => Inner.GetAsyncKeyState(vk);
        public IntPtr GetKeyboardLayout(uint threadId) => Inner.GetKeyboardLayout(threadId);
        public int ToUnicodeEx(uint vk, uint scan, byte[] state, StringBuilder buffer, int capacity, uint flags, IntPtr hkl) =>
            Inner.ToUnicodeEx(vk, scan, state, buffer, capacity, flags, hkl);
        public uint SendInput(Win32.INPUT[] inputs) => Inner.SendInput(inputs);
        public int LastSendInputError => Inner.LastSendInputError;
        public IntPtr GetForegroundWindow() => Inner.GetForegroundWindow();
        public bool TryGetWindowProcess(IntPtr window, out string? processName, out string? fullPath, out IntPtr hkl, out uint pid) =>
            Inner.TryGetWindowProcess(window, out processName, out fullPath, out hkl, out pid);
        public bool TryGetProcessStartTime(uint pid, out long startTimeTicks) => Inner.TryGetProcessStartTime(pid, out startTimeTicks);
        public bool IsWindowPasswordField(IntPtr window) => Inner.IsWindowPasswordField(window);
        public bool TryEnumProcessModules(uint pid, out string[] moduleFileNames) => Inner.TryEnumProcessModules(pid, out moduleFileNames);
        public IntPtr SetWinEventHook(uint eventMin, uint eventMax, Win32.WinEventDelegate cb) => Inner.SetWinEventHook(eventMin, eventMax, cb);
        public bool UnhookWinEvent(IntPtr hook) => Inner.UnhookWinEvent(hook);
    }

    [Fact]
    public void ToUnicodeNeVoitQueLesModificateursDeLaFrappeEnCours()
    {
        var api = new ApiEnregistreuse();
        var layout = new Layout();
        layout.Keys[SC_A] = new KeyDefinition { Position = "D01", Scancode = SC_A, Base = "a", Shift = "A" };
        var mapper = new KeyMapper(layout, api);
        api.Appels.Clear(); // le constructeur vide la touche morte système avec ses propres tampons

        // « a » avec Maj tenue.
        api.Inner.AsyncKeyStateScript[(int)VK_LSHIFT] = unchecked((short)0x8000);
        mapper.TrackModifiers(VK_LSHIFT, 0x2A, 0, true);
        mapper.ProcessKey(VK_A, SC_A, 0, true);
        mapper.ProcessKey(VK_A, SC_A, 0, false);
        mapper.TrackModifiers(VK_LSHIFT, 0x2A, 0, false);
        api.Inner.AsyncKeyStateScript.Remove((int)VK_LSHIFT);

        // « a » avec AltGr tenue : plus aucun bit de Maj.
        api.Inner.AsyncKeyStateScript[(int)VK_RMENU] = unchecked((short)0x8000);
        mapper.TrackModifiers(VK_RMENU, 0x38, 1, true);
        mapper.ProcessKey(VK_A, SC_A, 0, true);
        mapper.ProcessKey(VK_A, SC_A, 0, false);
        mapper.TrackModifiers(VK_RMENU, 0x38, 1, false);
        api.Inner.AsyncKeyStateScript.Remove((int)VK_RMENU);

        // « a » seul : état entièrement à zéro.
        mapper.ProcessKey(VK_A, SC_A, 0, true);
        mapper.ProcessKey(VK_A, SC_A, 0, false);

        Assert.Equal(new (byte, byte, byte, byte, int, int, int)[]
        {
            (0x80, 0x80, 0, 0, 0, 8, 0),
            (0, 0, 0x80, 0x80, 0, 8, 0),
            (0, 0, 0, 0, 0, 8, 0),
        }, api.Appels);
    }
}
