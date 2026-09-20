namespace TypingEngine.Windows;

/// <summary>
/// AG130-10 — détection d'un hook bas niveau décroché par Windows.
///
/// Windows retire silencieusement un hook <c>WH_KEYBOARD_LL</c> dont le rappel dépasse
/// <c>LowLevelHooksTimeout</c> (sous charge, à la reprise de veille). Rien n'est notifié :
/// l'application continue de croire son hook posé, l'utilisateur tape en AZERTY natif, et
/// seule la réinstallation périodique à l'aveugle — toutes les 60 s — finit par le réparer.
/// Jusqu'à une minute de frappe fausse, sans un mot dans le journal.
///
/// Le signal qui manquait : l'instant du dernier rappel reçu. Une sonde le compare à une
/// fenêtre pendant laquelle une frappe <b>a été vue par ailleurs</b> (<c>GetAsyncKeyState</c>).
/// Une frappe sans rappel, c'est un hook mort.
///
/// Cette classe ne porte que la décision, sans aucun appel système : elle est le seul endroit
/// où « combien de sondes muettes valent une réinstallation » est écrit, et le seul qu'un
/// témoin ait besoin d'éprouver.
/// </summary>
public sealed class HookSilenceWatchdog
{
    /// <summary>
    /// Deux sondes consécutives, pas une. Une frappe peut arriver juste après la lecture de
    /// l'horodatage et juste avant la fin de la fenêtre : une sonde isolée réinstallerait
    /// alors un hook parfaitement vivant. La réinstallation est bénigne mais pas gratuite —
    /// elle repose un hook global — et un faux positif récurrent ruinerait la valeur du
    /// journal, qui est justement de signaler un événement rare.
    /// </summary>
    public const int DefaultConsecutiveProbes = 2;

    private readonly int _consecutiveProbes;
    private int _silenceStreak;

    public HookSilenceWatchdog(int consecutiveProbes = DefaultConsecutiveProbes)
    {
        if (consecutiveProbes < 1)
            throw new ArgumentOutOfRangeException(nameof(consecutiveProbes));
        _consecutiveProbes = consecutiveProbes;
    }

    /// <summary>Sondes muettes accumulées depuis le dernier rappel observé.</summary>
    public int SilenceStreak => _silenceStreak;

    /// <summary>Réinstallations déclenchées depuis le démarrage.</summary>
    public int ReinstallCount { get; private set; }

    /// <summary>
    /// Verdict d'une sonde.
    /// </summary>
    /// <param name="keyPressObserved">
    /// Une touche a été pressée pendant la fenêtre écoulée, vue autrement que par le hook.
    /// </param>
    /// <param name="lastCallbackTicks">Instant du dernier rappel reçu du hook.</param>
    /// <param name="windowStartTicks">Début de la fenêtre que cette sonde referme.</param>
    /// <returns>Vrai quand le hook doit être réinstallé.</returns>
    public bool Observe(bool keyPressObserved, long lastCallbackTicks, long windowStartTicks)
    {
        // Un rappel reçu pendant la fenêtre prouve le hook vivant, que des touches aient
        // été vues ou non. Sans frappe, la sonde ne prouve rien : personne ne tapait.
        if (!keyPressObserved || lastCallbackTicks >= windowStartTicks)
        {
            _silenceStreak = 0;
            return false;
        }

        _silenceStreak++;
        if (_silenceStreak < _consecutiveProbes) return false;

        _silenceStreak = 0;
        ReinstallCount++;
        return true;
    }

    /// <summary>
    /// À appeler quand la sonde n'a pas lieu d'être — hook absent, application en pause ou
    /// suspendue pour compatibilité. Le silence y est voulu, et une série entamée avant la
    /// pause ne doit pas se poursuivre au travers.
    /// </summary>
    public void Suspend() => _silenceStreak = 0;
}
