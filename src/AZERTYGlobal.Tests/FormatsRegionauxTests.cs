using System.Globalization;
using System.Text;
using System.Text.Json;
using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Audit des formats régionaux (docs/audit-formats-regionaux.md). Chaque test fixe la culture
/// du fil (<see cref="CultureInfo.CurrentCulture"/>, sans les personnalisations de l'utilisateur)
/// et la restaure à la fin : les réglages régionaux de la machine ne changent pas. Les cultures
/// viennent de la sonde de l'audit (séparateurs, signe moins, calendrier, casse turque).
/// Tests rouges voulus jusqu'aux correctifs : FR-02 et FR-03 ; les autres prouvent une réfutation.
/// </summary>
public class FormatsRegionauxTests
{
    /// <summary>Les 22 réglages régionaux essayés (liste et raisons : § 2 du document).</summary>
    public static readonly TheoryData<string> Cultures = new()
    {
        "fr-FR", "en-US", "fr-CA", "fr-CH", "de-CH", "fr-SN", "fr-CI", "fr-CM", "fr-MA", "ar-MA", "ar-DZ",
        "tr-TR", "az-Latn-AZ", "th-TH", "ar-SA", "fa-IR", "ps-AF", "he-IL", "sv-SE", "nb-NO", "fi-FI", "hi-IN",
    };

    private static readonly DateOnly Jour = new(2026, 11, 17);

    private sealed class CultureDuFil : IDisposable
    {
        private readonly CultureInfo _avant = CultureInfo.CurrentCulture;
        public CultureDuFil(string nom) => CultureInfo.CurrentCulture = new CultureInfo(nom, useUserOverride: false);
        public void Dispose() => CultureInfo.CurrentCulture = _avant;
    }

    private static LessonExercise ExtraitDuDéfi(DateOnly jour) =>
        DailyChallenge.BuildModule(jour, DailyChallenge.SequenceLength)!.Lessons[0].Exercises[DailyChallenge.ExtractExerciseIndex];

    // ── FR-02 : l'identifiant du Défi suit le calendrier de Windows ──────────────────────────

    /// <summary>DailyChallenge.cs:220 écrit <c>defi-{date:yyyyMMdd}</c> avec la culture du fil :
    /// thaï 2569, hégirien 1448, persan 1405. L'identifiant entre dans les clés de progression.</summary>
    [Theory]
    [MemberData(nameof(Cultures))]
    public void FR02_IdentifiantDuDefi_EstLaDateGregorienne(string culture)
    {
        using var isolation = new BancCapture.Isolation();
        using var fil = new CultureDuFil(culture);

        var leçon = DailyChallenge.BuildModule(Jour, 0)!.Lessons[0];

        Assert.Equal("defi-20261117", leçon.Id);
    }

