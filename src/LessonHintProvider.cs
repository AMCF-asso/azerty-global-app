namespace AZERTYGlobal;

/// <summary>Couches d'une méthode de character-index.json (« Shift+AltGr », « Caps+Shift »…),
/// lues une fois pour toutes par <see cref="LessonHintProvider.ParseLayers"/>.</summary>
[Flags]
internal enum HintLayers
{
    None = 0,
    Shift = 1,
    AltGr = 2,
    Caps = 4
}

/// <summary>
/// Comment guider vers le caractère suivant. Le tutoriel guide pas à pas, selon l'état du
/// clavier ; les Leçons montrent la méthode recommandée telle quelle. Un seul calcul, deux
/// réglages (audit du 25/09, L-02).
/// </summary>
/// <param name="CapsLockFirst">Méthode directe sur une couche Caps : Verr. Maj. d'abord, la
/// touche une fois Verr. Maj. active.</param>
/// <param name="SmartCapsForLetters">Maj demandée pour une lettre alors que Verr. Maj. est
/// active : Verr. Maj. suffit, c'est elle qui est surlignée.</param>
/// <param name="RedirectOtherDeadKey">Une autre touche morte est armée : sa touche finale si
/// elle donne aussi le caractère, sinon Retour arrière pour l'annuler. Sans ce réglage, le
/// guidage revient à l'armement de la bonne.</param>
/// <param name="KeepCapsLock">Exercices 1 et 2 du tutoriel : Verr. Maj. reste surlignée tout
/// l'exercice.</param>
internal readonly record struct GuideOptions(
    bool CapsLockFirst,
    bool SmartCapsForLetters,
    bool RedirectOtherDeadKey,
    bool KeepCapsLock)
{
    public static GuideOptions Lessons => default;

    public static GuideOptions Tutorial(bool keepCapsLock) => new(true, true, true, keepCapsLock);
}

internal sealed class LessonHintProvider
{
    internal const string CapsLockLabel = "Verr. Maj.";
    private const uint BackspaceScancode = 0x0E;

    private readonly Dictionary<string, MethodData> _methods = new(StringComparer.Ordinal);

    public LessonHintProvider()
    {
        // Vue sur l'index partagé (audit du 25/09, V-04) : la méthode recommandée, sinon la première.
        foreach (var entry in CharacterIndex.Shared.Entries)
        {
            if (entry.Preferred is { } m)
                _methods[entry.Character] = m;
        }
    }

    public MethodData? GetRecommendedMethod(char ch)
    {
        return _methods.TryGetValue(ch.ToString(), out var method) ? method : null;
    }

    public void AddRequiredCharacters(IEnumerable<string> characters, ISet<string> visibleCharacters)
    {
        foreach (var value in characters)
            AddRequiredCharacters(value, visibleCharacters);
    }

    public void AddRequiredCharacters(string text, ISet<string> visibleCharacters)
    {
        foreach (char ch in text)
        {
            if (ch == '\r' || ch == '\n')
                continue;

            visibleCharacters.Add(ch.ToString());
            AddRequiredDeadKey(ch, visibleCharacters);
        }
    }

    public void AddRequiredDeadKey(char ch, ISet<string> visibleCharacters)
    {
        var method = GetRecommendedMethod(ch);
        if (!string.IsNullOrEmpty(method?.DeadKeyToken))
            visibleCharacters.Add(method.DeadKeyToken);
    }

    /// <summary>Couches d'une méthode. Une couche contient ses modificateurs : « Caps+Shift »
    /// demande Maj et Verr. Maj.</summary>
    internal static HintLayers ParseLayers(string? layer)
    {
        if (string.IsNullOrEmpty(layer))
            return HintLayers.None;
        var layers = HintLayers.None;
        if (layer.Contains("Shift", StringComparison.OrdinalIgnoreCase)) layers |= HintLayers.Shift;
        if (layer.Contains("AltGr", StringComparison.OrdinalIgnoreCase)) layers |= HintLayers.AltGr;
        if (layer.Contains("Caps", StringComparison.OrdinalIgnoreCase)) layers |= HintLayers.Caps;
        return layers;
    }

