// Le geste d'un caractère, tel que la Tablature l'écrit (refonte 1.3.0, maquettes du 07/10).
namespace AZERTYGlobal;

/// <summary>
/// Un temps du geste : les modificateurs tenus (le trait épais de la portée) et la touche
/// frappée (la case). <see cref="KeyLabel"/> est la gravure AZERTY de la touche physique
/// (« W », « ù », « Espace »), celle qu'on cherche des yeux sur son clavier.
/// </summary>
internal readonly record struct GestureStep(bool Shift, bool AltGr, string KeyCode, string KeyLabel)
{
    /// <summary>La case de la portée : gravure courte (« Esp » pour Espace).</summary>
    public string ShortLabel => Gesture.ShortKeyLabel(KeyCode, KeyLabel);
}

/// <summary>
/// Les temps qui tapent un caractère : un seul pour une frappe directe ; deux pour une touche
/// morte (« Frappez ù : rien ne s'affiche encore. Puis tenez Maj, frappez Z. ») ; Verr. Maj.
/// d'abord pour une couche Caps. Lu dans la méthode recommandée de character-index.json,
/// commun aux Leçons, à la recherche et au clavier virtuel.
/// </summary>
internal static class Gesture
{
    /// <summary>Code de touche → gravure physique AZERTY. La gravure ne change pas avec la
    /// langue de l'interface ; seule « Espace » se traduit (<see cref="KeyLabel"/>).</summary>
    internal static readonly IReadOnlyDictionary<string, string> KeyLegends = new Dictionary<string, string>
    {
        ["Backquote"] = "²",
        ["Digit1"] = "&",
        ["Digit2"] = "é",
        ["Digit3"] = "\"",
        ["Digit4"] = "'",
        ["Digit5"] = "(",
        ["Digit6"] = "-",
        ["Digit7"] = "è",
        ["Digit8"] = "_",
        ["Digit9"] = "ç",
        ["Digit0"] = "à",
        ["Minus"] = ")",
        ["Equal"] = "=",
        ["KeyQ"] = "A",
        ["KeyW"] = "Z",
        ["KeyE"] = "E",
        ["KeyR"] = "R",
        ["KeyT"] = "T",
        ["KeyY"] = "Y",
        ["KeyU"] = "U",
        ["KeyI"] = "I",
        ["KeyO"] = "O",
        ["KeyP"] = "P",
        ["BracketLeft"] = "^",
        ["BracketRight"] = "$",
        ["KeyA"] = "Q",
        ["KeyS"] = "S",
        ["KeyD"] = "D",
        ["KeyF"] = "F",
        ["KeyG"] = "G",
        ["KeyH"] = "H",
        ["KeyJ"] = "J",
        ["KeyK"] = "K",
        ["KeyL"] = "L",
        ["Semicolon"] = "M",
        ["Quote"] = "ù",
        ["Backslash"] = "*",
        ["IntlBackslash"] = "<",
        ["KeyZ"] = "W",
        ["KeyX"] = "X",
        ["KeyC"] = "C",
        ["KeyV"] = "V",
        ["KeyB"] = "B",
        ["KeyN"] = "N",
        ["KeyM"] = ",",
        ["Comma"] = ".",
        ["Period"] = "/",
        ["Slash"] = "§",
    };

    /// <summary>Code pseudo-touche de Verr. Maj. dans un geste.</summary>
    internal const string CapsLockCode = "CapsLock";

    /// <summary>Gravure d'une touche : la légende AZERTY, ou le mot traduit d'Espace et de
    /// Verr. Maj.</summary>
    internal static string KeyLabel(string keyCode) => keyCode switch
    {
        "Space" => L.Search_SpaceKeyLabel,
        CapsLockCode => L.Search_CapsLockWord,
        _ => KeyLegends.TryGetValue(keyCode, out var legend) ? legend : keyCode,
    };

    /// <summary>Gravure courte d'une case de portée (« Esp », « VM »).</summary>
    internal static string ShortKeyLabel(string keyCode, string label) => keyCode switch
    {
        "Space" => L.Gesture_SpaceShort,
        CapsLockCode => L.Gesture_CapsLockShort,
        _ => label,
    };

