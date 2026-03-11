using SlackActionTracker.Endpoints;
using SlackActionTracker.Middleware;
using SlackActionTracker.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddSlackServices();

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<SlackSignatureMiddleware>();

app.MapSlackRoutes();

app.Run();