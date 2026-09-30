namespace ArrowOut.Game;

// Thrown when a layout breaks the rules (out of bounds, overlapping, duplicate ids).
public sealed class InvalidBoardException : Exception
{
    public InvalidBoardException()
        : this(["The board definition is invalid."])
    {
    }

    public InvalidBoardException(string message)
        : this([message])
    {
    }

    public InvalidBoardException(string message, Exception innerException)
        : base(message, innerException)
    {
        Errors = [message];
    }

    public InvalidBoardException(IReadOnlyList<string> errors)
        : base(string.Join(" ", errors))
    {
        Errors = errors;
    }

    public IReadOnlyList<string> Errors { get; }
}
