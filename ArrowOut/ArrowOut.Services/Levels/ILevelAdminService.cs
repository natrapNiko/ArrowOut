using ArrowOut.Services.Models;

namespace ArrowOut.Services.Levels;

public interface ILevelAdminService
{
    Task<PagedResult<AdminLevelListItem>> GetPagedAsync(AdminLevelQuery query, CancellationToken cancellationToken = default);

    Task<LevelInputModel> GetForEditAsync(int id, CancellationToken cancellationToken = default);

    Task<LevelInputModel> CreateDraftAsync(CancellationToken cancellationToken = default);

    // Throws InvalidLevelDesignException or DuplicateLevelNumberException.
    Task<int> CreateAsync(LevelInputModel model, CancellationToken cancellationToken = default);

    Task UpdateAsync(int id, LevelInputModel model, CancellationToken cancellationToken = default);

    Task DeleteAsync(int id, CancellationToken cancellationToken = default);

    Task SetPublishedAsync(int id, bool isPublished, CancellationToken cancellationToken = default);

    Task<bool> IsNumberAvailableAsync(int number, int? excludeLevelId, CancellationToken cancellationToken = default);

    LevelDesignReport Analyze(int width, int height, string? arrowsJson);

    IReadOnlyList<ArrowInputModel> Generate(GenerateLevelRequest request);

    Task<(string FileName, byte[] Content)> ExportAsync(int id, CancellationToken cancellationToken = default);

    Task<int> ImportAsync(Stream content, CancellationToken cancellationToken = default);

    Task<DashboardStats> GetDashboardStatsAsync(CancellationToken cancellationToken = default);
}
