namespace ArrowOut.Services.Exceptions;

// Base class for errors we expect and can show to the user. The web layer turns each one
// into an HTTP status code. Any other exception is a 500.
public abstract class ArrowOutException : Exception
{
    protected ArrowOutException(string message)
        : base(message)
    {
    }

    protected ArrowOutException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    // HTTP status code for this error. Each subclass sets its own.
    public abstract int StatusCode { get; }

    // Short title for the ProblemDetails response.
    public abstract string Title { get; }
}

public sealed class EntityNotFoundException : ArrowOutException
{
    public EntityNotFoundException(string entityName, object key)
        : base($"{entityName} '{key}' was not found.")
    {
        EntityName = entityName;
        Key = key;
    }

    public string EntityName { get; }

    public object Key { get; }

    public override int StatusCode => 404;

    public override string Title => "Not found";
}

public sealed class LevelLockedException(int levelNumber)
    : ArrowOutException($"Level {levelNumber} is locked. Complete the previous level first.")
{
    public int LevelNumber { get; } = levelNumber;

    public override int StatusCode => 403;

    public override string Title => "Level locked";
}

public sealed class InvalidGameStateException(string message) : ArrowOutException(message)
{
    public override int StatusCode => 422;

    public override string Title => "Invalid game state";
}

public sealed class InvalidLevelDesignException(IReadOnlyList<string> errors)
    : ArrowOutException("The level design is invalid: " + string.Join(" ", errors))
{
    public IReadOnlyList<string> Errors { get; } = errors;

    public override int StatusCode => 422;

    public override string Title => "Invalid level design";
}

public sealed class DuplicateLevelNumberException(int number)
    : ArrowOutException($"A level with number {number} already exists.")
{
    public int Number { get; } = number;

    public override int StatusCode => 409;

    public override string Title => "Duplicate level number";
}

public sealed class ThemeValidationException(IReadOnlyList<string> errors)
    : ArrowOutException("The theme file is invalid: " + string.Join(" ", errors))
{
    public IReadOnlyList<string> Errors { get; } = errors;

    public override int StatusCode => 422;

    public override string Title => "Invalid theme";
}

public sealed class OperationNotAllowedException(string message) : ArrowOutException(message)
{
    public override int StatusCode => 409;

    public override string Title => "Operation not allowed";
}
