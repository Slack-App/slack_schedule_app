using SlackActionTracker.Endpoints;
using SlackActionTracker.Middleware;
using SlackActionTracker.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var host = Environment.GetEnvironmentVariable("PGHOST");
var port = Environment.GetEnvironmentVariable("PGPORT");
var db   = Environment.GetEnvironmentVariable("POSTGRES_DB");
var user = Environment.GetEnvironmentVariable("POSTGRES_USER");
var pass = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql($"Host={host};Port={port};Database={db};Username={user};Password={pass}"));

builder.Services.AddSlackServices();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbCtx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await dbCtx.Database.MigrateAsync();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();

// OAuth callback comes from a browser — it has no Slack signature header.
// Only apply signature verification to the other /slack/* routes.
app.UseWhen(
    ctx => ctx.Request.Path.StartsWithSegments("/slack") &&
           !ctx.Request.Path.StartsWithSegments("/slack/oauth"),
    appBuilder => appBuilder.UseMiddleware<SlackSignatureMiddleware>()
);

app.MapOAuthRoutes();   // GET /slack/oauth/callback
app.MapSlackRoutes();   // POST /slack/events, /slack/interactions, /slack/actions, /slack/action

app.Run();
