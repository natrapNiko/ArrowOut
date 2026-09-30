namespace ArrowOut.Services.Models;

// Non-generic version so one pagination partial works for any list.
public interface IPagedResult
{
    int Page { get; }

    int PageSize { get; }

    int TotalCount { get; }

    int TotalPages { get; }

    bool HasPrevious { get; }

    bool HasNext { get; }
}

public static class Paging
{
    public const int DefaultPageSize = 12;
    public const int MaxPageSize = 100;
}

public sealed class PagedResult<T> : IPagedResult
{
    public const int DefaultPageSize = Paging.DefaultPageSize;
    public const int MaxPageSize = Paging.MaxPageSize;

    public PagedResult(IReadOnlyList<T> items, int page, int pageSize, int totalCount)
    {
        ArgumentNullException.ThrowIfNull(items);

        Items = items;
        PageSize = NormalizePageSize(pageSize);
        TotalCount = Math.Max(0, totalCount);
        Page = Math.Clamp(page, 1, Math.Max(1, TotalPages));
    }

    public IReadOnlyList<T> Items { get; }

    public int Page { get; }

    public int PageSize { get; }

    public int TotalCount { get; }

    public int TotalPages => TotalCount == 0 ? 1 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasPrevious => Page > 1;

    public bool HasNext => Page < TotalPages;

    public static int NormalizePage(int page) => Math.Max(1, page);

    public static int NormalizePageSize(int pageSize) =>
        pageSize < 1 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);

    // If the page number is past the end, use the last page instead.
    public static int ClampPage(int page, int pageSize, int totalCount)
    {
        var size = NormalizePageSize(pageSize);
        var last = Math.Max(1, (int)Math.Ceiling(Math.Max(0, totalCount) / (double)size));
        return Math.Clamp(page, 1, last);
    }

    public static PagedResult<T> Empty(int pageSize = DefaultPageSize) => new([], 1, pageSize, 0);
}
