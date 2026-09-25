using TypingEngine.Windows;

namespace AZERTYGlobal;

/// <summary>Relie le moteur Windows aux services propres au produit AZERTY Global.</summary>
internal sealed class AzertyGlobalWindowsTypingHost : IWindowsTypingHost
{
    public uint ShortcutCharacterSearchVk => ConfigManager.ShortcutCharacterSearchVk;
    public uint ShortcutVirtualKeyboardVk => ConfigManager.ShortcutVirtualKeyboardVk;
    public bool CompatibilityDebugLog => ConfigManager.CompatibilityDebugLog;

    public string? GetCompatibilityOverride(string processName) =>
        ConfigManager.GetCompatibilityOverride(processName);

    public string AnonymizeProcessName(string? processName) =>
        ConfigManager.AnonymizeProcessName(processName);

    public void RecordEmittedText(string text) => UsageStats.RecordEmittedText(text);
    public void Log(string context, Exception exception) => ConfigManager.Log(context, exception);
    public void LogCompatibilityEvent(string eventName, string details) =>
        ConfigManager.LogCompatEvent(eventName, details);

    // Audit du 25/09 (M-07) : ce canal est appelé dans le rappel du hook quand SendInput
    // rend 0 (EmissionRefusee). L'écriture synchrone sous verrou y attendait le disque ; la
    // ligne part maintenant en file, écrite par le pool de threads dans l'ordre d'arrivée.
    // KeyboardHook s'en sert aussi (HookReinstallFailed), hors du rappel : même traitement.
    public void LogCompatibilityCriticalEvent(string eventName, string details) =>
        ConfigManager.LogCompatEventDeferred(eventName, details);
}
