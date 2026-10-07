using System.Reflection;
using AZERTYGlobal;
using TypingEngine.Windows;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Lot clarté du 2026-10-07 : les bulles, l'infobulle et la ligne d'état du menu disent
/// ce qui se passe, pourquoi, et quoi faire. Ce que ces tests tiennent : les longueurs que
/// Windows accepte (NOTIFYICONDATAW) et la typographie, en FR comme en EN.
///
/// Bornes : szInfoTitle[64], szInfo[256] et szTip[128], terminateur compris, soit 63, 255
/// et 127 caractères, au-delà desquels le marshalling de Win32.cs coupe en silence.
/// Microsoft recommande 48 pour le titre et 200 pour le corps : ce sont les bornes
/// tenues pour les chaînes fixes. Ce qui reste à la recette : la coupe visuelle réelle de
/// la notification Windows, que le banc de captures ne couvre pas.
/// </summary>
public class TrayClarityTests : IDisposable
{
    public TrayClarityTests() => L.Language = "fr";
    public void Dispose() => L.Language = "fr";

    private const int TitleMax = 63, BodyMax = 255, TipMax = 127;
    private const int TitleAdvised = 48, BodyAdvised = 200;

    // Noms longs mais réels de la liste anti-triche et des clients distants.
    private const string LongProcess = "Minecraft.Windows";
    private static readonly string TooltipPrefix = ProductIdentity.DisplayName + " v1.3.0 — ";

    public static TheoryData<string> Languages => new() { "fr", "en" };

    /// <summary>Chaînes fixes du lot : (nom, valeur) dans la langue courante.</summary>
    private static IEnumerable<(string Name, string Value)> ClarityStrings()
    {
        foreach (var prop in typeof(L).GetProperties(BindingFlags.Public | BindingFlags.Static))
        {
            if (!prop.Name.StartsWith("TrayClarity_") || prop.PropertyType != typeof(string)) continue;
            yield return (prop.Name, (string)prop.GetValue(null)!);
        }
        yield return ("TrayClarity_GameCompatRemovedBody", L.TrayClarity_GameCompatRemovedBody("x"));
        // Le compte à rebours « 23:59:59 » porte des deux-points sans espace : il est
        // éprouvé en longueur seulement, plus bas.
        yield return ("TrayClarity_TooltipPausedByYou", L.TrayClarity_TooltipPausedByYou("x"));
    }

    [Theory, MemberData(nameof(Languages))]
    public void LesCorpsDeBulleRestentSousLaLongueurConseillee(string lang)
    {
        L.Language = lang;
        var bodies = ClarityStrings().Where(s => s.Name.EndsWith("Body")).ToList();
        Assert.NotEmpty(bodies);
        foreach (var (name, value) in bodies)
            Assert.True(value.Length <= BodyAdvised, $"{lang} {name} : {value.Length} caractères");
    }

    [Theory, MemberData(nameof(Languages))]
    public void LesTitresRestentSousLaLongueurConseillee(string lang)
    {
        L.Language = lang;
        Assert.True(L.TrayClarity_GameCompatRemovedTitle.Length <= TitleAdvised);
        // Titres composés avec un nom de processus, réutilisés par les nouvelles bulles.
        foreach (var title in new[]
                 {
                     L.Tray_ToggleRefusedTitle(LongProcess), L.Tray_SuspendedInTitle(LongProcess),
                     L.Tray_SuspendedDuringTitle("TeamViewer"), L.Tray_PrecautionTitle, L.Tray_ForceRefusedTitle,
                     L.Tray_PausedTitle, L.Layers_SearchSecureTitle
                 })
            Assert.True(title.Length <= TitleAdvised, $"{lang} « {title} » : {title.Length} caractères");
    }

