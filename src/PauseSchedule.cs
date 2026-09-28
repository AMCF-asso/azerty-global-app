namespace AZERTYGlobal;

/// <summary>
/// Heures de la Pause, communes au menu de l'icône et à la fenêtre « Personnaliser » :
/// l'échéance de « Jusqu'à demain » et l'écriture d'une heure de reprise. Décisions
/// d'Antoine du 2026-09-28 : « demain » veut dire 8 h, et l'heure de reprise se lit dans
/// le menu comme dans la fenêtre.
/// </summary>
static class PauseSchedule
{
    internal const int MorningHour = 8;

    /// <summary>Le prochain 8 h : ce matin si l'on est avant 8 h, sinon demain.</summary>
    internal static DateTime NextMorning(DateTime nowLocal)
    {
        DateTime today = nowLocal.Date.AddHours(MorningHour);
        return nowLocal < today ? today : today.AddDays(1);
    }

    /// <summary>« 16 h 12 », « 8 h » en français (typographie de l'Imprimerie nationale) ;
    /// « 4:12 PM » en anglais.</summary>
    internal static string FormatClock(DateTime local)
    {
        if (L.IsEnglish)
            return local.ToString("h:mm tt", System.Globalization.CultureInfo.GetCultureInfo("en-US"));
        return local.Minute == 0 ? $"{local.Hour} h" : $"{local.Hour} h {local.Minute:00}";
    }

    /// <summary>« à 16 h 12 » le jour même, « demain à 8 h » le lendemain.</summary>
    internal static string DescribeResume(DateTime nowLocal, DateTime endLocal)
    {
        string clock = FormatClock(endLocal);
        return endLocal.Date > nowLocal.Date ? L.Pause_TomorrowAt(clock) : L.Pause_TodayAt(clock);
    }
}
