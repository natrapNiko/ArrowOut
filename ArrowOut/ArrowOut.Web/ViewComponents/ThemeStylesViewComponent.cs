using ArrowOut.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace ArrowOut.Web.ViewComponents;

// Writes the light and dark colours out as CSS variables for _Layout.
public class ThemeStylesViewComponent(IThemeResolver themeResolver) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync() =>
        View(await themeResolver.ResolveAsync(HttpContext));
}