    /// <summary>Les temps de la méthode recommandée de <paramref name="character"/>, vide
    /// si le caractère n'est pas dans l'index.</summary>
    internal static IReadOnlyList<GestureStep> Of(string character)
        => CharacterIndex.Shared.ByCharacter.TryGetValue(character, out var entry) && entry.Preferred is { } method
            ? Of(method)
            : Array.Empty<GestureStep>();

    /// <summary>Les temps d'une méthode de saisie.</summary>
    internal static IReadOnlyList<GestureStep> Of(MethodData method)
    {
        var steps = new List<GestureStep>(3);
        if (method.IsDeadKey)
        {
            string activationKey = method.DkActivationKey;
            string activationLayer = method.DkActivationLayer;
            if (activationKey.Length == 0
                && CharacterIndex.Shared.DeadKeyActivations.TryGetValue(method.DeadKey, out var activation))
                (activationKey, activationLayer) = activation;
            if (activationKey.Length > 0)
                AddSteps(steps, activationKey, activationLayer);
        }
        AddSteps(steps, method.Key, method.Layer);
        return steps;
    }

    private static void AddSteps(List<GestureStep> steps, string keyCode, string layer)
    {
        var layers = LessonHintProvider.ParseLayers(layer);
        if (layers.HasFlag(HintLayers.Caps))
            steps.Add(new GestureStep(false, false, CapsLockCode, KeyLabel(CapsLockCode)));
        if (keyCode.Length > 0)
            steps.Add(new GestureStep(layers.HasFlag(HintLayers.Shift), layers.HasFlag(HintLayers.AltGr), keyCode, KeyLabel(keyCode)));
    }

    /// <summary>Le geste a-t-il besoin d'une case sur la portée des Leçons ? Non quand on
    /// frappe la touche gravée du caractère lui-même (« n », « U » avec Maj, « ! »).</summary>
    internal static bool StrikesItsOwnKey(string character, IReadOnlyList<GestureStep> steps)
        => steps.Count == 1 && steps[0].KeyCode != "Space"
           && string.Equals(steps[0].KeyLabel, character, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Le geste en toutes lettres, pour le panneau de lecture et le lecteur d'écran :
    /// « Tenez Maj et AltGr, frappez W. » ; une touche morte prévient que rien ne s'affiche
    /// au premier temps.
    /// </summary>
    internal static List<TextRun> Sentence(IReadOnlyList<GestureStep> steps, IReadOnlyList<string>? deadKeyCodes = null)
    {
        var runs = new List<TextRun>();
        for (int i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            bool first = i == 0;
            if (!first)
                runs.Add(new TextRun(L.Gesture_Then));
            var held = new List<string>(2);
            if (step.Shift) held.Add(L.Settings_ShortcutModifier2);
            if (step.AltGr) held.Add("AltGr");
            if (held.Count > 0)
            {
                runs.Add(new TextRun(first ? L.Gesture_HoldCapital : L.Gesture_Hold));
                for (int h = 0; h < held.Count; h++)
                {
                    if (h > 0) runs.Add(new TextRun(L.Gesture_And));
                    runs.Add(new TextRun(held[h], true));
                }
                runs.Add(new TextRun(L.Gesture_StrikeAfterHold));
            }
            else
            {
                runs.Add(new TextRun(first ? L.Gesture_StrikeCapital : L.Gesture_Strike));
            }
            runs.Add(new TextRun(step.KeyLabel, true));
            bool armsDeadKey = steps.Count > 1 && i < steps.Count - 1 && step.KeyCode != CapsLockCode;
            runs.Add(new TextRun(armsDeadKey ? L.Gesture_NothingYet : step.KeyLabel.EndsWith('.') ? "" : "."));
        }
        return runs;
    }

    /// <summary>La phrase en texte simple (lecteur d'écran, infobulle).</summary>
    internal static string PlainSentence(IReadOnlyList<GestureStep> steps)
        => string.Concat(Sentence(steps).Select(run => run.Text));
}
