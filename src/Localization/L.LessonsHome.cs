namespace AZERTYGlobal;

internal static partial class L
{
    // ── Accueil des Leçons (direction B, choisie le 11/10) ──────────────────────────
    // « Aujourd’hui » (révision du jour, reprise, votre texte), « Votre carte » et « Votre texte ».
    // Les espaces avant « : » et « ? » sont insécables (typographie française).

    // Onglets et liens
    public static string LessonsHome_Today => T("Aujourd’hui", "Today");
    public static string LessonsHome_YourMap => T("Votre carte", "Your map");
    public static string LessonsHome_AllLessons => T("Toutes les leçons", "All lessons");
    public static string LessonsHome_YourText => T("Votre texte", "Your text");
    /// <summary>Lien de retour depuis l’écran d’exercice.</summary>
    public static string LessonsHome_BackToToday => T("‹ Aujourd’hui", "‹ Today");
    public static string LessonsHome_BackToTodayTooltip => T("Revenir à la page d’accueil des Leçons", "Back to the Lessons home page");

    // Révision du jour
    public static string LessonsHome_ReviewTitle => T("Révision du jour", "Today’s review");
    public static string LessonsHome_ReviewLessonTitle => T("Séance du jour", "Today’s session");
    public static string LessonsHome_ReviewCount(int count) => count == 1
        ? T("1 caractère à revoir", "1 character to review")
        : T($"{count} caractères à revoir", $"{count} characters to review");
    public static string LessonsHome_ReviewNone => T("Rien à revoir pour le moment", "Nothing to review right now");
    public static string LessonsHome_ReviewNoneHint =>
        T("Les gestes que vous tapez en leçon reviennent ici quand il est temps de les revoir.",
          "The gestures you type in lessons come back here when it is time to review them.");
    public static string LessonsHome_ReviewWords(string words) =>
        T($"Dans des mots des leçons : {words}.", $"In lesson words: {words}.");
    public static string LessonsHome_ReviewMissing(string characters) =>
        T($"{characters} viendront avec les nouveaux mots des leçons.",
          $"{characters} will come with the new lesson words.");
    public static string LessonsHome_ReviewNoWords =>
        T("Aucun mot des leçons ne contient encore ces caractères.",
          "No lesson word contains these characters yet.");
    public static string LessonsHome_ReviewDeferred =>
        T("Remise à plus tard : ces caractères reviendront à la prochaine ouverture des Leçons.",
          "Postponed: these characters will come back the next time you open Lessons.");
    public static string LessonsHome_ReviewInstruction =>
        T("Révision du jour : retapez ces mots.", "Today’s review: retype these words.");
    public static string LessonsHome_ReviewButton => T("Réviser", "Review");
    public static string LessonsHome_ReviewButtonName(int count) => count == 1
        ? T("Réviser, 1 caractère à revoir", "Review, 1 character to review")
        : T($"Réviser, {count} caractères à revoir", $"Review, {count} characters to review");
    public static string LessonsHome_ReviewLater => T("Plus tard", "Later");
    public static string LessonsHome_ReviewMore(int count) => $"+{count}";
    /// <summary>Un caractère échu avec son geste, pour le lecteur d’écran : « é, VM puis é ».</summary>
    public static string LessonsHome_DueSpoken(string character, string gesture) => $"{character}, {gesture}";

    // Reprendre
    public static string LessonsHome_ResumeTitle => T("Reprendre", "Resume");
    public static string LessonsHome_StartTitle => T("Commencer", "Start");
    public static string LessonsHome_ResumeButton => T("Reprendre", "Resume");
    public static string LessonsHome_StartButton => T("Commencer", "Start");
    public static string LessonsHome_ResumeName(string module, string lesson, int exercise, int total) =>
        T($"Reprendre : {module}, {lesson}, exercice {exercise} sur {total}",
          $"Resume: {module}, {lesson}, exercise {exercise} of {total}");

    public static string LessonsHome_StartName(string module, string lesson, int exercise, int total) =>
        T($"Commencer : {module}, {lesson}, exercice {exercise} sur {total}",
          $"Start: {module}, {lesson}, exercise {exercise} of {total}");

    // Votre texte (carte de l’accueil)
    public static string LessonsHome_TextSub => T("Une leçon faite de vos mots", "A lesson made of your words");
    public static string LessonsHome_TextBodyLead => T("Collez ", "Paste ");
    public static string LessonsHome_TextBodyStrong =>
        T("un nom, une adresse, une phrase dans votre langue", "a name, an address, a sentence in your language");
    public static string LessonsHome_TextBodyTail =>
        T(". L’app la découpe en gestes et vous la fait taper.", ". The app splits it into gestures and has you type it.");
    public static string LessonsHome_TextButton => T("Coller un texte", "Paste a text");

    // Carte de maîtrise
    public static string LessonsHome_MapTitle => T("Votre carte", "Your map");
    public static string LessonsHome_MapHint =>
        T("Chaque touche se remplit selon le niveau des caractères déjà tapés. Cliquez une touche pour la travailler.",
          "Each key fills up with the level of the characters already typed. Click a key to work on it.");
    public static string LessonsHome_MapClickHint => T("Cliquez une touche pour la travailler.", "Click a key to work on it.");
    /// <summary>Nom de la zone cliquable du clavier, pour le lecteur d’écran.</summary>
    public static string LessonsHome_MapKeysName => T("Clavier de la carte, choisir une touche", "Map keyboard, choose a key");
    public static string LessonsHome_MapKeysHelp =>
        T("Flèches gauche et droite : touche précédente ou suivante.", "Left and right arrows: previous or next key.");

