using System.Reflection;
using System.Text.Json;
using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// « Alt Gr » à l'affichage (audit UX du 09/11, lot 7, décision d'Antoine) : un seul libellé,
/// <see cref="L.AltGrKey"/>, le même en français et en anglais, avec une espace insécable.
/// L'identifiant interne (couche, libellé de VisualKey, character-index.json) reste « AltGr ».
/// </summary>
public class AltGrLabelTests : IDisposable
{
    public AltGrLabelTests() => L.Language = "fr";
    public void Dispose() => L.Language = "fr";

    public static TheoryData<string> Langues => new() { "fr", "en" };

    [Fact]
    public void Le_libelle_a_une_espace_insecable()
    {
        Assert.Equal("Alt Gr", L.AltGrKey);
    }

    [Theory]
    [MemberData(nameof(Langues))]
    public void Touche_tooltip_et_lignes_disent_le_meme_libelle(string langue)
    {
        L.Language = langue;
        Assert.Equal(L.AltGrKey, L.Keyboard_KeyCap("AltGr"));
        Assert.Equal(L.AltGrKey, L.Gesture_LineAltGr);
        Assert.Equal(L.AltGrKey, L.LessonsHome_LayerName(HomeLayer.AltGr));
        Assert.Contains(L.AltGrKey, L.Keyboard_TooltipAltGr);
        Assert.Contains(L.AltGrKey, L.Search_LayerKeyLabel("Caps+Shift+AltGr", "A"));
    }

    [Fact]
    public void L_identifiant_interne_de_la_touche_reste_AltGr()
    {
        L.Language = "en";
        Assert.Single(VirtualKeyboard.BuildKeyLayout(), k => k.Label == "AltGr");
    }

