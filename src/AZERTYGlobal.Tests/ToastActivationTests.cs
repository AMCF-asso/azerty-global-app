using System.Linq;
using System.Xml.Linq;
using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Tests de la partie testable hors runtime COM/WinRT de l'activateur de toast (v1.2.0) :
/// échappement XML du contenu injecté dans le template ToastGeneric, et cohérence du CLSID
/// avec le manifest MSIX. Le trajet COM complet (CoRegisterClassObject, activation) relève
/// du smoke test packagé.
/// </summary>
public class ToastActivationTests
{
    [Fact]
    public void EscapeXml_NeutralizesMarkupCharacters()
    {
        Assert.Equal("a&amp;b &lt;t&gt; &quot;q&quot; &apos;s&apos;",
            ToastActivation.EscapeXml("a&b <t> \"q\" 's'"));
    }

    [Fact]
    public void EscapeXml_LeavesPlainTextUntouched()
    {
        Assert.Equal("Vous aimez AZERTY Global ?", ToastActivation.EscapeXml("Vous aimez AZERTY Global ?"));
    }

    private static readonly XNamespace Com =
        "http://schemas.microsoft.com/appx/manifest/com/windows10";
    private static readonly XNamespace Desktop =
        "http://schemas.microsoft.com/appx/manifest/desktop/windows10";

    /// <summary>Remonte au manifeste MSIX depuis le dossier de sortie des tests. Le
    /// commentaire remplacé affirmait qu'il n'était pas lisible d'ici : il l'est, et
    /// LessonCoreTests remonte déjà à la racine du dépôt de la même façon.</summary>
    private static string FindAppxManifest()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            string candidate = Path.Combine(dir.FullName, "msix", "AppxManifest.xml");
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }

        throw new FileNotFoundException("msix/AppxManifest.xml introuvable depuis les tests.");
    }

    /// <summary>
    /// Extrait les deux déclarations qui doivent porter le CLSID de l'activateur : les
    /// `com:Class/@Id` du serveur COM, et les
    /// `desktop:ToastNotificationActivation/@ToastActivatorCLSID`. Séparée du test pour
    /// être éprouvée sur des manifestes fabriqués, sains comme cassés.
    /// </summary>
    private static (string[] ComClassIds, string[] ToastClsids) ReadActivatorDeclarations(
        XDocument manifest) => (
        manifest.Descendants(Com + "Class")
            .Select(e => (string?)e.Attribute("Id") ?? "").ToArray(),
        manifest.Descendants(Desktop + "ToastNotificationActivation")
            .Select(e => (string?)e.Attribute("ToastActivatorCLSID") ?? "").ToArray());

    /// <summary>Manifeste minimal fabriqué, chaque déclaration étant omissible.</summary>
    private static string ManifestFragment(string? comClassId, string? toastClsid)
    {
        string comBlock = comClassId == null ? "" :
            "<com:Extension Category=\"windows.comServer\"><com:ComServer>" +
            "<com:ExeServer Executable=\"AZERTY Global.exe\">" +
            "<com:Class Id=\"" + comClassId + "\" />" +
            "</com:ExeServer></com:ComServer></com:Extension>";
        string toastBlock = toastClsid == null ? "" :
            "<desktop:Extension Category=\"windows.toastNotificationActivation\">" +
            "<desktop:ToastNotificationActivation ToastActivatorCLSID=\"" + toastClsid + "\" />" +
            "</desktop:Extension>";

        return "<Package xmlns=\"http://schemas.microsoft.com/appx/manifest/foundation/windows10\" " +
               "xmlns:desktop=\"" + Desktop + "\" xmlns:com=\"" + Com + "\">" +
               "<Applications><Application Id=\"AZERTYGlobal\"><Extensions>" +
               comBlock + toastBlock +
               "</Extensions></Application></Applications></Package>";
    }

    /// <summary>
    /// B2 de l'audit v1.2.0 : ce test comparait deux chaînes codées en dur et n'ouvrait
    /// jamais le manifeste, si bien qu'il est resté vert alors qu'aucune déclaration
    /// `com:` n'existait. Il lit désormais le fichier livré au packaging.
    /// </summary>
    [Fact]
    public void ActivatorClsid_MatchesAppxManifestDeclaration()
    {
        var (comClassIds, toastClsids) =
            ReadActivatorDeclarations(XDocument.Load(FindAppxManifest()));

        Assert.Equal(new[] { ToastActivation.ActivatorClsidString }, comClassIds);
        Assert.Equal(new[] { ToastActivation.ActivatorClsidString }, toastClsids);
    }

    /// <summary>
    /// Témoin : un manifeste sans extension `com:` — l'état réel du dépôt avant la
    /// correction de B2 — rend des listes vides, donc fait échouer le test ci-dessus.
    /// Sans ce témoin, un lecteur muet passerait pour un garde-fou.
    /// </summary>
    [Fact]
    public void ReadActivatorDeclarations_ManifesteSansServeurCom_NeTrouveRien()
    {
        var (comClassIds, toastClsids) = ReadActivatorDeclarations(
            XDocument.Parse(ManifestFragment(comClassId: null, toastClsid: null)));

        Assert.Empty(comClassIds);
        Assert.Empty(toastClsids);
    }

    /// <summary>
    /// Témoin réciproque : un CLSID divergent est rapporté tel quel, donc vu. Les deux
    /// témoins ensemble prouvent que l'extraction lit vraiment les attributs.
    /// </summary>
    [Fact]
    public void ReadActivatorDeclarations_ClsidDivergent_EstRapporteTelQuel()
    {
        const string autre = "00000000-1111-2222-3333-444444444444";
        var (comClassIds, toastClsids) = ReadActivatorDeclarations(XDocument.Parse(
            ManifestFragment(comClassId: ToastActivation.ActivatorClsidString, toastClsid: autre)));

        Assert.Equal(new[] { ToastActivation.ActivatorClsidString }, comClassIds);
        Assert.Equal(new[] { autre }, toastClsids);
    }
}

