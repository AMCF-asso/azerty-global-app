using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Couche de politiques d'entreprise — lot C du plan v1.2.0, décisions d'Antoine du
/// 2026-08-19.
///
/// Chaque clé est éprouvée dans ses trois états : 0, 1, et absente. Le troisième est le
/// témoin réciproque — sans lui, un lecteur qui répondrait toujours « désactivé » passerait
/// au vert sur les deux premiers. Et le chemin de registre n'est pas comparé à une chaîne
/// que le test se serait fabriquée : le lecteur enregistre ce qu'on lui demande vraiment,
/// et c'est cela qui est comparé au chemin littéral attendu.
///
/// La lecture registre réelle est éprouvée à part, sur des valeurs de Windows lisibles sans
/// droits administrateur : elle prouve que le P/Invoke rend bien un REG_SZ, un REG_DWORD et
/// null sur valeur absente. Ce qu'aucun test ne peut atteindre, faute de pouvoir écrire sous
/// HKLM : une politique réellement posée par une DSI, qui attend le smoke test du lot G.
/// </summary>
public class PolicyTests : IDisposable
{
    /// <summary>Chemin littéral attendu, celui que documentera l'ADMX du lot F.</summary>
    private const string CheminAttendu = @"SOFTWARE\Policies\AZERTYGlobal";

    private readonly string _tempDir;

    public PolicyTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "AZGTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        ConfigManager.OverrideConfigPathForTests(Path.Combine(_tempDir, "config.json"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    /// <summary>Lecteur de test : rend les valeurs qu'on lui donne et retient ce qu'on lui a
    /// demandé, chemin compris.</summary>
    private sealed class LecteurEnregistreur
    {
        private readonly Dictionary<string, object?> _valeurs;

        internal LecteurEnregistreur(Dictionary<string, object?>? valeurs = null) =>
            _valeurs = valeurs ?? new Dictionary<string, object?>();

        internal List<(string Chemin, string Valeur)> Demandes { get; } = new();

        internal object? Lire(string chemin, string nomValeur)
        {
            Demandes.Add((chemin, nomValeur));
            return _valeurs.TryGetValue(nomValeur, out var valeur) ? valeur : null;
        }
    }

    private static PolicySet Lire(params (string Nom, object? Valeur)[] valeurs)
    {
        var lecteur = new LecteurEnregistreur(
            valeurs.ToDictionary(v => v.Nom, v => v.Valeur));
        return PolicyManager.Read(lecteur.Lire);
    }

    // ═══════════════════════════════════════════════════════════════
    // Emplacement
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void KeyPath_EstLaCleDeStrategieDuProduit()
    {
        Assert.Equal(CheminAttendu, PolicyManager.KeyPath);
    }

    /// <summary>Le chemin n'est pas seulement construit : c'est bien celui qui est demandé au
    /// registre, pour les cinq valeurs et sous leurs noms exacts.</summary>
    [Fact]
    public void Read_DemandeLesCinqValeursSousLeCheminDeStrategie()
    {
        var lecteur = new LecteurEnregistreur();

        PolicyManager.Read(lecteur.Lire);

        Assert.All(lecteur.Demandes, d => Assert.Equal(CheminAttendu, d.Chemin));
        Assert.Equal(
            new[]
            {
                "NotificationsEnabled", "UsageStatsEnabled", "ExternalLinksEnabled",
                "ShowOnboarding", "Language",
            },
            lecteur.Demandes.Select(d => d.Valeur).ToArray());
    }