    /// <summary>Toute chaîne sans paramètre de <c>L</c> : aucun « AltGr » collé.</summary>
    [Theory]
    [MemberData(nameof(Langues))]
    public void Aucun_texte_de_l_app_ne_dit_AltGr(string langue)
    {
        L.Language = langue;
        var fautifs = new List<string>();
        int lues = 0;
        foreach (var p in typeof(L).GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
        {
            if (p.PropertyType != typeof(string) || p.GetIndexParameters().Length > 0) continue;
            string? valeur = (string?)p.GetValue(null);
            lues++;
            if (valeur != null && valeur.Contains("AltGr", StringComparison.Ordinal)) fautifs.Add($"{p.Name} = {valeur}");
        }
        Assert.True(lues > 500, $"seulement {lues} chaînes lues");
        Assert.Empty(fautifs);

        foreach (var couche in new[] { "AltGr", "AltGr+Shift", "Shift+AltGr", "Caps+AltGr", "Caps+Shift+AltGr", "Caps+AltGr+Shift" })
            Assert.DoesNotContain("AltGr", L.Search_LayerKeyLabel(couche, "E"), StringComparison.Ordinal);
    }

    /// <summary>Consignes, titres et descriptions des leçons, une fois formatés : ni « AltGr »
    /// ni marqueur {ALTGR}, {SHIFT} ou {CAPS} resté brut.</summary>
    [Theory]
    [MemberData(nameof(Langues))]
    public void Les_textes_des_lecons_disent_Alt_Gr(string langue)
    {
        L.Language = langue;
        var catalog = LessonCatalogLoader.LoadFromResource();
        var textes = new List<string>();
        foreach (var module in catalog.Modules)
        {
            textes.Add(module.Title);
            textes.Add(module.Description);
            foreach (var lesson in module.Lessons)
            {
                textes.Add(lesson.Title);
                textes.Add(lesson.Description);
                textes.AddRange(lesson.Exercises.Select(e => LessonCatalogLoader.ExpandPlaceholders(e.Instruction)));
            }
        }
        Assert.Contains(textes, t => t.Contains(L.AltGrKey, StringComparison.Ordinal));
        Assert.DoesNotContain(textes, t => t.Contains("AltGr", StringComparison.Ordinal));
        // « Accolades { } » est un titre légitime : seuls les marqueurs connus sont cherchés.
        foreach (var marqueur in new[] { "{ALTGR}", "{SHIFT}", "{CAPS}" })
            Assert.DoesNotContain(textes, t => t.Contains(marqueur, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Corpus du Défi : du texte tapé, donc « Alt Gr » avec une espace ordinaire
    /// (une insécable obligerait à taper Maj + Alt Gr + Espace au milieu de la phrase).</summary>
    [Fact]
    public void Le_corpus_du_Defi_dit_Alt_Gr_avec_une_espace_ordinaire()
    {
        using var stream = typeof(DailyChallenge).Assembly.GetManifestResourceStream("defi-corpus.json")!;
        using var doc = JsonDocument.Parse(stream);
        var textes = new List<string>();
        Collect(doc.RootElement, textes);
        Assert.True(textes.Count > 50, $"{textes.Count} textes lus");
        Assert.DoesNotContain(textes, t => t.Contains("AltGr", StringComparison.Ordinal));
        Assert.DoesNotContain(textes, t => t.Contains(L.AltGrKey, StringComparison.Ordinal));
        Assert.Contains(textes, t => t.Contains("Alt Gr + N", StringComparison.Ordinal));
    }

    private static void Collect(JsonElement element, List<string> textes)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var p in element.EnumerateObject())
                {
                    if (p.Name == "text" && p.Value.ValueKind == JsonValueKind.String) textes.Add(p.Value.GetString()!);
                    else Collect(p.Value, textes);
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray()) Collect(item, textes);
                break;
        }
    }

    /// <summary>Le mot « AltGr » écrit en dur (original du site) s'affiche « Alt Gr », sans toucher
    /// « Alt Gr » ni un mot qui le contient.</summary>
    [Theory]
    [MemberData(nameof(Langues))]
    public void Le_mot_AltGr_ecrit_en_dur_s_affiche_Alt_Gr(string langue)
    {
        L.Language = langue;
        Assert.Equal($"Tenez {L.AltGrKey} + U ; {L.AltGrKey} + N", LessonCatalogLoader.ExpandPlaceholders("Tenez AltGr + U ; {ALTGR} + N"));
        Assert.Equal($"{L.AltGrKey} ({L.AltGrKey})", LessonCatalogLoader.ExpandPlaceholders($"{L.AltGrKey} (AltGr)"));
        Assert.Equal("AltGrafe", LessonCatalogLoader.ExpandPlaceholders("AltGrafe"));
    }

    /// <summary>La consigne réécrite ne coûte aucune progression : l'empreinte d'avant le
    /// 09/11, calculée avec l'ancienne consigne « AltGr + : », est toujours reconnue.</summary>
    [Theory]
    [InlineData("fr", "Tapez ces exemples de code (AltGr + : est le raccourci ergonomique)")]
    [InlineData("en", "Type these code examples (AltGr + : is the ergonomic shortcut)")]
    public void La_progression_d_avant_reste_reconnue(string langue, string ancienneConsigne)
    {
        L.Language = langue;
        var exercice = LessonCatalogLoader.LoadFromResource().Modules
            .SelectMany(m => m.Lessons).SelectMany(l => l.Exercises)
            .Single(e => e.KeyPrefix == "email-web/croisillon/2/");
        // lessons.json est une copie de l'original du site, qui écrit « AltGr » : c'est l'affichage
        // qui dit « Alt Gr » (ExpandPlaceholders), la consigne brute reste celle du fichier.
        Assert.DoesNotContain("AltGr", LessonCatalogLoader.ExpandPlaceholders(exercice.Instruction), StringComparison.Ordinal);
        string ancienne = LessonCatalogLoader.ComputeLegacyExerciseHash(exercice.Type, ancienneConsigne, exercice.Content);
        Assert.Contains(ancienne, exercice.LegacyHashes);
    }
}
