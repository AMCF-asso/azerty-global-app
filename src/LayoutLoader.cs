using TypingEngine.Core;

namespace AZERTYGlobal;

/// <summary>Loads the app-owned embedded layout and delegates parsing to the portable core.</summary>
static class LayoutLoader
{
    public static Layout LoadFromResource(string resourceName = "layout.json")
    {
        using var stream = typeof(LayoutLoader).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new FileNotFoundException($"Ressource '{resourceName}' introuvable dans l'assemblage.");
        return LayoutJsonParser.Parse(stream);
    }
}
