namespace TypingEngine.Core;

/// <summary>Thrown when a layout cannot be read, naming the place at fault. The parser used
/// to let two undiagnosable exceptions escape instead: JsonElement.GetProperty raises a bare
/// KeyNotFoundException ("The given key was not present in the dictionary.") which names
/// neither the missing property nor the key it was read from, and uint.Parse raises
/// FormatException or OverflowException carrying the offending value with no idea of where it
/// came from. A layout is written by hand, so where matters as much as what.</summary>
public sealed class LayoutFormatException : Exception
{
    public LayoutFormatException(string path, string problem)
        : base($"Layout unreadable at {path}: {problem}")
    {
        Path = path;
        Problem = problem;
    }

    /// <summary>JSON path of the place at fault, such as <c>rows[2].keys[5].scancode</c>.</summary>
    public string Path { get; }

    /// <summary>What is wrong there, stated without the path.</summary>
    public string Problem { get; }
}
