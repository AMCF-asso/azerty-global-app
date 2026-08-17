using System.Reflection;
using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// La version de l'application vit en quatre endroits sans rien pour les synchroniser :
/// <c>Program.Version</c> (infobulle du tray, URL de rapport de bug), les attributs de
/// <c>AssemblyInfo.cs</c>, <c>&lt;Version&gt;</c> du csproj et <c>AppxManifest.xml</c>.
/// <c>GenerateAssemblyInfo</c> est désactivé pour une raison documentée — le commentaire
/// MSBuild généré finit dans le binaire AOT et déclenche un avertissement WACK — donc les
/// attributs sont écrits à la main.
///
/// Le 2026-08-17, <c>Program.cs</c> et <c>AssemblyInfo.cs</c> étaient restés en 1.1.2 quand
/// le csproj et le manifeste étaient passés en 1.2.0 : l'application se serait annoncée en
/// 1.1.2 dans son infobulle et étiqueté tous ses rapports de bug en 1.1.2.
/// <c>Verify-Release.ps1</c> l'attrapait, mais seulement au moment des portes de release, et
/// une levée à la fois. Ces tests le voient à chaque <c>dotnet test</c>, donc en CI avant
/// même le packaging.
///
/// Le csproj et le manifeste restent couverts par <c>Verify-Release.ps1</c> : ce sont des
/// fichiers de build, hors de portée d'un test qui ne connaît que l'assembly compilé.
/// </summary>
public class VersionAlignmentTests
{
    private static readonly Assembly App = typeof(Program).Assembly;

    /// <summary>Le suffixe « +hash » d'un build source-linké n'est pas une divergence.</summary>
    private static string InformationalVersion()
    {
        var raw = App.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        Assert.NotNull(raw);
        int plus = raw!.IndexOf('+');
        return plus >= 0 ? raw[..plus] : raw;
    }

    private static string FileVersion()
    {
        var raw = App.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;
        Assert.NotNull(raw);
        return raw!;
    }

    [Fact]
    public void ProgramVersion_HasThreeParts()
    {
        Assert.Matches(@"^\d+\.\d+\.\d+$", Program.Version);
    }

    /// <summary>Ce qui s'affiche à l'utilisateur et ce que porte le binaire doivent être
    /// le même numéro.</summary>
    [Fact]
    public void ProgramVersion_MatchesAssemblyInformationalVersion()
    {
        Assert.Equal(Program.Version, InformationalVersion());
    }

    /// <summary>Les quatre segments du Store se déduisent des trois de l'application :
    /// c'est la règle appliquée par Verify-Release.ps1 pour le manifeste.</summary>
    [Fact]
    public void AssemblyFileVersion_IsProgramVersionWithRevisionZero()
    {
        Assert.Equal(Program.Version + ".0", FileVersion());
    }

    [Fact]
    public void AssemblyVersion_IsProgramVersionWithRevisionZero()
    {
        var assemblyVersion = App.GetName().Version;
        Assert.NotNull(assemblyVersion);
        Assert.Equal(Program.Version + ".0", assemblyVersion!.ToString());
    }
}