    /// <summary>
    /// Surligne dans <paramref name="state"/> la prochaine touche à presser pour taper
    /// <paramref name="character"/> par <paramref name="method"/>, et le genre de ce
    /// surlignage : direct, armement de la touche morte (étape 1) ou touche finale (étape 2).
    /// Fonction pure de l'état du clavier (touche morte armée, Verr. Maj.) et des réglages.
    /// </summary>
    internal static void Guide(KeyboardRenderState state, MethodData method, string character,
        string? activeDeadKey, bool capsLockActive, GuideOptions options)
    {
        if (options.CapsLockFirst && method.Type == "direct" && method.Layer.StartsWith("Caps", StringComparison.Ordinal))
        {
            // Verr. Maj. reste surlignée tant qu'elle est requise : contour tant qu'elle est
            // éteinte, fond plein une fois allumée, et la touche apparaît alors.
            state.HighlightKind = KeyHighlight.Direct;
            state.HighlightedLabels.Add(CapsLockLabel);
            if (capsLockActive)
            {
                AddKey(state, method.Key);
                var layers = ParseLayers(method.Layer);
                if (layers.HasFlag(HintLayers.Shift))
                    state.HighlightedContextIds.Add(VirtualKeyboard.ContextShiftLeft);
                // « Caps+AltGr » : l'espace fine de l'exercice 2 (AltGr + Espace).
                if (layers.HasFlag(HintLayers.AltGr))
                    state.HighlightedLabels.Add("AltGr");
            }
            return;
        }

        if (method.IsDeadKey && method.DkActivationKey.Length > 0)
        {
            if (activeDeadKey == null)
            {
                state.HighlightKind = KeyHighlight.Step1;
                AddKeyAndLayers(state, method.DkActivationKey, method.DkActivationLayer, capsLockActive, options);
            }
            else if (activeDeadKey == method.DeadKey)
            {
                state.HighlightKind = KeyHighlight.Step2;
                AddKeyAndLayers(state, method.Key, method.Layer, capsLockActive, options);
            }
            else if (options.RedirectOtherDeadKey)
            {
                state.HighlightKind = KeyHighlight.Step2;
                var other = ResolveStep2MethodForActiveDeadKey(character, method, activeDeadKey,
                    CharacterIndex.Shared.DeadKeyMethodsByCharacter);
                if (other != null)
                    AddKeyAndLayers(state, other.Key, other.Layer, capsLockActive, options);
                else
                    state.HighlightedScancodes.Add(BackspaceScancode);
            }
            else
            {
                state.HighlightKind = KeyHighlight.Step1;
                AddKeyAndLayers(state, method.DkActivationKey, method.DkActivationLayer, capsLockActive, options);
            }
        }
        else
        {
            state.HighlightKind = KeyHighlight.Direct;
            AddKeyAndLayers(state, method.Key, method.Layer, capsLockActive, options);
        }

        if (options.KeepCapsLock)
            state.HighlightedLabels.Add(CapsLockLabel);
    }

    /// <summary>Touche morte armée qui n'est pas celle de la méthode recommandée : la méthode
    /// qui donne le même caractère par elle, s'il y en a une.</summary>
    internal static MethodData? ResolveStep2MethodForActiveDeadKey(
        string character,
        MethodData preferredMethod,
        string activeDeadKey,
        IReadOnlyDictionary<string, List<MethodData>> deadKeyMethodsByCharacter)
    {
        if (!string.Equals(preferredMethod.Type, "deadkey", StringComparison.OrdinalIgnoreCase))
            return null;

        if (string.Equals(preferredMethod.DeadKey, activeDeadKey, StringComparison.Ordinal))
            return preferredMethod;

        if (!deadKeyMethodsByCharacter.TryGetValue(character, out var methods))
            return null;

        return methods.FirstOrDefault(method =>
            string.Equals(method.Type, "deadkey", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(method.DeadKey, activeDeadKey, StringComparison.Ordinal));
    }

    private static void AddKeyAndLayers(KeyboardRenderState state, string key, string layer, bool capsLockActive,
        GuideOptions options)
    {
        bool isLetter = AddKey(state, key);
        var layers = ParseLayers(layer);
        if (layers.HasFlag(HintLayers.Shift))
        {
            // Verr. Maj. active et lettre : la majuscule sort déjà, Maj serait redondante.
            if (options.SmartCapsForLetters && capsLockActive && isLetter)
                state.HighlightedLabels.Add(CapsLockLabel);
            else
                state.HighlightedContextIds.Add(VirtualKeyboard.ContextShiftLeft);
        }
        if (layers.HasFlag(HintLayers.AltGr))
            state.HighlightedLabels.Add("AltGr");
        if (layers.HasFlag(HintLayers.Caps))
            state.HighlightedLabels.Add(CapsLockLabel);
    }

    /// <summary>Surligne la touche physique ; rend vrai pour une touche de lettre.</summary>
    private static bool AddKey(KeyboardRenderState state, string key)
    {
        if (string.IsNullOrEmpty(key) || !VirtualKeyboard.KeyCodeToScancode.TryGetValue(key, out var scancode))
            return false;
        state.HighlightedScancodes.Add(scancode);
        return KeyboardRenderer.IsLetterKey(scancode);
    }
}
