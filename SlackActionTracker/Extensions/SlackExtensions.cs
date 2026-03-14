using SlackActionTracker.Parsers;
using SlackActionTracker.Services;

public static class ServiceExtensions
{
    public static IServiceCollection AddSlackServices(this IServiceCollection services)
    {
        services.AddSingleton<IActionParser, CommitmentParser>();
        services.AddSingleton<IActionParser, RequestParser>();
        // services.AddSingleton<IActionParser, DeadlineParser>();
        services.AddSingleton<IActionParser, QuestionParser>();

        services.AddScoped<ActionItemService>();
        services.AddScoped<SlackEventProcessor>();

        services.AddScoped<SlackHomeService>();
        services.AddHttpClient();

        return services;
    }
}