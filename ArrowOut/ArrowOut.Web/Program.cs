using System.Text.Json.Serialization;
using ArrowOut.Data;
using ArrowOut.Data.Models;
using ArrowOut.Data.Seeding;
using ArrowOut.Services;
using ArrowOut.Web.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

var builder = WebApplication.CreateBuilder(args);
// Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options => options
    .UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(maxRetryCount: 5))
    // If the model and the migrations don't match, just log it instead of crashing on start-up.
    .ConfigureWarnings(w => w.Log(RelationalEventId.PendingModelChangesWarning)));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddOptions<SeedOptions>()
    .Bind(builder.Configuration.GetSection(SeedOptions.SectionName))
    .ValidateDataAnnotations();
builder.Services.AddDatabaseSeeding();

// Identity
builder.Services
    .AddDefaultIdentity<ApplicationUser>(options =>
    {
        options.Stores.SchemaVersion = IdentitySchemaVersions.Version1; //matches the InitialCreate migration
        options.SignIn.RequireConfirmedAccount = false;
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireNonAlphanumeric = false;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.SlidingExpiration = true;

    // The API gets a plain 401/403 instead of being redirected to the login page.
    options.Events.OnRedirectToLogin = context => ApiAwareRedirect(context, StatusCodes.Status401Unauthorized);
    options.Events.OnRedirectToAccessDenied = context => ApiAwareRedirect(context, StatusCodes.Status403Forbidden);
});

// Locked or demoted users get kicked out within a minute, not when their cookie runs out.
builder.Services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.FromMinutes(1));

// Our services
builder.Services.AddArrowOutServices(builder.Configuration);
builder.Services.AddScoped<IThemeResolver, ThemeResolver>();

// MVC, API and security
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN"; //used by fetch() calls from the game and editor scripts
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

builder.Services
    .AddControllersWithViews(options =>
    {
        // CSRF check on every POST/PUT/DELETE, the API included (it uses the login cookie too).
        options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
        options.Filters.Add<DomainExceptionFilter>();
    })
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddRazorPages();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

builder.Services.Configure<RouteOptions>(options => options.LowercaseUrls = true);

builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(365);
    options.IncludeSubDomains = true;
});

var app = builder.Build();

// Request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseHsts();
}

app.UseExceptionHandler("/error/500");

// Nice HTML error pages for the browser. The API keeps plain status codes and ProblemDetails.
app.UseWhen(
    context => !context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase),
    branch => branch.UseStatusCodePagesWithReExecute("/error/{0}"));

app.UseHttpsRedirection();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

await app.InitialiseDatabaseAsync();
await app.RunAsync();

static Task ApiAwareRedirect(Microsoft.AspNetCore.Authentication.RedirectContext<Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationOptions> context, int statusCode)
{
    if (context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.StatusCode = statusCode;
        return Task.CompletedTask;
    }

    context.Response.Redirect(context.RedirectUri);
    return Task.CompletedTask;
}

// So tests can use WebApplicationFactory<Program>.
public partial class Program;