    [Theory, MemberData(nameof(Languages))]
    public void LaListeDesJeuxRetiresTientDansLaBulle(string lang)
    {
        // Trois réglages forcés retirés d'un coup : cas déjà large pour une seule bulle.
        L.Language = lang;
        string body = L.TrayClarity_GameCompatRemovedBody(
            "FortniteClient-Win64-Shipping.exe, VALORANT-Win64-Shipping.exe, r5apex.exe");
        Assert.True(body.Length <= BodyMax, $"{lang} : {body.Length} caractères");
    }

    [Theory, MemberData(nameof(Languages))]
    public void LInfobulleDitPourquoiEtTientDansSzTip(string lang)
    {
        L.Language = lang;
        var tips = new List<string>
        {
            L.TrayClarity_TooltipPausedByYou("23:59:59"),
            L.TrayClarity_TooltipDisabled,
        };
        foreach (CompatibilitySuspendReason reason in Enum.GetValues<CompatibilitySuspendReason>())
            tips.Add(TrayApplication.SuspensionReasonText(reason));
        foreach (var tip in tips)
        {
            Assert.False(string.IsNullOrWhiteSpace(tip));
            int total = (TooltipPrefix + tip).Length;
            Assert.True(total <= TipMax, $"{lang} « {tip} » : {total} caractères");
        }
    }

    [Theory, MemberData(nameof(Languages))]
    public void ChaqueMotifDeSuspensionASonTexte(string lang)
    {
        L.Language = lang;
        var texts = new[]
        {
            CompatibilitySuspendReason.AntiCheat, CompatibilitySuspendReason.RemoteAccess,
            CompatibilitySuspendReason.UserOverride, CompatibilitySuspendReason.UnknownForeground
        }.Select(TrayApplication.SuspensionReasonText).ToList();
        Assert.Equal(texts.Count, texts.Distinct().Count());
        // Plus de « Suspendu pour compatibilité », qui taisait la cause.
        Assert.DoesNotContain(L.Tray_TooltipSuspendedCompat, texts);
    }

    [Fact]
    public void LeFrancaisPorteEspacesInsecablesEtApostropheTypographique()
    {
        L.Language = "fr";
        foreach (var (name, value) in ClarityStrings())
        {
            Assert.DoesNotContain("'", value);
            for (int i = 1; i < value.Length; i++)
            {
                if (":;!?»".IndexOf(value[i]) >= 0)
                    Assert.True(value[i - 1] is ' ' or ' ', $"{name} : espace avant « {value[i]} » dans \"{value}\"");
                if (value[i - 1] == '«')
                    Assert.True(value[i] is ' ' or ' ', $"{name} : espace après « dans \"{value}\"");
            }
        }
    }

    [Fact]
    public void LAnglaisPorteApostropheTypographiqueSansEspaceAvantLaPonctuation()
    {
        L.Language = "en";
        foreach (var (name, value) in ClarityStrings())
        {
            Assert.DoesNotContain("'", value);
            Assert.DoesNotContain("\"", value);
            for (int i = 1; i < value.Length; i++)
                if (":;!?".IndexOf(value[i]) >= 0)
                    Assert.False(char.IsWhiteSpace(value[i - 1]), $"{name} : espace avant « {value[i]} »");
        }
    }

    [Theory, MemberData(nameof(Languages))]
    public void LesBullesDisentQuoiFaire(string lang)
    {
        // Chaque corps finit par une issue : rien à faire, un chemin, ou un état conservé.
        L.Language = lang;
        string[] issues = lang == "fr"
            ? new[] { "Rien à faire", "Pour annuler", "Pour l’activer", "Pour reprendre", "Réactivez", "réglage n’a pas changé", "revient dès" }
            : new[] { "Nothing to do", "To undo", "To turn it on", "To resume", "Turn it back on", "Turn ", "setting hasn’t changed", "comes back" };
        foreach (var (name, value) in ClarityStrings().Where(s => s.Name.EndsWith("Body")))
            Assert.True(issues.Any(value.Contains), $"{lang} {name} ne dit pas quoi faire : \"{value}\"");
    }
}
