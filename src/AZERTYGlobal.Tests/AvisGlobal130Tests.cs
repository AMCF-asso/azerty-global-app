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

    // ── Sigle « VM » : Verr. Maj. en clair là où la place le permet ──────────

    [Theory]
    [InlineData("fr", "Verr. Maj.", "VM")]
    [InlineData("en", "Caps", "Caps")]
    public void Verr_Maj_s_ecrit_en_clair_dans_les_portees_et_en_sigle_dans_la_touche_du_mini_clavier(
        string langue, string enClair, string compact)
    {
        using var isolation = new BancCapture.Isolation();
        L.Language = langue;
        var caps = Gesture.Of("É")[0];
        Assert.Equal(Gesture.CapsLockCode, caps.KeyCode);
        Assert.Equal(enClair, caps.ShortLabel);
        Assert.Equal(compact, caps.CompactLabel);

        // La colonne de la portée (leçons, recherche, accueil) porte le nom en clair.
        Assert.Contains(TabColumn.FromGesture(Gesture.Of("É")), c => c.Key == enClair);

        // Aucun autre temps n'est touché : les lettres gardent leur gravure.
        var deux = Gesture.Of("É")[1];
        Assert.Equal(deux.KeyLabel, deux.ShortLabel);
        Assert.Equal(deux.KeyLabel, deux.CompactLabel);

        // Tous les gestes du catalogue : « VM » n'apparaît plus qu'en version compacte.
        foreach (var caractère in new[] { "É", "È", "Ç", "À", "Ù" })
            foreach (var temps in Gesture.Of(caractère))
                if (temps.KeyCode == Gesture.CapsLockCode)
                    Assert.NotEqual("VM", temps.ShortLabel);
    }

    /// <summary>« Le test de troncature tranche » : la touche Verr. Maj. du mini-clavier fait 1,75 unité
    /// (42 px à l'unité 24) ; le nom en clair n'y tient pas même à la police la plus réduite, le
    /// sigle y tient. D'où le sigle conservé à cet endroit seulement.</summary>
    [Fact]
    public void Le_nom_en_clair_ne_tient_pas_dans_la_touche_du_mini_clavier_le_sigle_y_tient()
    {
        using var isolation = new BancCapture.Isolation();
        L.Language = "fr";
        float unité = 24f; // MiniUnit des Leçons
        int largeurTouche = (int)Math.Round(1.75f * unité);
        IntPtr hdc = Win32.CreateCompatibleDC(IntPtr.Zero);
        try
        {
            int hauteur = Math.Max(6, (int)Math.Round(unité * 0.58f) - 2); // police la plus réduite
            var police = new FontSpec(AppFonts.Text, hauteur, TypeRamp.Semibold);
            Assert.True(PaperText.Width(hdc, L.Gesture_CapsLockShort, police) > largeurTouche - 2,
                "« Verr. Maj. » tient dans la touche : le sigle n'a plus de raison d'être là");
            var normale = new FontSpec(AppFonts.Text, (int)Math.Round(unité * 0.58f), TypeRamp.Semibold);
            Assert.True(PaperText.Width(hdc, L.Gesture_CapsLockCompact, normale) <= largeurTouche - 2);
        }
        finally { Win32.DeleteDC(hdc); }
    }

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