/// <summary>
/// <c>ToastActivation.ToastBody</c> ne garde que la premiere ligne d'un corps, parce que la
/// seconde n'existe que pour dire ce que le clic ouvre et que le bouton du toast la remplace.
/// La troncature n'est donc sure que tant que cette convention tient : ces tests la verrouillent
/// sur les corps qui empruntent reellement le canal toast, au lieu de la supposer.
/// </summary>
public class ToastBodyTests
{
    /// <summary>Les corps du canal toast : sollicitation d'avis (4 variantes) et relance du
    /// lancement automatique. Chacun doit porter exactement une ligne d'affordance.</summary>
    public static TheoryData<string> CorpsDuCanalToast()
    {
        var data = new TheoryData<string>();
        foreach (var langue in new[] { "fr", "en" })
        {
            L.Language = langue;
            for (int essai = 1; essai <= 2; essai++)
            {
                data.Add(L.Tray_ReviewPromptBodyStore(essai));
                data.Add(L.Tray_ReviewPromptBodyFeedback(essai));
            }
            data.Add(L.Tray_AutoStartNudgeBody);
        }
        L.Language = "fr";
        return data;
    }

    [Theory]
    [MemberData(nameof(CorpsDuCanalToast))]
    public void UnCorpsDuCanalToast_NAQuUneLigneDAffordance(string corps)
    {
        Assert.Equal(1, corps.Count(c => c == '\n'));
    }

    [Theory]
    [MemberData(nameof(CorpsDuCanalToast))]
    public void ToastBody_RetireLAffordance_EtGardeLeReste(string corps)
    {
        var attendu = corps.Split('\n')[0];
        Assert.Equal(attendu, ToastActivation.ToastBody(corps));
        Assert.DoesNotContain("\n", ToastActivation.ToastBody(corps));
        Assert.NotEmpty(ToastActivation.ToastBody(corps));
    }

    /// <summary>Reciproque : sans saut de ligne, le corps traverse intact. Sans cette
    /// assertion, un ToastBody qui renverrait la chaine vide passerait les tests ci-dessus
    /// pour un corps sans affordance.</summary>
    [Fact]
    public void ToastBody_SansSautDeLigne_RendLeCorpsIntact()
    {
        Assert.Equal("une seule ligne", ToastActivation.ToastBody("une seule ligne"));
    }
}