    // Votre carte : détail d’une touche
    public static string LessonsHome_KeyTitle(string key) => T($"Touche {key}", $"Key {key}");
    public static string LessonsHome_KeySummary(int followed, int seen, int due)
    {
        if (L.IsEnglish)
        {
            string signs = followed == 1 ? "1 sign followed" : $"{followed} signs followed";
            return seen == 0 ? $"{signs}, none typed." : $"{signs}: {seen} typed" + (due > 0 ? $", {due} to review" : "") + ".";
        }
        string signes = followed == 1 ? "1 signe suivi" : $"{followed} signes suivis";
        return seen == 0 ? $"{signes}, aucun tapé."
            : $"{signes} : {seen} tapé{(seen > 1 ? "s" : "")}" + (due > 0 ? $", {due} à revoir" : "") + ".";
    }
    public static string LessonsHome_KeySessionButton => T("Séance sur cette touche", "Practise this key");
    public static string LessonsHome_KeySessionName(string key) => T($"Séance sur la touche {key}", $"Practise the {key} key");
    public static string LessonsHome_KeySessionInstruction =>
        T("Séance de la touche : tapez ces mots.", "Key session: type these words.");
    /// <summary>Ce que la séance d’une touche fera taper : les signes déjà vus, puis le nouveau.</summary>
    public static string LessonsHome_KeySessionNote(IReadOnlyList<string> seen, string? fresh)
    {
        string list = HomeJoin(seen);
        if (seen.Count > 0 && fresh != null)
            return T($"{list} d’abord, puis un signe nouveau : {fresh}.", $"{list} first, then a new sign: {fresh}.");
        if (seen.Count > 0)
            return T($"Les signes déjà vus : {list}.", $"Signs already seen: {list}.");
        return fresh != null ? T($"Un signe nouveau : {fresh}.", $"A new sign: {fresh}.") : "";
    }
    public static string LessonsHome_KeySessionNone =>
        T("Rien à travailler sur cette touche pour le moment.", "Nothing to practise on this key for now.");
    public static string LessonsHome_LayerName(HomeLayer layer) => layer switch
    {
        HomeLayer.Shift => Gesture_LineShift,
        HomeLayer.AltGr => "AltGr",
        HomeLayer.ShiftAltGr => Gesture_LineShift + " AltGr",
        _ => T("Touche seule", "Key alone"),
    };
    public static string LessonsHome_ThenLetter => T("puis la lettre", "then the letter");
    public static string LessonsHome_Then(string rest) => T($"puis {rest}", $"then {rest}");
    public static string LessonsHome_DueTag => T(" · à revoir", " · to review");
    public static string LessonsHome_LayerMore(int count) => count == 1
        ? T("Et 1 autre signe.", "And 1 more sign.")
        : T($"Et {count} autres signes.", $"And {count} more signs.");
    public static string LessonsHome_LayerNoneTyped => T("Rien de tapé sur cette couche.", "Nothing typed on this layer.");

    /// <summary>« a, b et c » ; « a, b and c ».</summary>
    private static string HomeJoin(IReadOnlyList<string> items) => items.Count switch
    {
        0 => "",
        1 => items[0],
        _ => string.Join(", ", items.Take(items.Count - 1)) + T(" et ", " and ") + items[^1],
    };
    /// <summary>Résumé de la carte pour le lecteur d’écran.</summary>
    public static string LessonsHome_MapSpoken(int seen, int followed, int due) =>
        T($"Votre carte : {seen} touche(s) travaillée(s) sur {followed}, {due} avec des caractères à revoir.",
          $"Your map: {seen} of {followed} keys worked on, {due} with characters to review.");
    public static string LessonsHome_LegendLevel => T("niveau moyen des caractères tapés (cinq crans)", "average level of typed characters (five notches)");
    public static string LessonsHome_LegendDue => T("au moins un caractère à revoir aujourd’hui", "at least one character to review today");
    public static string LessonsHome_LegendUnseen => T("caractères à geste, jamais tapés", "gesture characters, never typed");
    public static string LessonsHome_LegendNothing => T("rien à suivre : lettres, Espace", "nothing to follow: letters, Space");
    public static string LessonsHome_MapEmpty => T("La carte ne suit encore aucun caractère.", "The map does not follow any character yet.");

    /// <summary>Infobulle d’une touche de la carte, première ligne.</summary>
    public static string LessonsHome_KeyTooltipTitle(string key, int seen, int followed) =>
        T($"Touche {key} : {seen} tapé(s) sur {followed}", $"Key {key}: {seen} typed of {followed}");
    public static string LessonsHome_KeyTooltipLine(string character, string level, bool due) =>
        due ? T($"{character}  {level} · à revoir", $"{character}  {level} · to review") : $"{character}  {level}";
    public static string LessonsHome_KeyTooltipMore(int count) =>
        T($"… et {count} autre(s) tapé(s)", $"… and {count} more typed");
    public static string LessonsHome_LevelOf(int box) => T($"boîte {box} sur 5", $"box {box} of 5");
    public static string LessonsHome_LevelNever => T("jamais tapé", "never typed");

    // Lecteur d’écran : résumé de la page (surface de lecture)
    public static string LessonsHome_SurfaceHelp => T("Résumé de la page. Tab atteint les boutons.", "Page summary. Tab reaches the buttons.");
    public static string LessonsHome_SpokenResume(string module, string lesson, int exercise, int total) =>
        T($"{module}, {lesson}, exercice {exercise} sur {total}.", $"{module}, {lesson}, exercise {exercise} of {total}.");
}
