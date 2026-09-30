using ArrowOut.Data;
using ArrowOut.Data.Common;
using ArrowOut.Data.Models;
using ArrowOut.Services.Exceptions;
using ArrowOut.Services.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ArrowOut.Services.Users;

public interface IUserAdminService
{
    Task<PagedResult<UserListItem>> GetPagedAsync(UserQuery query, CancellationToken cancellationToken = default);

    Task<UserListItem> GetAsync(string userId, CancellationToken cancellationToken = default);

    Task SetLockedAsync(string userId, bool locked, string currentUserId);

    Task SetAdministratorAsync(string userId, bool isAdministrator, string currentUserId);

    Task DeleteAsync(string userId, string currentUserId);
}

public sealed class UserAdminService(
    ApplicationDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    TimeProvider timeProvider) : IUserAdminService
{
    public async Task<PagedResult<UserListItem>> GetPagedAsync(UserQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var adminIds = await GetAdministratorIdsAsync(cancellationToken);
        var adminIdList = adminIds.ToList(); // a List<T> turns into SQL properly (OPENJSON)
        var users = dbContext.Users.AsNoTracking();

        var search = query.Search?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            users = users.Where(u =>
                (u.Email != null && u.Email.Contains(search))
                || (u.DisplayName != null && u.DisplayName.Contains(search)));
        }

        users = query.Role switch
        {
            UserRoleFilter.Administrators => users.Where(u => adminIdList.Contains(u.Id)),
            UserRoleFilter.Players => users.Where(u => !adminIdList.Contains(u.Id)),
            _ => users,
        };

        var total = await users.CountAsync(cancellationToken);
        var size = PagedResult<UserListItem>.NormalizePageSize(query.PageSize);
        var page = PagedResult<UserListItem>.ClampPage(query.Page, size, total);
        var now = timeProvider.GetUtcNow();

        var rows = await users
            .OrderBy(u => u.Email)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(u => new
            {
                u.Id,
                u.Email,
                u.DisplayName,
                u.UserName,
                u.LockoutEnd,
                u.CreatedOn,
                Completed = u.Progress.Count(p => p.IsCompleted),
            })
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(r => new UserListItem(
                r.Id,
                r.Email ?? string.Empty,
                r.DisplayName ?? r.UserName ?? string.Empty,
                adminIds.Contains(r.Id),
                r.LockoutEnd.HasValue && r.LockoutEnd > now,
                r.Completed,
                r.CreatedOn))
            .ToList();

        return new PagedResult<UserListItem>(items, page, size, total);
    }

    public async Task<UserListItem> GetAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await FindAsync(userId);
        var completed = await dbContext.PlayerProgress.CountAsync(p => p.UserId == userId && p.IsCompleted, cancellationToken);

        return new UserListItem(
            user.Id,
            user.Email ?? string.Empty,
            user.PublicName,
            await userManager.IsInRoleAsync(user, Roles.Administrator),
            await userManager.IsLockedOutAsync(user),
            completed,
            user.CreatedOn);
    }

    public async Task SetLockedAsync(string userId, bool locked, string currentUserId)
    {
        EnsureNotSelf(userId, currentUserId, "lock your own account");
        var user = await FindAsync(userId);

        await Check(userManager.SetLockoutEnabledAsync(user, true));
        await Check(userManager.SetLockoutEndDateAsync(user, locked ? DateTimeOffset.MaxValue : null));

        if (locked)
        {
            // Logs them out everywhere the next time the security stamp is checked.
            await Check(userManager.UpdateSecurityStampAsync(user));
        }
    }

    public async Task SetAdministratorAsync(string userId, bool isAdministrator, string currentUserId)
    {
        EnsureNotSelf(userId, currentUserId, "change your own administrator role");
        var user = await FindAsync(userId);
        var isInRole = await userManager.IsInRoleAsync(user, Roles.Administrator);

        if (isAdministrator && !isInRole)
        {
            await Check(userManager.AddToRoleAsync(user, Roles.Administrator));
        }
        else if (!isAdministrator && isInRole)
        {
            await Check(userManager.RemoveFromRoleAsync(user, Roles.Administrator));
        }

        await Check(userManager.UpdateSecurityStampAsync(user));
    }

    public async Task DeleteAsync(string userId, string currentUserId)
    {
        EnsureNotSelf(userId, currentUserId, "delete your own account from the admin area");
        var user = await FindAsync(userId);
        await Check(userManager.DeleteAsync(user));
    }

    private async Task<HashSet<string>> GetAdministratorIdsAsync(CancellationToken cancellationToken)
    {
        var roleId = await dbContext.Roles
            .Where(r => r.Name == Roles.Administrator)
            .Select(r => r.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (roleId is null)
        {
            return [];
        }

        var ids = await dbContext.UserRoles
            .Where(ur => ur.RoleId == roleId)
            .Select(ur => ur.UserId)
            .ToListAsync(cancellationToken);

        return ids.ToHashSet();
    }

    private async Task<ApplicationUser> FindAsync(string userId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        return await userManager.FindByIdAsync(userId) ?? throw new EntityNotFoundException("User", userId);
    }

    private static void EnsureNotSelf(string userId, string currentUserId, string action)
    {
        if (string.Equals(userId, currentUserId, StringComparison.Ordinal))
        {
            throw new OperationNotAllowedException($"You cannot {action}.");
        }
    }

    private static async Task Check(Task<IdentityResult> operation)
    {
        var result = await operation;
        if (!result.Succeeded)
        {
            throw new OperationNotAllowedException(string.Join(" ", result.Errors.Select(e => e.Description)));
        }
    }
}
