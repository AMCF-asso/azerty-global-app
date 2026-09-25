namespace AZERTYGlobal;

internal sealed record LessonHintMethod(
    string Type,
    string? Key,
    string? Layer,
    string? DeadKey,
    string? DkActivationKey,
    string? DkActivationLayer)
{
    public bool IsDeadKey => string.Equals(Type, "deadkey", StringComparison.OrdinalIgnoreCase);

    public string? DeadKeyToken => !string.IsNullOrEmpty(DeadKey) && DeadKey.StartsWith("dk_", StringComparison.Ordinal)
        ? "dk:" + DeadKey[3..]
        : null;
}

internal readonly record struct LessonHintKeyStep(string? Key, string? Layer);

internal sealed class LessonHintProvider
{
    private readonly Dictionary<string, LessonHintMethod> _methods = new(StringComparer.Ordinal);

    public LessonHintProvider()
    {
        // Vue sur l'index partagé (audit du 25/09, V-04) : la méthode recommandée, sinon la première.
        foreach (var entry in CharacterIndex.Shared.Entries)
        {
            if (entry.Preferred is not { } m) continue;
            _methods[entry.Character] = new LessonHintMethod(
                m.Type,
                NullIfEmpty(m.Key),
                NullIfEmpty(m.Layer),
                NullIfEmpty(m.DeadKey),
                NullIfEmpty(m.DkActivationKey),
                NullIfEmpty(m.DkActivationLayer));
        }
    }

    public LessonHintMethod? GetRecommendedMethod(char ch)
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

    public static LessonHintKeyStep GetCurrentStep(LessonHintMethod method, string? activeDeadKey)
    {
        if (method.IsDeadKey &&
            !string.IsNullOrEmpty(method.DeadKey) &&
            !string.Equals(activeDeadKey, method.DeadKey, StringComparison.Ordinal) &&
            !string.IsNullOrEmpty(method.DkActivationKey))
            return new LessonHintKeyStep(method.DkActivationKey, method.DkActivationLayer);

        return new LessonHintKeyStep(method.Key, method.Layer);
    }

    private static string? NullIfEmpty(string value) => value.Length > 0 ? value : null;
}
