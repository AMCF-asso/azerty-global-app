// Durées d'exercice (audit temps et horloge du 17/11/2026, TT-2, docs/audit-temps-horloge.md).
// Décision d'Antoine : la veille ne compte pas dans la durée d'un exercice.
using System.Runtime.InteropServices;

namespace AZERTYGlobal;

/// <summary>
/// Horloge des durées : le temps de fonctionnement de Windows, hors veille et hibernation
/// (<c>QueryUnbiasedInterruptTime</c>, Kernel32, Windows 7 et plus). Contrairement à l'heure
/// système, ce compte n'est réglé ni par l'utilisateur ni par le service de temps de Windows :
/// une heure qui recule ou avance pendant un exercice ne change pas sa durée, et une veille
/// en pleine frappe ne l'allonge pas. <c>Environment.TickCount64</c> compte la veille, d'où
/// ce choix. L'origine est arbitraire : seule une différence entre deux lectures a un sens.
/// </summary>
internal static class MonotonicClock
{
    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryUnbiasedInterruptTime(out ulong unbiasedTime);

    /// <summary>Temps écoulé hors veille depuis une origine fixe, au pas de 100 ns.</summary>
    internal static TimeSpan Now()
    {
        // L'appel n'échoue que sur un pointeur nul, impossible ici.
        QueryUnbiasedInterruptTime(out ulong ticks);
        return TimeSpan.FromTicks((long)ticks);
    }
}
