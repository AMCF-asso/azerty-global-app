using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Signal de franchissement du seuil de caractères enrichis (v1.3.0, décision d'Antoine
/// du 2026-09-21) : la sollicitation d'avis part quand l'utilisateur a tapé des caractères
/// qu'AZERTY Global apporte et que l'AZERTY traditionnel de Windows ne donne pas.
///
/// Ce que ces tests éprouvent est la décision, pas l'affichage :
/// <c>TrayApplication.MaybeShowReviewPrompt</c> est une méthode privée d'instance liée à un
/// HWND, hors de portée d'une suite sans fenêtre. Le signal, lui, est pur et mesurable —
/// et c'est lui qui porte tout le changement de comportement.
///
/// ⛔ Chaque assertion négative est doublée de sa réciproque. Un test qui vérifie seulement
/// « rien ne se déclenche à 19 caractères » reste vert si le compteur cesse de compter.
/// </summary>
public class EnrichedThresholdTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _statsPath;

    public EnrichedThresholdTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "AZGSeuil_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _statsPath = Path.Combine(_tempDir, "usage-stats.json");
        UsageStats.OverrideStatsPathForTests(_statsPath);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    /// <summary>Répète un caractère enrichi n fois, une émission par caractère — c'est ce
    /// que fait le hook clavier, une frappe à la fois.</summary>
    private static void Taper(string caractere, int fois)
    {
        for (int i = 0; i < fois; i++)
            UsageStats.RecordEmittedText(caractere);
    }

    // ── Le seuil lui-même ───────────────────────────────────────────

    [Fact]
    public void Seuil_UnCaractereEnDessous_NArmePasLeSignal()
    {
        Taper("É", UsageStats.EnrichedCharsReviewThreshold - 1);

        Assert.Equal(UsageStats.EnrichedCharsReviewThreshold - 1, UsageStats.TotalSpecialCharsCount);
        Assert.False(UsageStats.EnrichedThresholdCrossed);
    }

    [Fact]
    public void Seuil_Atteint_ArmeLeSignal()
    {
        Taper("É", UsageStats.EnrichedCharsReviewThreshold);

        Assert.Equal(UsageStats.EnrichedCharsReviewThreshold, UsageStats.TotalSpecialCharsCount);
        Assert.True(UsageStats.EnrichedThresholdCrossed);
    }

    [Fact]
    public void Seuil_FranchiEnUneSeuleEmission_ArmeLeSignal()
    {
        // Une insertion de texte peut livrer plusieurs caractères d'un coup (touche morte,
        // collage remappé) : la transition doit se voir même sans passage par chaque valeur.
        UsageStats.RecordEmittedText(new string('É', UsageStats.EnrichedCharsReviewThreshold + 5));

        Assert.True(UsageStats.EnrichedThresholdCrossed);
    }

    // ── Ce que l'AZERTY traditionnel donne déjà ne compte pas ────────

    [Fact]
    public void Seuil_CaracteresDejaAccessiblesEnAzertyTraditionnel_NArmentJamais()
    {
        // Minuscules accentuées françaises (touches directes ou touches mortes natives) et
        // symboles gravés sur un clavier français : l'AZERTY de Windows les donne déjà,
        // donc les taper ne prouve rien sur l'apport d'AZERTY Global. C'est exactement la
        // distinction que la décision du 2026-09-21 demande.
        Taper("é", 40);
        Taper("è", 40);
        Taper("€", 40);
        Taper("§", 40);

        Assert.Equal(0L, UsageStats.TotalSpecialCharsCount);
        Assert.False(UsageStats.EnrichedThresholdCrossed);
    }

    [Fact]
    public void Seuil_MajusculesAccentuees_ComptentEtArment()
    {
        // Réciproque du test précédent : la majuscule accentuée, elle, n'a aucune séquence
        // en un appui sur l'AZERTY traditionnel. Sans ce test, un compteur devenu inerte
        // rendrait le test ci-dessus vert pour la mauvaise raison.
        Taper("Ç", UsageStats.EnrichedCharsReviewThreshold);

        Assert.Equal(UsageStats.EnrichedCharsReviewThreshold, UsageStats.AccentedUppercaseCount);
        Assert.True(UsageStats.EnrichedThresholdCrossed);
    }

    [Fact]
    public void Seuil_CategoriesMelangees_SAdditionnent()
    {
        // Le seuil porte sur le total des quatre catégories, pas sur l'une d'elles.
        Taper("É", 7);   // majuscules accentuées
        Taper("«", 7);   // typographie française
        Taper("ŋ", 6);   // caractères internationaux

        Assert.Equal(20L, UsageStats.TotalSpecialCharsCount);
        Assert.True(UsageStats.EnrichedThresholdCrossed);
    }

    // ── Transition, et non état ─────────────────────────────────────

    [Fact]
    public void Seuil_DejaDepasseAuChargement_NArmePasLeSignal()
    {
        // Une installation ancienne démarre très au-dessus du seuil. Aucune transition
        // n'a lieu dans cette session de processus : la frappe seule n'arme rien. Depuis
        // l'audit 24/09, c'est TrayApplication qui arme le signal au chargement, sous ses
        // propres conditions (ReviewPromptGate.ShouldArmSignalAtLoad, témoins dans
        // ReviewPromptFirstAttemptTests) ; le chemin de démarrage ne lance plus l'essai 1.
        File.WriteAllText(_statsPath,
            """
            {
              "firstRemapDate": "2026-01-01",
              "lastActiveDate": "2026-01-01",
              "activeDaysCount": 40,
              "accentedUppercaseCount": 500,
              "frenchTypographyCount": 0,
              "internationalCount": 0,
              "symbolsCount": 0
            }
            """);
        UsageStats.OverrideStatsPathForTests(_statsPath); // force le rechargement

        Taper("É", 5);

        Assert.True(UsageStats.TotalSpecialCharsCount >= UsageStats.EnrichedCharsReviewThreshold);
        Assert.False(UsageStats.EnrichedThresholdCrossed);
    }

    [Fact]
    public void Signal_UneFoisDesarme_NeSeRearmePas()
    {
        Taper("É", UsageStats.EnrichedCharsReviewThreshold);
        Assert.True(UsageStats.EnrichedThresholdCrossed);

        UsageStats.ClearEnrichedThresholdSignal();
        Assert.False(UsageStats.EnrichedThresholdCrossed);

        // Le franchissement ne vaut qu'une fois : continuer à taper ne relance rien.
        Taper("É", 50);
        Assert.False(UsageStats.EnrichedThresholdCrossed);
    }

    // ── Silence de frappe ───────────────────────────────────────────

    [Fact]
    public void Silence_AvantTouteFrappe_EstInfini()
    {
        Assert.Equal(long.MaxValue, UsageStats.MillisecondsSinceLastRemap);
    }

    [Fact]
    public void Silence_ApresUneFrappe_EstCourt()
    {
        UsageStats.RecordEmittedText("É");

        long silence = UsageStats.MillisecondsSinceLastRemap;
        Assert.True(silence >= 0, $"silence négatif : {silence}");
        Assert.True(silence < 5_000, $"silence inattendu de {silence} ms juste après une frappe");
    }

    [Fact]
    public void Silence_UneFrappeOrdinaireLeReinitialiseAussi()
    {
        // Le timer attend une pause dans LA FRAPPE, pas une pause dans les caractères
        // enrichis : quelqu'un qui vient de taper « É » puis continue sa phrase en ASCII
        // est toujours en train d'écrire, et on ne l'interrompt pas.
        UsageStats.RecordEmittedText("É");
        UsageStats.RecordEmittedText(".");

        Assert.True(UsageStats.MillisecondsSinceLastRemap < 5_000);
    }
}
