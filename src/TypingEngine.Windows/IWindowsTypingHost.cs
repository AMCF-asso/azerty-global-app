namespace TypingEngine.Windows;

/// <summary>
/// Services propres au produit hôte dont le moteur Windows a besoin sans dépendre
/// de sa configuration, de ses statistiques ou de son système de journalisation.
/// </summary>
public interface IWindowsTypingHost
{
    uint ShortcutCharacterSearchVk { get; }
    uint ShortcutVirtualKeyboardVk { get; }
    bool CompatibilityDebugLog { get; }

    string? GetCompatibilityOverride(string processName);
    string AnonymizeProcessName(string? processName);
    void RecordEmittedText(string text);
    void Log(string context, Exception exception);
    void LogCompatibilityEvent(string eventName, string details);
    void LogCompatibilityCriticalEvent(string eventName, string details);
}

internal sealed class NullWindowsTypingHost : IWindowsTypingHost
{
    public static NullWindowsTypingHost Instance { get; } = new();

    private NullWindowsTypingHost() { }

    public uint ShortcutCharacterSearchVk => 0;
    public uint ShortcutVirtualKeyboardVk => 0;
    public bool CompatibilityDebugLog => false;

    public string? GetCompatibilityOverride(string processName) => null;
    public string AnonymizeProcessName(string? processName) => processName ?? "unknown";
    public void RecordEmittedText(string text) { }
    public void Log(string context, Exception exception) { }
    public void LogCompatibilityEvent(string eventName, string details) { }
    public void LogCompatibilityCriticalEvent(string eventName, string details) { }
}
