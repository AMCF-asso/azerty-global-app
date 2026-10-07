// Progression par geste (refonte 1.3.0, décision d'Antoine du 07/10) : une seule
// progression, comptée par caractère à geste, et non par exercice. Elle survit aux
// changements de texte des leçons et de langue de l'interface, que la clé d'exercice
// (hash du contenu et de la consigne) ne survit pas (audit du 25/09, L-12 et L-13).
namespace AZERTYGlobal;

/// <summary>
/// Maîtrise d'un caractère : combien de fois il a été demandé, combien de fautes, et sa
/// boîte de répétition espacée (0 = jamais réussi sans faute, 5 = acquis).
/// </summary>
internal sealed class GestureMasteryEntry
{
    public GestureMasteryEntry(string character)
    {
        Character = character;
    }

    public string Character { get; }
    public int Attempts { get; set; }
    public int Errors { get; set; }
    public int Box { get; set; }
    public DateTimeOffset? LastReviewUtc { get; set; }
    public DateTimeOffset? DueUtc { get; set; }

    /// <summary>Maîtrise de 0 à 1, lue par la carte du clavier.</summary>
    public double Level => Math.Clamp(Box / (double)GestureMastery.MaxBox, 0, 1);

    public bool IsDue(DateTimeOffset now) => DueUtc is not { } due || due <= now;

    public GestureMasteryEntry Clone() => new(Character)
    {
        Attempts = Attempts,
        Errors = Errors,
        Box = Box,
        LastReviewUtc = LastReviewUtc,
        DueUtc = DueUtc,
    };
}

internal static class GestureMastery
{
    public const int MaxBox = 5;

    /// <summary>Délai avant la révision suivante, par boîte (Leitner) : une faute renvoie en
    /// boîte 1, revue dès la séance suivante ; chaque réussite sans faute, une fois échue,
    /// fait monter d'une boîte.</summary>
    private static readonly TimeSpan[] Intervals =
    {
        TimeSpan.Zero,
        TimeSpan.FromDays(1),
        TimeSpan.FromDays(3),
        TimeSpan.FromDays(7),
        TimeSpan.FromDays(16),
        TimeSpan.FromDays(35),
    };

    internal static TimeSpan IntervalFor(int box) => Intervals[Math.Clamp(box, 0, MaxBox)];

    /// <summary>
    /// Le caractère mérite-t-il un suivi ? Oui quand son geste demande autre chose que la
    /// touche gravée du caractère : AltGr, une touche morte, Verr. Maj., ou une autre touche
    /// (les chiffres, en Maj sur AZERTY). « e » ou « E » ne s'apprennent pas ici.
    /// </summary>
    internal static bool IsTracked(string character, IReadOnlyList<GestureStep> steps)
    {
        if (steps.Count == 0) return false;
        if (steps.Count == 1 && steps[0].KeyCode == "Space") return false;
        return !Gesture.StrikesItsOwnKey(character, steps);
    }

    internal static bool IsTracked(char character) =>
        IsTracked(character.ToString(), Gesture.Of(character.ToString()));

    /// <summary>
    /// Applique une séance terminée : <paramref name="attempts"/> demandes du caractère,
    /// dont <paramref name="errors"/> fautes. Une réussite avant l'échéance compte sans
    /// faire monter la boîte : réviser trop tôt n'ancre pas mieux.
    /// </summary>
    internal static void Apply(GestureMasteryEntry entry, int attempts, int errors, DateTimeOffset now)
    {
        if (attempts <= 0) return;
        bool wasDue = entry.IsDue(now);
        entry.Attempts += attempts;
        entry.Errors += errors;
        entry.LastReviewUtc = now;

        if (errors > 0)
        {
            entry.Box = 1;
            entry.DueUtc = now;
            return;
        }

        if (wasDue)
            entry.Box = Math.Min(entry.Box + 1, MaxBox);
        entry.DueUtc = now + IntervalFor(entry.Box);
    }

    /// <summary>
    /// Les caractères à revoir maintenant : échus, la boîte la plus basse d'abord, puis le
    /// plus de fautes rapportées aux demandes, puis l'échéance la plus ancienne.
    /// </summary>
    internal static IReadOnlyList<GestureMasteryEntry> Due(
        IEnumerable<GestureMasteryEntry> entries, DateTimeOffset now, int maxCount)
    {
        return entries
            .Where(entry => entry.IsDue(now))
            .OrderBy(entry => entry.Box)
            .ThenByDescending(entry => entry.Attempts == 0 ? 0 : entry.Errors / (double)entry.Attempts)
            .ThenBy(entry => entry.DueUtc ?? DateTimeOffset.MinValue)
            .ThenBy(entry => entry.Character, StringComparer.Ordinal)
            .Take(maxCount)
            .ToArray();
    }
}
