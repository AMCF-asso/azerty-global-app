namespace AZERTYGlobal;

internal static partial class L
{
    // ── Module synthétique "Initiation" (LessonCatalog.BuildInitiationModule) ──
    // Contenu à taper (Content) volontairement laissé en français quelle que soit la
    // langue de l'UI — c'est l'objet même de l'apprentissage (cf. plan i18n § périmètre).
    // Un seul nom, « Prise en main » / “Getting started”, pour la fenêtre, le module et sa
    // leçon (QCM du 10/11, RA13). Ces titres n'entrent pas dans l'empreinte des exercices.
    public static string Lessons_InitiationModuleTitle => T("Prise en main", "Getting started");
    public static string Lessons_InitiationModuleDesc => T(
        "Rejouer la prise en main proposée à l’accueil.",
        "Replay Getting started from the welcome screen.");
    public static string Lessons_InitiationLessonTitle => T("Prise en main", "Getting started");
    public static string Lessons_InitiationLessonDesc => T(
        $"Les 8 exercices courts de la prise en main d’{Product}.",
        $"The 8 short Getting started exercises for {Product}.");

    public static string Lessons_Init0 => T("Tapez É pour découvrir les majuscules accentuées.", "Type É to discover accented capitals.");
    public static string Lessons_Init1 => T(
        "Tapez cette phrase en utilisant Verr. Maj. pour les capitales accentuées.",
        "Type this sentence using Caps Lock for accented capitals.");
    public static string Lessons_Init2 => T("Tapez cette adresse e-mail.", "Type this email address.");
    public static string Lessons_Init3 => T("Tapez cette phrase de typographie française.", "Type this French typography sentence.");
    public static string Lessons_Init4 => T("Tapez cette ligne de code.", "Type this line of code.");
    public static string Lessons_Init5 => T("Tapez ces mots étrangers.", "Type these foreign words.");
    public static string Lessons_Init6 => T("Tapez ce message.", "Type this message.");
    public static string Lessons_Init7 => T("Tapez ces lettres des langues d’Afrique.", "Type these letters of African languages.");
}
