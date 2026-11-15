using System.Globalization;

namespace AZERTYGlobal;

/// <summary>
/// Dates « au jour près » persistées en texte (série d'usage, séances et rappels du Défi) :
/// toujours <c>yyyy-MM-dd</c>, grégorien, culture invariante, à l'écriture comme à la lecture.
/// Relue avec la culture de Windows, une date écrite « 2026-11-15 » devient, sous un calendrier
/// thaï bouddhiste, l'an 1483 grégorien : ce n'est plus celle du jour, la série retombe et le Défi
/// se recompte (ME-01). Un seul analyseur, pour que les lecteurs ne divergent jamais entre eux.
/// </summary>
internal static class StoredDate
{
    internal const string Format = "yyyy-MM-dd";

    /// <summary>La date écrite, ou null si le texte est absent ou n'est pas au format.</summary>
    internal static DateOnly? Parse(string? text) =>
        text != null &&
        DateOnly.TryParseExact(text, Format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;
}
