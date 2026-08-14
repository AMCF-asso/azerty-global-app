namespace AZERTYGlobal;

internal static partial class L
{
    // ── Module synthétique "Initiation" (LessonCatalog.BuildInitiationModule) ──
    // Contenu à taper (Content) volontairement laissé en français quelle que soit la
    // langue de l'UI — c'est l'objet même de l'apprentissage (cf. plan i18n § périmètre).
    public static string Lessons_InitiationModuleTitle => T("Initiation", "Getting started");
    public static string Lessons_InitiationModuleDesc => T(
        "Rejouer le parcours de prise en main intégré à l’accueil.",
        "Replay the guided walkthrough from the welcome screen.");
    public static string Lessons_InitiationLessonTitle => T("Premiers pas", "First steps");
    public static string Lessons_InitiationLessonDesc => T(
        "Les 6 exercices courts de l’initiation AZERTY Global.",
        "The 6 short exercises from the AZERTY Global walkthrough.");

    public static string Lessons_Init0 => T("Tapez É pour découvrir les majuscules accentuées.", "Type É to discover accented capitals.");
    public static string Lessons_Init1 => T(
        "Tapez cette phrase en utilisant Verr. Maj. pour les capitales accentuées.",
        "Type this sentence using Caps Lock for accented capitals.");
    public static string Lessons_Init2 => T("Tapez cette adresse e-mail.", "Type this email address.");
    public static string Lessons_Init3 => T("Tapez cette phrase de typographie française.", "Type this French typography sentence.");
    public static string Lessons_Init4 => T("Tapez cette ligne de code.", "Type this line of code.");
    public static string Lessons_Init5 => T("Tapez ces mots étrangers.", "Type these foreign words.");
}
