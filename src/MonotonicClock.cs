// Durées d'exercice (audit temps et horloge du 17/11/2026, TT-2, docs/audit-temps-horloge.md).
// Décision d'Antoine : la veille ne compte pas dans la durée d'un exercice.
using System.Runtime.InteropServices;

namespace AZERTYGlobal;

/// <summary>
/// Horloge des durées : le temps de fonctionnement de Windows, hors veille et hibernation
/// (<c>QueryUnbiasedInterruptTimePrecise</c>, Windows 10 et plus, sous le <c>MinVersion</c>
/// 10.0.17763 du manifeste). Contrairement à l'heure système, ce compte n'est réglé ni par
/// l'utilisateur ni par le service de temps de Windows : une heure qui recule ou avance pendant
/// un exercice ne change pas sa durée, et une veille en pleine frappe ne l'allonge pas.
/// <c>Environment.TickCount64</c> compte la veille, d'où ce choix. L'origine est arbitraire :
/// seule une différence entre deux lectures a un sens.
/// <para>Variante « Precise » : <c>QueryUnbiasedInterruptTime</c> n'avance qu'au tic d'horloge
/// (0,5 à 15,6 ms selon la machine). Deux lectures dans le même tic donnaient une durée nulle,
/// donc aucune vitesse (verify du 18/11 : <c>DefiRecordTests</c>, « vitesse mesurée : 0 »).
/// La variante précise lit le compteur matériel : pas de 100 ns, toujours hors veille.</para>
/// </summary>
internal static class MonotonicClock
{
    // Jeu d'API et non Kernel32 : la documentation cite Kernel32.dll, mais Kernel32 n'exporte pas
    // ce point d'entrée (EntryPointNotFoundException mesurée le 18/11 sous Windows 11 26200) ;
    // il vit dans KernelBase, que le jeu d'API « realtime » l1-1-1 désigne depuis Windows 10.
    [DllImport("api-ms-win-core-realtime-l1-1-1.dll")]
    private static extern void QueryUnbiasedInterruptTimePrecise(out ulong unbiasedTime);

    /// <summary>Temps écoulé hors veille depuis une origine fixe, au pas de 100 ns.</summary>
    internal static TimeSpan Now()
    {
        QueryUnbiasedInterruptTimePrecise(out ulong ticks);
        return TimeSpan.FromTicks((long)ticks);
    }
}
