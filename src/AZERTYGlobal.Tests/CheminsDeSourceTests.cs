using System.Text;
using System.Text.RegularExpressions;
using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Audit de régression 1.3.0, RP4 : la couture de troncature reçoit <c>[CallerFilePath]</c>, que
/// le compilateur remplace par le chemin complet du fichier source. Sans <c>PathMap</c>, le
/// dossier du poste de build entrait dans le binaire livré, qui dépendait alors de l'endroit où
/// il est compilé (contre la reproductibilité, SEV-A4-02). Le projet remplace son dossier par
/// « /_/ ». Le binaire publié (NativeAOT) se vérifie par <c>scripts/check-source-paths.py</c>.
/// </summary>
public sealed class CheminsDeSourceTests
{
    /// <summary>Lettre de lecteur seule (pas la fin de « https »), « :\ » ou « :/ » sans second
    /// séparateur (pas une adresse « https:// »), puis un chemin qui finit par « .cs ». Une
    /// adresse web suivie, dans le tas des chaînes, de « /_/Fichier.cs » passait pour un chemin
    /// du poste (fusion du 10/11).</summary>
    private static readonly Regex CheminDuPoste = new(@"(?<![A-Za-z])[A-Za-z]:[\\/](?![\\/])[^\x00-\x1f""<>|*?]{1,400}?\.cs(?![A-Za-z0-9])");

    [Fact]
    public void Une_adresse_web_n_est_pas_un_chemin_du_poste()
    {
        Assert.Empty(CheminDuPoste.Matches("https://discord.gg/abc─Onboarding WndProc\u2D00/_/OnboardingWindow.cs"));
        Assert.Single(CheminDuPoste.Matches(@"x C:\Users\poste\src\OnboardingWindow.cs"));
        Assert.Single(CheminDuPoste.Matches("D:/build/src/GdiHelpers.cs"));
    }

    [Fact]
    public void L_assemblage_du_produit_ne_porte_aucun_chemin_de_source_du_poste()
    {
        byte[] octets = File.ReadAllBytes(typeof(TruncationProbe).Assembly.Location);
        // Les chaînes C# sont en UTF-16 dans l'assemblage, à une adresse paire ou impaire (les
        // deux décalages sont lus) ; l'UTF-8 couvre le reste.
        var trouvés = CheminDuPoste.Matches(Encoding.Unicode.GetString(octets)).Select(m => m.Value)
            .Concat(CheminDuPoste.Matches(Encoding.Unicode.GetString(octets, 1, octets.Length - 1)).Select(m => m.Value))
            .Concat(CheminDuPoste.Matches(Encoding.Latin1.GetString(octets)).Select(m => m.Value))
            .Distinct().ToList();

        Assert.True(trouvés.Count == 0,
            $"{trouvés.Count} chemin(s) de source du poste de build dans l'assemblage :\n" + string.Join("\n", trouvés.Take(5)));
    }

    [Fact]
    public void La_couture_recoit_un_chemin_sans_le_dossier_du_poste()
    {
        var vus = new List<TruncationProbe.Passage>();
        TruncationProbe.Observer = vus.Add;
        IntPtr hdc = Win32.CreateCompatibleDC(IntPtr.Zero);
        try
        {
            var police = FontSpec.Of(AppFonts.Text, 15, TypeRamp.Regular, 1f);
            var bouton = new Win32.RECT { left = 0, top = 0, right = 10, bottom = 30 };
            GdiHelpers.DrawLightButton(hdc, bouton, "Continuer les leçons", police.Handle, primary: true,
                hot: false, pressed: false, enabled: true, radius: 4, backdrop: 0xFFFFFF);
        }
        finally
        {
            TruncationProbe.Observer = null;
            Win32.DeleteDC(hdc);
        }

        var passage = Assert.Single(vus);
        Assert.Equal("/_/GdiHelpers.cs", passage.File.Replace('\\', '/'));
    }
}
