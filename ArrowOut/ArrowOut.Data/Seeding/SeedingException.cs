using Microsoft.AspNetCore.Identity;

namespace ArrowOut.Data.Seeding;

public sealed class SeedingException : Exception
{
    public SeedingException()
    {
    }

    public SeedingException(string message)
        : base(message)
    {
    }

    public SeedingException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public static void ThrowIfFailed(IdentityResult result, string operation)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Description}"));
            throw new SeedingException($"Seeding failed to {operation}: {errors}");
        }
    }
}
