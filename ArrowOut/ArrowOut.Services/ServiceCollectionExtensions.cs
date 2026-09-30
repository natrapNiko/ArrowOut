using ArrowOut.Game.Solving;
using ArrowOut.Services.Analytics;
using ArrowOut.Services.Challenges;
using ArrowOut.Services.Gameplay;
using ArrowOut.Services.Leaderboard;
using ArrowOut.Services.Levels;
using ArrowOut.Services.Storage;
using ArrowOut.Services.Themes;
using ArrowOut.Services.Users;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ArrowOut.Services;

public static class ServiceCollectionExtensions
{
    // Registers all our services.
    public static IServiceCollection AddArrowOutServices(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddMemoryCache();
        services.AddSingleton(TimeProvider.System);

        // Engine stuff has no state, so one instance is enough.
        services.AddSingleton<IPuzzleSolver, GreedySolver>();
        services.AddSingleton<IHintProvider, UnblockingHintProvider>();
        services.AddSingleton<ILevelDesignValidator, LevelDesignValidator>();

        // One per request, so they share the request's DbContext.
        services.AddScoped<ILevelAccessGuard, LevelAccessGuard>();
        services.AddScoped<ILevelService, LevelService>();
        services.AddScoped<ILevelAdminService, LevelAdminService>();
        services.AddScoped<IGameService, GameService>();
        services.AddScoped<IChallengeService, ChallengeService>();
        services.AddScoped<ILeaderboardService, LeaderboardService>();
        services.AddScoped<IUserAdminService, UserAdminService>();
        services.AddScoped<IPlayerSettingsService, PlayerSettingsService>();

        // Files
        services.AddOptions<FileStorageOptions>()
            .Bind(configuration.GetSection(FileStorageOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IFileStorage, LocalFileStorage>();
        services.AddSingleton<IThemeService, ThemeService>();

        AddAnalytics(services, configuration);
        return services;
    }

    private static void AddAnalytics(IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(AnalyticsOptions.SectionName);
        services.AddOptions<AnalyticsOptions>()
            .Bind(section)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var analyticsOptions = section.Get<AnalyticsOptions>() ?? new AnalyticsOptions();

        services.AddSingleton(sp => new AnalyticsQueue(sp.GetRequiredService<IOptions<AnalyticsOptions>>().Value.QueueCapacity));
        services.AddSingleton<IAnalyticsTracker, ChannelAnalyticsTracker>();
        services.AddHostedService<AnalyticsDispatcher>();

        if (analyticsOptions.IsConfigured)
        {
            services
                .AddHttpClient<IAnalyticsClient, PostHogAnalyticsClient>(client =>
                {
                    // Timeouts and retries are handled by the resilience handler below.
                    client.BaseAddress = new Uri(analyticsOptions.Host.TrimEnd('/') + "/");
                })
                .AddStandardResilienceHandler();
        }
        else
        {
            services.AddSingleton<IAnalyticsClient, NullAnalyticsClient>();
        }
    }
}