    // ═══════════════════════════════════════════════════════════════
    // Les quatre REG_DWORD — trois états chacun
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("NotificationsEnabled")]
    [InlineData("UsageStatsEnabled")]
    [InlineData("ExternalLinksEnabled")]
    [InlineData("ShowOnboarding")]
    public void Read_ValeurZero_RendFalse(string nom)
    {
        Assert.False(Valeur(Lire((nom, 0)), nom));
    }

    [Theory]
    [InlineData("NotificationsEnabled")]
    [InlineData("UsageStatsEnabled")]
    [InlineData("ExternalLinksEnabled")]
    [InlineData("ShowOnboarding")]
    public void Read_ValeurUn_RendTrue(string nom)
    {
        Assert.True(Valeur(Lire((nom, 1)), nom));
    }

    /// <summary>Témoin réciproque : sans clé, la politique n'existe pas — ce qui n'est pas la
    /// même chose que 0, et c'est cette différence qui fait vivre le réglage utilisateur.</summary>
    [Theory]
    [InlineData("NotificationsEnabled")]
    [InlineData("UsageStatsEnabled")]
    [InlineData("ExternalLinksEnabled")]
    [InlineData("ShowOnboarding")]
    public void Read_CleAbsente_RendNull(string nom)
    {
        Assert.Null(Valeur(Lire(), nom));
    }

    /// <summary>Une valeur non nulle autre que 1 vaut « activé », comme partout dans le
    /// registre Windows.</summary>
    [Fact]
    public void Read_DwordNonNul_RendTrue()
    {
        Assert.True(Lire(("NotificationsEnabled", 2)).Notifications);
    }

    /// <summary>Type inattendu : la valeur est ignorée plutôt qu'interprétée de travers.</summary>
    [Fact]
    public void Read_DwordAvecTypeInattendu_RendNull()
    {
        Assert.Null(Lire(("NotificationsEnabled", "1")).Notifications);
    }

    private static bool? Valeur(PolicySet politiques, string nom) => nom switch
    {
        "NotificationsEnabled" => politiques.Notifications,
        "UsageStatsEnabled" => politiques.UsageStats,
        "ExternalLinksEnabled" => politiques.ExternalLinks,
        "ShowOnboarding" => politiques.ShowOnboarding,
        _ => throw new ArgumentOutOfRangeException(nameof(nom), nom, null),
    };

    // ═══════════════════════════════════════════════════════════════
    // Language — REG_SZ
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("fr", "fr")]
    [InlineData("en", "en")]
    [InlineData("EN", "en")]
    [InlineData(" fr ", "fr")]
    public void Read_LangueValide_EstRetenue(string brut, string attendu)
    {
        Assert.Equal(attendu, Lire(("Language", brut)).Language);
    }

    [Theory]
    [InlineData("de")]
    [InlineData("")]
    [InlineData("fr-FR")]
    public void Read_LangueInvalide_EstIgnoree(string brut)
    {
        Assert.Null(Lire(("Language", brut)).Language);
    }

    [Fact]
    public void Read_LangueAbsente_RendNull()
    {
        Assert.Null(Lire().Language);
    }

    /// <summary>Un REG_DWORD posé sous ce nom n'est pas une langue.</summary>
    [Fact]
    public void Read_LangueAvecTypeInattendu_RendNull()
    {
        Assert.Null(Lire(("Language", 1)).Language);
    }

    // ═══════════════════════════════════════════════════════════════
    // Précédence — politique > réglage utilisateur > défaut du canal
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Notifications_SansPolitique_SuitLeReglageUtilisateur()
    {
        Assert.True(PolicyManager.NotificationsEnabled(null, userSetting: true));
        Assert.False(PolicyManager.NotificationsEnabled(null, userSetting: false));
    }

    [Fact]
    public void Notifications_SousPolitique_IgnoreLeReglageUtilisateur()
    {
        Assert.False(PolicyManager.NotificationsEnabled(false, userSetting: true));
        Assert.True(PolicyManager.NotificationsEnabled(true, userSetting: false));
    }

    /// <summary>Défaut de canal : la collecte est éteinte sur le canal sobre et allumée
    /// ailleurs, y compris hors package — la décision D8 laisse ce canal inchangé.</summary>
    [Fact]
    public void Statistiques_SansPolitique_SuiventLeDefautDuCanal()
    {
        Assert.False(PolicyManager.UsageStatsEnabled(null, DistributionChannel.Amcf));
        Assert.True(PolicyManager.UsageStatsEnabled(null, DistributionChannel.Store));
        Assert.True(PolicyManager.UsageStatsEnabled(null, DistributionChannel.Unpackaged));
    }

    [Fact]
    public void Statistiques_SousPolitique_PrimentSurLeCanal()
    {
        Assert.True(PolicyManager.UsageStatsEnabled(true, DistributionChannel.Amcf));
        Assert.False(PolicyManager.UsageStatsEnabled(false, DistributionChannel.Store));
    }

    [Fact]
    public void LiensExternes_SansPolitique_SuiventLeDefautDuCanal()
    {
        Assert.False(PolicyManager.ExternalLinksEnabled(null, DistributionChannel.Amcf));
        Assert.True(PolicyManager.ExternalLinksEnabled(null, DistributionChannel.Store));
        Assert.True(PolicyManager.ExternalLinksEnabled(null, DistributionChannel.Unpackaged));
    }

    /// <summary>Ce que la politique apporte de neuf : une structure peut éteindre les liens
    /// externes sur le canal Store, que le lot B laissait bavard.</summary>
    [Fact]
    public void LiensExternes_EteintsParPolitique_MemeSurStore()
    {
        Assert.False(PolicyManager.ExternalLinksEnabled(false, DistributionChannel.Store));
        Assert.True(PolicyManager.ExternalLinksEnabled(true, DistributionChannel.Amcf));
    }

    [Fact]
    public void FenetreDeBienvenue_SansPolitique_SuitLaCaseDeLUtilisateur()
    {
        Assert.True(PolicyManager.ShowOnboarding(null, userSetting: true));
        Assert.False(PolicyManager.ShowOnboarding(null, userSetting: false));
    }

    /// <summary>Le seul écart assumé à la précédence uniforme : 1 autorise sans imposer, donc
    /// un utilisateur qui a décoché la case reste décoché. Ce test est le témoin de la
    /// décision — il rougit si quelqu'un « uniformise » la règle.</summary>
    [Fact]
    public void FenetreDeBienvenue_PolitiqueAUn_NImposeRien()
    {
        Assert.False(PolicyManager.ShowOnboarding(true, userSetting: false));
        Assert.True(PolicyManager.ShowOnboarding(true, userSetting: true));
    }

    [Fact]
    public void FenetreDeBienvenue_PolitiqueAZero_Supprime()
    {
        Assert.False(PolicyManager.ShowOnboarding(false, userSetting: true));
    }

    [Fact]
    public void Langue_SousPolitique_PrimeSurLeChoixUtilisateur()
    {
        Assert.Equal("en", PolicyManager.AppLanguage("en", "fr"));
        Assert.Equal("fr", PolicyManager.AppLanguage(null, "fr"));
        Assert.Equal("en", PolicyManager.AppLanguage(null, "en"));
    }

    // ═══════════════════════════════════════════════════════════════
    // Ce que l'interface grise
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void EstGere_DesQuUneValeurEstPosee()
    {
        Assert.True(PolicyManager.IsManaged(false));
        Assert.True(PolicyManager.IsManaged(true));
        Assert.False(PolicyManager.IsManaged(null));
    }

    /// <summary>La case de la fenêtre de bienvenue ne se grise que si la politique la
    /// contraint : la griser à 1 mentirait sur ce que l'utilisateur peut encore faire.</summary>
    [Fact]
    public void FenetreDeBienvenue_NEstGriseeQueParUnZero()
    {
        Assert.True(PolicyManager.IsOnboardingManaged(false));
        Assert.False(PolicyManager.IsOnboardingManaged(true));
        Assert.False(PolicyManager.IsOnboardingManaged(null));
    }

    [Fact]
    public void Langue_EstGereeDesQuUneLangueValideEstImposee()
    {
        Assert.True(PolicyManager.IsLanguageManaged("fr"));
        Assert.False(PolicyManager.IsLanguageManaged(null));
    }

    // ═══════════════════════════════════════════════════════════════
    // Effet sur le reste de l'application
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Statistiques_PolitiqueUn_RallumeLaCollecteSurLeCanalSobre()
    {
        var lecteur = new LecteurEnregistreur(new Dictionary<string, object?>
        {
            ["UsageStatsEnabled"] = 1,
        });

        using (PolicyManager.OverrideForTests(lecteur.Lire))
        using (AppChannel.OverrideForTests(DistributionChannel.Amcf))
        {
            Assert.True(UsageStats.CollectionEnabled);
        }
    }

    [Fact]
    public void Statistiques_PolitiqueZero_EteintLaCollecteSurLeCanalStore()
    {
        var lecteur = new LecteurEnregistreur(new Dictionary<string, object?>
        {
            ["UsageStatsEnabled"] = 0,
        });

        using (PolicyManager.OverrideForTests(lecteur.Lire))
        using (AppChannel.OverrideForTests(DistributionChannel.Store))
        {
            Assert.False(UsageStats.CollectionEnabled);
        }
    }

    /// <summary>Les deux listes du menu obéissent à la politique, pas seulement au canal. Le
    /// sens réciproque compte autant : sur le canal sobre, une politique à 1 rend les
    /// entrées.</summary>
    [Fact]
    public void MenuRetours_LiensExternesEteintsParPolitique_PerdSoutienEtDiscord()
    {
        Assert.Equal(
            new[] { TrayApplication.IDM_FEEDBACK, TrayApplication.IDM_BUG },
            TrayApplication.FeedbackMenuEntries(DistributionChannel.Store, externalLinksPolicy: false));
        Assert.Empty(
            TrayApplication.FeedbackTopLevelEntries(DistributionChannel.Store, externalLinksPolicy: false));
    }

    [Fact]
    public void MenuRetours_LiensExternesRallumesParPolitique_LesRendSurLeCanalSobre()
    {
        Assert.Equal(
            new[]
            {
                TrayApplication.IDM_SUPPORT, TrayApplication.IDM_FEEDBACK,
                TrayApplication.IDM_DISCORD, TrayApplication.IDM_BUG,
            },
            TrayApplication.FeedbackMenuEntries(DistributionChannel.Amcf, externalLinksPolicy: true));
        Assert.Equal(
            new[] { TrayApplication.IDM_RATE_STORE },
            TrayApplication.FeedbackTopLevelEntries(DistributionChannel.Amcf, externalLinksPolicy: true));
    }

    /// <summary>Second chemin de sollicitation, celui qu'on oublie : le partage tombe lui
    /// aussi avec les liens externes, et il a sa propre garde.</summary>
    [Fact]
    public void Partage_LiensExternesEteints_NeSollicitePas()
    {
        var signaux = new ReviewSharePromptSignals(
            Channel: DistributionChannel.Store,
            ExternalLinks: false,
            PromptClicked: false,
            PromptCount: 0,
            PromptLastShown: null,
            LastErrorUtc: null,
            UtcNow: new DateTime(2026, 8, 19, 12, 0, 0, DateTimeKind.Utc),
            Today: new DateOnly(2026, 8, 19),
            FirstRemapDate: new DateOnly(2026, 8, 10),
            ActiveDaysCount: ReviewSharePrompt.MinActiveDays);

        Assert.False(ReviewSharePrompt.ShouldPrompt(signaux));
        Assert.True(ReviewSharePrompt.ShouldPrompt(signaux with { ExternalLinks = true }));
    }

    /// <summary>
    /// Trois surfaces changent la langue — Paramètres, menu tray, drapeau de la fenêtre de
    /// bienvenue — et le garde est posé sur la porte unique qu'elles empruntent toutes.
    ///
    /// La politique impose « fr » et l'appel demande « en », jamais l'inverse : « fr » étant
    /// déjà le défaut du réglage utilisateur, une écriture de « fr » aurait été invisible et
    /// ce test serait resté vert sans le garde. Mesuré — première version de ce test, témoin
    /// de mutation à zéro rouge.
    /// </summary>
    [Fact]
    public void SetAppLanguage_SousPolitique_NEcritRien()
    {
        var lecteur = new LecteurEnregistreur(new Dictionary<string, object?>
        {
            ["Language"] = "fr",
        });

        using (PolicyManager.OverrideForTests(lecteur.Lire))
        {
            ConfigManager.SetAppLanguage("en");

            Assert.Equal("fr", ConfigManager.AppLanguage);
            Assert.Equal("fr", ConfigManager.AppLanguageUserSetting);
        }
    }

    /// <summary>Réciproque : sans politique, exactement le même appel écrit. Sans elle, le
    /// test précédent serait vert pour la mauvaise raison — un appel qui n'écrit jamais.</summary>
    [Fact]
    public void SetAppLanguage_SansPolitique_EcritLeChoixDeLUtilisateur()
    {
        using (PolicyManager.OverrideForTests(new LecteurEnregistreur().Lire))
        {
            ConfigManager.SetAppLanguage("en");

            Assert.Equal("en", ConfigManager.AppLanguage);
            Assert.Equal("en", ConfigManager.AppLanguageUserSetting);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Cache et portée du hook de test
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Le scope restaure lecteur et cache : un état statique laissé derrière soi rend
    /// vert en isolation et rouge dans la suite entière, incident mesuré le 2026-08-18.</summary>
    [Fact]
    public void OverrideForTests_RestaureLEtatPrecedent()
    {
        var avant = PolicyManager.Current;

        using (PolicyManager.OverrideForTests(
            new LecteurEnregistreur(new Dictionary<string, object?> { ["ShowOnboarding"] = 0 }).Lire))
        {
            Assert.False(PolicyManager.Current.ShowOnboarding);
        }

        Assert.Same(avant, PolicyManager.Current);
    }

    /// <summary>Une politique est lue une seule fois : le second accès ne redemande rien au
    /// registre. C'est ce qui rend un redémarrage nécessaire après un changement de
    /// stratégie, et l'ADMX du lot F le documentera.</summary>
    [Fact]
    public void Current_NeLitLeRegistreQuUneFois()
    {
        var lecteur = new LecteurEnregistreur();

        using (PolicyManager.OverrideForTests(lecteur.Lire))
        {
            _ = PolicyManager.Current;
            _ = PolicyManager.Current;
        }

        Assert.Equal(5, lecteur.Demandes.Count);
    }

    // ═══════════════════════════════════════════════════════════════
    // Lecture registre réelle
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Le lecteur de production, éprouvé sur des valeurs que Windows publie sous HKLM et que
    /// n'importe quel compte peut lire. Sans ce test, tout ce qui précède ne prouverait que
    /// l'interprétation : le P/Invoke lui-même, ses deux types et son cas « valeur absente »
    /// resteraient sans témoin.
    /// </summary>
    [Fact]
    public void ReadRegistryValue_LitUnREG_SZ_UnREG_DWORD_EtRendNullSurValeurAbsente()
    {
        const string cleWindows = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";

        var chaine = PolicyManager.ReadRegistryValue(cleWindows, "CurrentVersion");
        var entier = PolicyManager.ReadRegistryValue(cleWindows, "CurrentMajorVersionNumber");
        var absente = PolicyManager.ReadRegistryValue(cleWindows, "AZERTYGlobalValeurQuiNExistePas");

        Assert.IsType<string>(chaine);
        Assert.NotEmpty((string)chaine!);
        Assert.IsType<int>(entier);
        Assert.True((int)entier! >= 10);
        Assert.Null(absente);
    }

    /// <summary>Clé absente : aucune des cinq valeurs ne remonte, et rien ne lève.</summary>
    [Fact]
    public void ReadRegistryValue_CleInexistante_RendNull()
    {
        Assert.Null(PolicyManager.ReadRegistryValue(
            @"SOFTWARE\Policies\AZERTYGlobalCleQuiNExistePas", "NotificationsEnabled"));
    }
}
