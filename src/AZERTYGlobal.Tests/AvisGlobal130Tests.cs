using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Retouches « avant le Store » de l'avis global du 10/11 (1.3.0) : C1 (libellé doublé du
/// sélecteur de leçon), sigle « VM » et double virgule de la révision du jour.
/// </summary>
public class AvisGlobal130Tests
{
    private static LessonsWindow Ouvrir()
    {
        var layout = LayoutLoader.LoadFromResource();
        var mapper = new KeyMapper(layout, new MockWin32Api());
        var hook = new KeyboardHook(mapper); // jamais installé
        ConfigManager.ClearWindowBounds(ConfigManager.LessonsWindowBoundsKey);
        return new LessonsWindow(layout, mapper, hook);
    }

    private static void Peindre(LessonsWindow fenêtre)
    {
        IntPtr hwnd = BancCapture.Handle(fenêtre);
        Win32.InvalidateRect(hwnd, IntPtr.Zero, false);
        Win32.SendMessageW(hwnd, Win32.WM_PAINT, IntPtr.Zero, IntPtr.Zero);
    }

    private static List<string> Noms(LessonsWindow fenêtre) =>
        BancCapture.Field<List<LessonControlView>>(fenêtre, "_controlViews").Select(c => c.Name).ToList();

    // ── C1 : un seul nom quand module et leçon portent le même titre ─────────

    [Theory]
    [InlineData("Prise en main", "Prise en main", "Prise en main")]
    [InlineData("Prise en main", "  prise en main ", "  prise en main ")]
    [InlineData("Getting started", "Getting started", "Getting started")]
    [InlineData("Majuscules accentuées", "Majuscules accentuées", "Majuscules accentuées")]
    [InlineData("Majuscules accentuées", "Capitale É", "Majuscules accentuées | Capitale É")]
    [InlineData("Courrier", "Prise en main", "Courrier | Prise en main")]
    public void Un_seul_nom_quand_les_deux_titres_sont_identiques(string module, string leçon, string attendu) =>
        Assert.Equal(attendu, LessonNames.Join(module, leçon, " | "));

    [Fact]
    public void Les_noms_pour_lecteur_d_ecran_de_l_accueil_n_ecrivent_pas_deux_fois_le_titre()
    {
        using var isolation = new BancCapture.Isolation();
        foreach (string langue in new[] { "fr", "en" })
        {
            L.Language = langue;
            string nom = L.Lessons_InitiationModuleTitle;
            foreach (string phrase in new[]
            {
                L.LessonsHome_ResumeName(nom, nom, 2, 5),
                L.LessonsHome_StartName(nom, nom, 1, 5),
                L.LessonsHome_SpokenResume(nom, nom, 3, 5),
            })
            {
                Assert.Contains(nom, phrase);
                Assert.DoesNotContain(nom + ", " + nom, phrase);
            }
            // Titres différents : module puis leçon, comme avant.
            Assert.Contains("Courrier, Prise", L.LessonsHome_ResumeName("Courrier", "Prise", 2, 5));
        }
    }

    /// <summary>Le catalogue réel : chaque couple module/leçon de même titre (Prise en main,
    /// Majuscules accentuées…) n'est désigné qu'une fois sur le bouton du sélecteur, ceux de titres
    /// différents gardent « module leçon ».</summary>
    [Theory]
    [InlineData("fr")]
    [InlineData("en")]
    public void Le_bouton_du_selecteur_n_ecrit_pas_deux_fois_le_meme_titre(string langue)
    {
        using var isolation = new BancCapture.Isolation();
        L.Language = langue;
        var fenêtre = Ouvrir();
        try
        {
            var catalogue = BancCapture.Field<LessonCatalog>(fenêtre, "_catalog");
            int doubles = 0, distincts = 0;
            foreach (var module in catalogue.Modules.Where(m => m.Id != DailyChallenge.ModuleId))
                foreach (var leçon in module.Lessons)
                {
                    Assert.True((bool)BancCapture.Call(fenêtre, "SelectExercise", module.Id, leçon.Id, 0, false)!);
                    Peindre(fenêtre);
                    var noms = Noms(fenêtre);
                    if (LessonNames.SameTitle(module.Title, leçon.Title))
                    {
                        doubles++;
                        Assert.Contains(leçon.Title, noms);
                        Assert.DoesNotContain($"{module.Title} {leçon.Title}", noms);
                    }
                    else
                    {
                        distincts++;
                        Assert.Contains($"{module.Title} {leçon.Title}", noms);
                    }
                }
            Assert.True(doubles >= 2, "« Prise en main » et « Majuscules accentuées » doivent être couverts");
            Assert.True(distincts > 0);
        }
        finally { BancCapture.Teardown(fenêtre); }
    }
}
