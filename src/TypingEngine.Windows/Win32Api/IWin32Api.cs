// Abstraction des P/Invoke critiques utilisés par KeyMapper et ForegroundMonitor.
// Permet d'injecter une implémentation mock dans les tests unitaires.
//
// KeyboardHook conserve ses appels statiques SetWindowsHookEx : le callback natif
// reste couvert par les smoke tests, tandis que sa logique de mapping est testée
// via KeyMapper et cette interface.

namespace TypingEngine.Windows;

public interface IWin32Api
{
    // Layout / clavier
    short VkKeyScanExW(char ch, IntPtr hkl);
    uint MapVirtualKeyExW(uint code, uint mapType, IntPtr hkl);
    short GetKeyState(int vk);
    short GetAsyncKeyState(int vk);
    IntPtr GetKeyboardLayout(uint threadId);
    int ToUnicode(uint vk, uint scan, byte[] state, System.Text.StringBuilder buffer, int capacity, uint flags);
    int ToUnicodeEx(uint vk, uint scan, byte[] state, System.Text.StringBuilder buffer, int capacity, uint flags, IntPtr hkl);

    // Injection de frappes
    uint SendInput(Win32.INPUT[] inputs);

    /// <summary>
    /// Code d'erreur Win32 du dernier SendInput qui a rendu 0 (AG130-09).
    /// Membre par defaut : une implementation qui ne le renseigne pas rend 0,
    /// ce qui reste lisible dans le journal comme « cause inconnue ».
    /// </summary>
    int LastSendInputError => 0;

    // Foreground / process inspection
    IntPtr GetForegroundWindow();

    /// <summary>
    /// Récupère le nom court (ex: "Minecraft.Windows.exe"), le chemin complet, le HKL
    /// du thread de la fenêtre capturée et le PID, sans relire le premier plan.
    /// Retourne false si la fenêtre ou son processus ne peut pas être inspecté.
    /// </summary>
    bool TryGetWindowProcess(IntPtr window, out string? processName, out string? fullPath, out IntPtr hkl, out uint pid);

    /// <summary>Retourne l'instant de création du processus en ticks FILETIME UTC.</summary>
    bool TryGetProcessStartTime(uint pid, out long startTimeTicks);

    /// <summary>
    /// Détection du contrôle sécurisé dans la fenêtre capturée. Cette méthode
    /// est appelée par ForegroundMonitor, jamais depuis le callback clavier.
    /// </summary>
    bool IsWindowPasswordField(IntPtr window);

    /// <summary>
    /// Énumère les modules (DLL) chargés dans un process. Retourne false si OpenProcess
    /// échoue (process protégé par anti-cheat ou privilèges insuffisants).
    /// </summary>
    bool TryEnumProcessModules(uint pid, out string[] moduleFileNames);

    // WinEvent (foreground change)
    IntPtr SetWinEventHook(uint eventMin, uint eventMax, Win32.WinEventDelegate cb);
    bool UnhookWinEvent(IntPtr hook);
}
