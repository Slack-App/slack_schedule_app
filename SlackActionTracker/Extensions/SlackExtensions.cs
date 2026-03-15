using SlackActionTracker.Parsers;
using SlackActionTracker.Services;

public static class ServiceExtensions
{
    public static IServiceCollection AddSlackServices(this IServiceCollection services)
    {
        // Parsers
        services.AddSingleton<IActionParser, CommitmentParser>();
        services.AddSingleton<IActionParser, RequestParser>();
        services.AddSingleton<IActionParser, QuestionParser>();
        // services.AddSingleton<IActionParser, DeadlineParser>(); // enable when ready

        // Core services
        services.AddScoped<ActionItemService>();
        services.AddScoped<SlackEventProcessor>();
        services.AddScoped<SlackHomeService>();
        services.AddScoped<OnboardingService>();

        // Background services
        services.AddHostedService<DailyDigestService>();
        services.AddHostedService<WeeklyDigestService>();

        // Named HttpClient with Slack auth header pre-configured
        services.AddHttpClient("Slack", (sp, client) =>
        {
            var token = Environment.GetEnvironmentVariable("SLACK_BOT_TOKEN") ?? "";
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        });

        return services;
    }
}
