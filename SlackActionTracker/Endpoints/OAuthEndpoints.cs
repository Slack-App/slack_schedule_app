using SlackActionTracker.Services;
using Microsoft.EntityFrameworkCore;
using SlackActionTracker.Data;

namespace SlackActionTracker.Endpoints;

public static class OAuthEndpoints
{
    public static void MapOAuthRoutes(this IEndpointRouteBuilder app)
    {
        app.MapGet("/slack/oauth/callback", HandleOAuthCallback);

        // TEMPORARY - remove after running once
        app.MapGet("/admin/migrate", async (AppDbContext db) =>
        {
            await db.Database.ExecuteSqlRawAsync(@"
                ALTER TABLE ""ActionItems"" ADD COLUMN IF NOT EXISTS ""AssigneeId"" text;
                ALTER TABLE ""ActionItems"" ADD COLUMN IF NOT EXISTS ""Priority"" integer NOT NULL DEFAULT 1;
                ALTER TABLE ""ActionItems"" ADD COLUMN IF NOT EXISTS ""DueDate"" timestamp with time zone;
            ");
            await db.Database.ExecuteSqlRawAsync(@"
                INSERT INTO ""__EFMigrationsHistory"" (""MigrationId"", ""ProductVersion"")
                VALUES ('20260315000000_AddPriorityAssigneeAndDueDate', '10.0.3')
                ON CONFLICT DO NOTHING;
            ");
            return Results.Ok("Migration applied successfully.");
        });
    }

    private static async Task<IResult> HandleOAuthCallback(
        HttpRequest request,
        IHttpClientFactory httpClientFactory,
        OnboardingService onboarding,
        ILogger<Program> logger)
    {
        var code = request.Query["code"].ToString();
        var error = request.Query["error"].ToString();

        if (!string.IsNullOrEmpty(error))
        {
            logger.LogWarning("[OAuth] Install declined: {Error}", error);
            return Results.Redirect("https://slackscheduleapp-staging.up.railway.app/install-cancelled");
        }

        if (string.IsNullOrEmpty(code))
        {
            logger.LogError("[OAuth] No code in callback");
            return Results.BadRequest("Missing code parameter.");
        }

        var clientId     = Environment.GetEnvironmentVariable("SLACK_CLIENT_ID") ?? "";
        var clientSecret = Environment.GetEnvironmentVariable("SLACK_CLIENT_SECRET") ?? "";

        var client = httpClientFactory.CreateClient();
        var response = await client.PostAsync("https://slack.com/api/oauth.v2.access",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["code"]          = code,
                ["client_id"]     = clientId,
                ["client_secret"] = clientSecret,
                ["redirect_uri"]  = "https://slackscheduleapp-staging.up.railway.app/slack/oauth/callback"
            }));

        var json = await response.Content.ReadFromJsonAsync<OAuthResponse>();

        if (json == null || !json.ok)
        {
            logger.LogError("[OAuth] Token exchange failed: {Error}", json?.error ?? "null response");
            return Results.Redirect("https://slackscheduleapp-staging.up.railway.app/install-error");
        }

        logger.LogInformation("[OAuth] New install: team={TeamId} teamName={TeamName}",
            json.team?.id, json.team?.name);

        if (!string.IsNullOrEmpty(json.authed_user?.id))
            await onboarding.OnboardIfNewAsync(json.authed_user.id);

        return Results.Redirect("https://slackscheduleapp-staging.up.railway.app/install-success");
    }

    private record OAuthResponse(
        bool ok,
        string? error,
        string? access_token,
        OAuthTeam? team,
        OAuthUser? authed_user);

    private record OAuthTeam(string id, string name);
    private record OAuthUser(string id);
}
