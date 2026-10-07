using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Paramètres, lot « clarté » (07/10) : un refus de raccourci dit pourquoi, à la place de
/// l'invite, et Échap annule la saisie même après un refus. Avant, un refus effaçait l'invite
/// et Échap fermait la fenêtre au lieu de rendre l'ancienne touche.
/// </summary>
public class SettingsClarityTests : IDisposable
{
    private const int VK_ESCAPE = 0x1B;

    public SettingsClarityTests() => L.Language = "fr";
    public void Dispose() => L.Language = "fr";

    private static IEnumerable<char> LettresRefusees()
    {
        for (char letter = 'A'; letter <= 'Z'; letter++)
            if (!ConfigManager.IsShortcutAllowedVk(letter))
                yield return letter;
    }

    [Theory]
    [InlineData("fr")]
    [InlineData("en")]
    public void Chaque_lettre_refusee_dit_ce_qu_elle_fait_deja(string langue)
    {
        L.Language = langue;
        var usages = new HashSet<string>();
        foreach (char letter in LettresRefusees())
        {
            string? usage = L.SettingsClarity_ReservedUse(letter);
            Assert.False(string.IsNullOrEmpty(usage), $"{letter} : aucune raison");
            Assert.True(usages.Add(usage!), $"{letter} : raison partagée avec une autre lettre");
            Assert.Contains(usage!, L.SettingsClarity_ShortcutReservedLetter(letter));
        }
        Assert.NotEmpty(usages);
    }

    [Fact]
    public void Le_refus_suit_la_regle_de_ConfigManager()
    {
        Assert.Null(SettingsWindow.ShortcutRefusal('Q'));
        Assert.Null(SettingsWindow.ShortcutRefusal('7'));
        Assert.Null(SettingsWindow.ShortcutRefusal(0x7B)); // F12
        Assert.Equal(L.SettingsClarity_ShortcutReservedLetter('T'), SettingsWindow.ShortcutRefusal('T'));
        Assert.Equal(L.SettingsClarity_ShortcutKeypadDigit, SettingsWindow.ShortcutRefusal(0x63)); // pavé 3
        Assert.Equal(L.SettingsClarity_ShortcutOtherKey, SettingsWindow.ShortcutRefusal(0x25)); // flèche gauche
    }

    // Le test de typographie générale ignore les méthodes à paramètre char : celles-ci sont
    // vérifiées ici, lettre par lettre.
    [Fact]
    public void Les_refus_par_lettre_suivent_la_typographie_francaise()
    {
        foreach (char letter in LettresRefusees())
        {
            string text = L.SettingsClarity_ShortcutReservedLetter(letter);
            for (int i = 1; i < text.Length; i++)
            {
                Assert.False(":;?!»".Contains(text[i]) && text[i - 1] == ' ', $"{letter} : espace normale dans « {text} »");
                Assert.False(text[i - 1] == '«' && text[i] == ' ', $"{letter} : espace normale après « dans {text}");
                Assert.False(text[i] == '\'', $"{letter} : apostrophe droite dans « {text} »");
            }
        }
    }

    [Fact]
    public void Echap_apres_un_refus_annule_sans_fermer()
    {
        using var window = new SettingsWindow();
        IntPtr search = BancCapture.Field<IntPtr>(window, "_hWndEditSearch");
        Assert.NotEqual(IntPtr.Zero, search);

        BancCapture.Call(window, "ApplyCapturedShortcut", search, (uint)'T');
        Assert.False(BancCapture.Field<bool>(window, "_searchValid"));
        Assert.Equal(L.SettingsClarity_ShortcutReservedLetter('T'), BancCapture.Field<string>(window, "_validationMessage"));

        // Échap passe par la vraie procédure de la case : elle annule au lieu de fermer.
        Win32.SendMessageW(search, Win32.WM_KEYDOWN, (IntPtr)VK_ESCAPE, IntPtr.Zero);
        Assert.True(BancCapture.Field<bool>(window, "_searchValid"));
        Assert.Equal(string.Empty, BancCapture.Field<string>(window, "_validationMessage"));
    }

    [Fact]
    public void Une_touche_deja_prise_nomme_la_fenetre_qui_l_utilise()
    {
        using var window = new SettingsWindow();
        IntPtr search = BancCapture.Field<IntPtr>(window, "_hWndEditSearch");
        uint keyboardVk = BancCapture.Field<uint>(window, "_keyboardVk");

        BancCapture.Call(window, "ApplyCapturedShortcut", search, keyboardVk);
        Assert.Equal(L.SettingsClarity_ShortcutTakenByKeyboard(ConfigManager.GetShortcutDisplayName(keyboardVk)),
            BancCapture.Field<string>(window, "_validationMessage"));
    }
}
