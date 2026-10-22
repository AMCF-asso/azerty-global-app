using System.Collections.Generic;
using AZERTYGlobal;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Capture 3 de la fiche Store 1.3.0 (Leçons, QCM d'Antoine du 22/10) : la page « Votre
/// texte » avec une vraie phrase qui mêle capitales accentuées, ligature, wolof, éwé et
/// portugais. Même chemin que <c>lecons-texte</c> ; rien dans <c>src/</c> ne change.
/// </summary>
public partial class KeyboardContextBench
{
    private const string StorePhrase =
        "« ÇA GÈLE À MONTRÉAL ! » Merci de tout cœur : jërëjëf à Dakar, akpé à Ŋɔtsie, obrigado à São Paulo.";

    private static void AddStoreStates(List<(string Name, System.Action Run)> attempts, BancCapture.Target target,
        Layout layout, KeyMapper mapper, KeyboardHook hook)
    {
        attempts.Add(("lecons-store-texte", () => CaptureLessonsHome(target, "lecons-store-texte", layout, mapper, hook, "",
            page: "Text", text: StorePhrase)));
    }
}
