namespace TypingEngine.Windows;

/// <summary>Résultat SendInput : événements demandés/acceptés, pas une preuve de saisie dans le contrôle cible.</summary>
public readonly record struct TextEmissionResult(int RequestedEvents, uint SentEvents, bool Blocked = false)
{
    public bool IsComplete => !Blocked && RequestedEvents > 0 && SentEvents == (uint)RequestedEvents;
}
