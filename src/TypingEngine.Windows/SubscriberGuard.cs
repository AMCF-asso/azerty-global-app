namespace TypingEngine.Windows;

/// <summary>
/// Prévient les abonnés d'un événement du moteur un par un, chacun dans son propre <c>try</c>
/// (audit des chemins d'erreur, CE-T1 : constats CE-01 et CE-02).
///
/// Ces événements partent du rappel du hook clavier ou d'un rappel WinEvent. Avant, une exception
/// d'un abonné (fenêtre détruite, logique d'une leçon) remontait dans le moteur : le hook laissait
/// alors passer la touche d'origine (caractère doublé, touche coincée, Verr. Maj. transmise) et le
/// moniteur du premier plan remplaçait le bon état par le repli « champ sécurisé ». La décision du
/// moteur est prise avant de prévenir ; l'erreur d'un abonné est journalisée et ne touche plus ni à
/// cette décision ni aux autres abonnés.
/// </summary>
internal static class SubscriberGuard
{
    public static void Raise(Action? handlers, IWindowsTypingHost host, string context)
    {
        if (handlers == null) return;
        foreach (var handler in handlers.GetInvocationList())
        {
            try { ((Action)handler)(); }
            catch (Exception ex) { LogSafely(host, context, ex); }
        }
    }

    public static void Raise<T>(Action<T>? handlers, T arg, IWindowsTypingHost host, string context)
    {
        if (handlers == null) return;
        foreach (var handler in handlers.GetInvocationList())
        {
            try { ((Action<T>)handler)(arg); }
            catch (Exception ex) { LogSafely(host, context, ex); }
        }
    }

    public static void Raise<T1, T2>(Action<T1, T2>? handlers, T1 arg1, T2 arg2, IWindowsTypingHost host, string context)
    {
        if (handlers == null) return;
        foreach (var handler in handlers.GetInvocationList())
        {
            try { ((Action<T1, T2>)handler)(arg1, arg2); }
            catch (Exception ex) { LogSafely(host, context, ex); }
        }
    }

    // Le journal de l'hôte absorbe ses propres erreurs ; ce try garde la promesse même s'il lève.
    private static void LogSafely(IWindowsTypingHost host, string context, Exception ex)
    {
        try { host.Log(context, ex); } catch { }
    }
}