    /// <summary>Effet persistant : un Défi réussi sous fr-FR n'est plus vu comme fait après un
    /// passage du format régional en thaï le même jour (la clé « defi/defi-2569… » est neuve).</summary>
    [Fact]
    public void FR02_DefiReussi_ResteFait_QuandLeFormatRegionalChangeLeMemeJour()
    {
        using var isolation = new BancCapture.Isolation();
        string dossier = Path.Combine(Path.GetTempPath(), "ag-formats-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dossier);
        try
        {
            string chemin = Path.Combine(dossier, LessonProgressStore.FileName);
            LessonExercise avant;
            using (new CultureDuFil("fr-FR"))
            {
                avant = ExtraitDuDéfi(Jour);
                File.WriteAllText(chemin,
                    "{\n  \"version\": 1,\n  \"exercises\": {\n" +
                    $"    \"{avant.StableKey}\": {{ \"hash\": \"{avant.Hash}\", \"completed\": true, \"successfulAttempts\": 1, " +
                    "\"bestWpm\": 40, \"bestAccuracyPercent\": 97, \"bestSeconds\": 30, \"lastCompletedUtc\": null, \"hintsUsed\": 0 }\n" +
                    "  }\n}\n", new UTF8Encoding(false));
                Assert.True(new LessonProgressStore(chemin).IsCompleted(avant)); // témoin
            }

            using (new CultureDuFil("th-TH"))
            {
                var après = ExtraitDuDéfi(Jour);
                Assert.True(new LessonProgressStore(chemin).IsCompleted(après),
                    $"clé d'avant {avant.StableKey}, clé d'après {après.StableKey}");
            }
        }
        finally { Directory.Delete(dossier, recursive: true); }
    }

    // ── FR-03 : le calendrier Umm al-Qura s'arrête au 16/11/2077 ─────────────────────────────

    /// <summary>Sous ar-SA, <c>{date:yyyyMMdd}</c> lève ArgumentOutOfRangeException après le
    /// 2077-11-16 (plage d'UmAlQuraCalendar mesurée par la sonde) : le module du Défi ne se
    /// construit plus.</summary>
    [Fact]
    public void FR03_DefiDu17Novembre2077_SeConstruitSousArSA()
    {
        using var isolation = new BancCapture.Isolation();
        using var fil = new CultureDuFil("ar-SA");

        var erreur = Record.Exception(() => DailyChallenge.BuildModule(new DateOnly(2077, 11, 17), 0));

        Assert.Null(erreur);
    }

    // ── Réfutations ──────────────────────────────────────────────────────────────────────────

    /// <summary>LessonCatalog.cs:60 compare les titres en CurrentCultureIgnoreCase. Sur tout le
    /// catalogue, français et anglais, la casse turque ne change aucun résultat.</summary>
    [Theory]
    [InlineData("tr-TR")]
    [InlineData("az-Latn-AZ")]
    public void TitresModuleEtLecon_MemeVerdictSousLaCasseTurque(string culture)
    {
        using var isolation = new BancCapture.Isolation();
        int paires = 0;
        foreach (var langue in new[] { "fr", "en" })
        {
            isolation.SetLanguage(langue);
            var catalogue = LessonCatalogLoader.LoadFromResource();
            foreach (var module in catalogue.Modules)
                foreach (var leçon in module.Lessons)
                {
                    bool attendu = string.Equals(module.Title.Trim(), leçon.Title.Trim(), StringComparison.OrdinalIgnoreCase);
                    using var fil = new CultureDuFil(culture);
                    Assert.Equal(attendu, LessonNames.SameTitle(module.Title, leçon.Title));
                    paires++;
                }
        }
        Assert.True(paires > 20, $"{paires} paires seulement");
    }

    /// <summary>KeyMapper.cs:856/882 et VirtualKeyboard.cs:1244/1316/1320 testent
    /// <c>StartsWith("dk_")</c> avec la culture du fil. Sur toutes les chaînes de la disposition,
    /// le verdict linguistique est celui de la comparaison ordinale, sous les 22 cultures.</summary>
    [Theory]
    [MemberData(nameof(Cultures))]
    public void PrefixeDk_MemeVerdictQueLOrdinal_SurToutesLesChainesDeLaDisposition(string culture)
    {
        using var stream = typeof(LayoutLoader).Assembly.GetManifestResourceStream("layout.json")!;
        using var doc = JsonDocument.Parse(stream);
        var chaînes = new List<string>();
        void Parcourir(JsonElement e)
        {
            switch (e.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var p in e.EnumerateObject()) { chaînes.Add(p.Name); Parcourir(p.Value); }
                    break;
                case JsonValueKind.Array:
                    foreach (var x in e.EnumerateArray()) Parcourir(x);
                    break;
                case JsonValueKind.String:
                    chaînes.Add(e.GetString()!);
                    break;
            }
        }
        Parcourir(doc.RootElement);
        Assert.True(chaînes.Count > 500, $"{chaînes.Count} chaînes seulement");

        using var fil = new CultureDuFil(culture);
        var écarts = chaînes.Where(s => s.StartsWith("dk_") != s.StartsWith("dk_", StringComparison.Ordinal)).ToArray();
        Assert.Empty(écarts);
    }

