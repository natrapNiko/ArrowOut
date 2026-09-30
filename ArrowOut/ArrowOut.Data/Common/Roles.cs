namespace ArrowOut.Data.Common;

public static class Roles
{
    public const string Administrator = "Administrator";
    public const string Player = "Player";

    public static readonly IReadOnlyList<string> All = [Administrator, Player];
}
