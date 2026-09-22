using Xunit;

namespace TypingEngine.Windows.Tests;

/// <summary>
/// Témoin R11 de la revue de code du 2026-09-21.
///
/// Le raccourci de la recherche de caractères portait la garde « saisie sécurisée »
/// (<c>AdvancedFeaturesSuppressed</c>), celui du clavier virtuel non : Ctrl+Maj+Q faisait
/// surgir le clavier au-dessus d'un champ mot de passe. Les deux branches passent
/// désormais par <c>KeyboardHook.ShortcutOpensWindow</c>, pour qu'un oubli ne puisse plus
/// porter sur une seule des deux.
///
/// ⚠️ Limite assumée : ce témoin tient le prédicat, pas le fait que les deux branches
/// l'appellent — le callback du hook n'est pas atteignable par la suite. C'est le partage
/// du prédicat, et non le test, qui rend l'asymétrie difficile à réintroduire.
/// </summary>
public class SecureInputShortcutTests
{
    private const uint VK_W = 0x57;

    /// <summary>Cas nominal : hors saisie sécurisée, un raccourci configuré s'arme.</summary>
    [Fact]
    public void HorsSaisieSecurisee_LeRaccourciSArme()
    {
        Assert.True(KeyboardHook.ShortcutOpensWindow(false, VK_W));
    }

    /// <summary>Le défaut R11 lui-même.</summary>
    [Fact]
    public void SaisieSecurisee_LeRaccourciNeSArmePas()
    {
        Assert.False(KeyboardHook.ShortcutOpensWindow(true, VK_W));
    }

    /// <summary>
    /// Second motif du prédicat, conservé des deux branches d'origine : un raccourci non
    /// configuré (VK nul) ne s'arme jamais, sécurisé ou non. Sans ce témoin, un prédicat
    /// réduit à la seule garde de sécurité passerait.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RaccourciNonConfigure_NeSArmeJamais(bool saisieSecurisee)
    {
        Assert.False(KeyboardHook.ShortcutOpensWindow(saisieSecurisee, 0));
    }
}
