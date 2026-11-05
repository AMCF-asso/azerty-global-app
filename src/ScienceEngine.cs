// Liaison au moteur AZERTY Global Science (Rust, dépôt AMCF-asso/science, crate science-ffi).
//
// Une seule source de vérité : le moteur Rust, jamais porté en C#. L'ABI C tient en trois
// fonctions (README de crates/science-ffi) : la saisie part en UTF-16 telle quelle, le moteur
// rend un document JSON UTF-8 versionné, libéré par science_free. Par défaut (ScienceLink=dll),
// science_ffi.dll est livrée à côté de l'exe, en publication comme en build JIT ; le lien
// statique (ScienceLink=static, DirectPInvoke) ne passe qu'en ARM64 tant que rustc stable n'a
// pas ehcont (ScienceEngine.targets).
//
// Les positions des diagnostics et des points de choix sont en unités UTF-16 : elles se
// passent telles quelles à EM_SETSEL.
using System.Runtime.InteropServices;
using System.Text.Json;

namespace AZERTYGlobal;

internal enum ScienceStatus { Formula, Ambiguous, Rejected, Error }

/// <summary>Point de choix rencontré (A1 à A10 de SPEC § 5) et lecture prise.</summary>
internal readonly record struct ScienceChoice(string Code, int Start, int End, bool Second);

internal sealed record ScienceReading(
    string Canonical, string Latex, string UnicodeMath, string MathML, IReadOnlyList<ScienceChoice> Choices);

internal readonly record struct ScienceDiagnostic(string Code, int Start, int End, string? Detail, string Message);

/// <summary>
/// Formula : une lecture. Ambiguous : de deux à huit, dans l'ordre de la spécification.
/// Rejected : des diagnostics. Error : saisie invalide (UTF-16 mal formé) ou moteur absent,
/// avec <see cref="Error"/>.
/// </summary>
internal sealed record ScienceResult(
    ScienceStatus Status, IReadOnlyList<ScienceReading> Readings, IReadOnlyList<ScienceDiagnostic> Diagnostics, string? Error);

internal static partial class ScienceEngine
{
    /// <summary>Version des fonctions exportées attendue (science_abi_version).</summary>
    public const uint AbiVersion = 1;

    /// <summary>Version du document JSON attendue (champ "v").</summary>
    public const int ContractVersion = 1;

    private const string Library = "science_ffi";

    [LibraryImport(Library)]
    private static partial uint science_abi_version();

    [LibraryImport(Library, StringMarshalling = StringMarshalling.Utf16)]
    private static partial int science_analyze(string input, nuint length, uint lang, out IntPtr output, out nuint outputLength);

    [LibraryImport(Library)]
    private static partial void science_free(IntPtr output, nuint length);

    /// <summary>
    /// Vrai si le moteur est présent et parle la même ABI : une DLL absente ou ancienne rend
    /// faux au lieu de planter.
    /// </summary>
    public static bool IsAvailable
    {
        get
        {
            try { return science_abi_version() == AbiVersion; }
            catch (DllNotFoundException) { return false; }
            catch (EntryPointNotFoundException) { return false; }
        }
    }

    /// <summary>Analyse une ligne saisie. Ne lève pas : un moteur absent donne Error.</summary>
    public static ScienceResult Analyze(string input, bool english)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!IsAvailable)
            return Failure("engine");
        byte[] json;
        int code = science_analyze(input, (nuint)input.Length, english ? 1u : 0u, out IntPtr output, out nuint length);
        if (code != 0)
            return Failure("abi");
        try
        {
            json = new byte[checked((int)length)];
            Marshal.Copy(output, json, 0, json.Length);
        }
        finally
        {
            science_free(output, length);
        }
        return Parse(json);
    }

    /// <summary>Lit le document JSON du moteur ; un contrat d'une autre version donne Error.</summary>
    internal static ScienceResult Parse(byte[] json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.GetProperty("v").GetInt32() != ContractVersion)
            return Failure("contract");
        var status = root.GetProperty("status").GetString() switch
        {
            "formula" => ScienceStatus.Formula,
            "ambiguous" => ScienceStatus.Ambiguous,
            "rejected" => ScienceStatus.Rejected,
            _ => ScienceStatus.Error,
        };
        var readings = new List<ScienceReading>();
        foreach (var r in root.GetProperty("readings").EnumerateArray())
        {
            var choices = new List<ScienceChoice>();
            foreach (var c in r.GetProperty("choices").EnumerateArray())
                choices.Add(new ScienceChoice(
                    c.GetProperty("code").GetString()!, c.GetProperty("start").GetInt32(),
                    c.GetProperty("end").GetInt32(), c.GetProperty("second").GetBoolean()));
            readings.Add(new ScienceReading(
                r.GetProperty("canonical").GetString()!, r.GetProperty("latex").GetString()!,
                r.GetProperty("unicodemath").GetString()!, r.GetProperty("mathml").GetString()!, choices));
        }
        var diagnostics = new List<ScienceDiagnostic>();
        foreach (var d in root.GetProperty("diagnostics").EnumerateArray())
            diagnostics.Add(new ScienceDiagnostic(
                d.GetProperty("code").GetString()!, d.GetProperty("start").GetInt32(),
                d.GetProperty("end").GetInt32(), d.GetProperty("detail").GetString(),
                d.GetProperty("message").GetString()!));
        string? error = root.TryGetProperty("error", out var e) ? e.GetString() : null;
        return new ScienceResult(status, readings, diagnostics, error);
    }

    private static ScienceResult Failure(string error) => new(ScienceStatus.Error, [], [], error);
}
