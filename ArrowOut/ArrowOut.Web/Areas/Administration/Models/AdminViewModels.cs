using ArrowOut.Services.Models;
using ArrowOut.Services.Themes;
using ArrowOut.Web.ViewModels;

namespace ArrowOut.Web.Areas.Administration.Models;

public sealed class AdminLevelsIndexViewModel
{
    public required AdminLevelQuery Query { get; init; }

    public required PagedResult<AdminLevelListItem> Levels { get; init; }

    public PaginationViewModel Pagination => new(Levels, "Index", new Dictionary<string, string?>
    {
        ["search"] = Query.Search,
        ["difficulty"] = Query.Difficulty?.ToString(),
        ["isPublished"] = Query.IsPublished?.ToString(),
        ["sort"] = Query.Sort == AdminLevelSort.NumberAsc ? null : Query.Sort.ToString(),
    });
}

public sealed class LevelFormViewModel
{
    public int? Id { get; init; }

    public required LevelInputModel Input { get; init; }

    public LevelDesignReport? Report { get; init; }

    public bool IsEdit => Id.HasValue;
}

public sealed class UsersIndexViewModel
{
    public required UserQuery Query { get; init; }

    public required PagedResult<UserListItem> Users { get; init; }

    public required string CurrentUserId { get; init; }

    public PaginationViewModel Pagination => new(Users, "Index", new Dictionary<string, string?>
    {
        ["search"] = Query.Search,
        ["role"] = Query.Role == UserRoleFilter.All ? null : Query.Role.ToString(),
    });
}

public sealed class ThemesIndexViewModel
{
    public required IReadOnlyList<ThemeDefinition> Themes { get; init; }

    public required Func<string, bool> IsBuiltIn { get; init; }
}