    /// <summary>MaintainableLayersWindow.cs:383 et :443, PauseDurationDialog.cs:556 lisent un
    /// entier avec la culture du fil, mais après <see cref="NumericField.Digits"/> ou sa
    /// saisie filtrée : chiffres ASCII seuls, jamais de signe.</summary>
    [Theory]
    [MemberData(nameof(Cultures))]
    public void ChampsNumeriques_LisentLesChiffresAscii_SousToutFormatRegional(string culture)
    {
        using var isolation = new BancCapture.Isolation();
        using var fil = new CultureDuFil(culture);

        Assert.Equal(500, MaintainableLayersWindow.ClampDelay("500", out _));
        Assert.Equal(500, MaintainableLayersWindow.ClampDelay(" 5 0 0 ", out _));
        Assert.Equal(MaintainableLayersWindow.DelayDefault, MaintainableLayersWindow.ClampDelay("٥٠٠", out _));
        Assert.Equal("1000", MaintainableLayersWindow.DelayMax.ToString());
    }

    /// <summary>UsageStatsWindow.cs:732 (<c>N0</c>) et L.LessonsWindow.cs:74 (<c>#,0</c>) groupent
    /// les milliers avec L.DisplayCulture : U+202F en fr-FR sous ICU, « , » en en-US. Les polices
    /// embarquées qui peignent ces nombres (AG Texte, normal et demi-gras : DrawStatLine) ont le
    /// glyphe (GetGlyphIndicesW, comme PaperText.Covers). AG Titre ne l'a pas (mesuré le 17/11),
    /// mais ne peint aucun nombre groupé, et PaperText passe alors à la police suivante.</summary>
    [Theory]
    [InlineData(TypeRamp.Regular)]
    [InlineData(TypeRamp.Semibold)]
    public void SeparateurDeMilliers_AUnGlyphe_DansLaPoliceDesNombres(int graisse)
    {
        const string famille = AppFonts.Text;
        string fr = 1234567.ToString("N0", CultureInfo.ReadOnly(new CultureInfo("fr-FR", useUserOverride: false)));
        Assert.Contains(' ', fr); // prémisse : ICU, pas NLS
        IntPtr dc = Win32.CreateCompatibleDC(IntPtr.Zero);
        try
        {
            var police = FontSpec.Of(famille, TypeRamp.Body, graisse, 1f);
            var ancienne = Win32.SelectObject(dc, police.Handle);
            string texte = "  ,0123456789";
            var indices = new ushort[texte.Length];
            Win32.GetGlyphIndicesW(dc, texte, texte.Length, indices, Win32.GGI_MARK_NONEXISTING_GLYPHS);
            Win32.SelectObject(dc, ancienne);
            var manquants = texte.Where((c, i) => indices[i] == 0xFFFF).Select(c => $"U+{(int)c:X4}").ToArray();
            Assert.True(manquants.Length == 0, $"{famille} : glyphes absents {string.Join(" ", manquants)}");
        }
        finally { Win32.DeleteDC(dc); }
    }

    /// <summary>L.FormatDate et FormatDateCompact formatent avec L.DisplayCulture (fr-FR ou
    /// en-US) : la culture du fil ne change rien ; error.log date avec le format « s », qui est
    /// invariant par construction.</summary>
    [Theory]
    [MemberData(nameof(Cultures))]
    public void DatesAffichees_NeDependentPasDuFormatRegional(string culture)
    {
        using var isolation = new BancCapture.Isolation();
        var instant = new DateTime(2026, 11, 17, 14, 5, 9);
        string fr = L.FormatDate(Jour);
        isolation.SetLanguage("en");
        string en = L.FormatDateCompact(Jour);

        using var fil = new CultureDuFil(culture);
        Assert.Equal(en, L.FormatDateCompact(Jour));
        isolation.SetLanguage("fr");
        Assert.Equal(fr, L.FormatDate(Jour));
        Assert.Equal("2026-11-17T14:05:09", $"{instant:s}");
    }
}
