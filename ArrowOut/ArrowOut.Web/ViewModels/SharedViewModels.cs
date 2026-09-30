using ArrowOut.Services.Models;

namespace ArrowOut.Web.ViewModels;

// What the _Pagination partial needs: the page, plus the query string to keep in the links.
public sealed class PaginationViewModel
{
    public PaginationViewModel(IPagedResult page, string action, IDictionary<string, string?> routeValues)
    {
        Page = page;
        Action = action;
        RouteValues = routeValues;
    }

    public IPagedResult Page { get; }

    public string Action { get; }

    public IDictionary<string, string?> RouteValues { get; }

    // A few page numbers around the current page.
    public IEnumerable<int> VisiblePages(int radius = 2)
    {
        var start = Math.Max(1, Page.Page - radius);
        var end = Math.Min(Page.TotalPages, Page.Page + radius);
        return Enumerable.Range(start, end - start + 1);
    }

    public IDictionary<string, string?> RouteFor(int page)
    {
        var values = new Dictionary<string, string?>(RouteValues, StringComparer.OrdinalIgnoreCase)
        {
            ["page"] = page.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };
        return values;
    }
}

public sealed record ErrorViewModel(int StatusCode, string Title, string Message, string? RequestId);
