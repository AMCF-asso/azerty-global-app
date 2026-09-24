// Séances d'apprentissage ouvertes et fin de la dernière — décision B d'Antoine du
// 2026-09-24 : aucune sollicitation d'avis pendant une séance de Leçons ou un tutoriel,
// ni dans les dix minutes qui suivent (ReviewPromptGate.LearningAllowsPrompt).
namespace AZERTYGlobal;

/// <summary>
/// Tient qui a une séance ouverte : la fenêtre Leçons tant qu'elle est affichée, le
/// tutoriel de l'accueil (<c>LearningModule</c>) tant qu'il vit. Chaque propriétaire est
/// compté une fois, quel que soit le nombre d'appels : <c>Show</c> sur une fenêtre déjà
/// visible, <c>Hide</c> ou <c>Dispose</c> répétés ne dérèglent rien. Seule une vraie
/// fermeture date la fin de séance. État du processus, rien n'est persisté : après un
/// redémarrage, aucune séance n'est réputée récente.
/// </summary>
internal static class LearningSessionTracker
{
    private static readonly object _lock = new();
    private static readonly HashSet<object> _open = new(ReferenceEqualityComparer.Instance);
    private static long _lastCloseTick; // 0 : aucune séance refermée dans ce processus

    internal static void Opened(object owner)
    {
        lock (_lock) { _open.Add(owner); }
    }

    internal static void Closed(object owner)
    {
        lock (_lock)
        {
            if (_open.Remove(owner)) _lastCloseTick = Environment.TickCount64;
        }
    }

    internal static bool IsOpen { get { lock (_lock) { return _open.Count > 0; } } }

    /// <summary>Millisecondes depuis la dernière fin de séance, ou <see cref="long.MaxValue"/>
    /// si aucune ne s'est refermée dans ce processus.</summary>
    internal static long MillisecondsSinceLastClose
    {
        get
        {
            lock (_lock)
            {
                return _lastCloseTick == 0 ? long.MaxValue : Environment.TickCount64 - _lastCloseTick;
            }
        }
    }

    /// <summary>Remise à zéro entre deux tests.</summary>
    internal static void ResetForTests()
    {
        lock (_lock) { _open.Clear(); _lastCloseTick = 0; }
    }
}
