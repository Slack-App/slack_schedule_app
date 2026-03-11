using SlackActionTracker.Services;
using SlackActionTracker.UI;
using SlackActionTracker.DTOs;
using Microsoft.EntityFrameworkCore;

namespace SlackActionTracker.Endpoints;

public static class SlackEndpoints
{
    public static void MapSlackRoutes(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/slack");

        group.MapPost("/interactions", HandleInteractions);
        group.MapPost("/actions", HandleActions);
        group.MapPost("/events", HandleEvents);
    }


    private static async Task<IResult> HandleInteractions(
        HttpRequest request,
        ActionItemService service,
        IHttpClientFactory httpClientFactory)
    {
        var form = await request.ReadFormAsync();
        var payloadJson = form["payload"].ToString();
        var payload = System.Text.Json.JsonSerializer.Deserialize<SlackInteractionPayload>(payloadJson);

        var action = payload?.actions?[0];
        if (action == null) return Results.Ok();

        var userId = payload.user.id;
        var responseUrl = payload.response_url;
        var homeService = request.HttpContext.RequestServices.GetRequiredService<SlackHomeService>();

        if (action.action_id.StartsWith("filter_"))
        {
            await homeService.PublishHomeAsync(userId, filter: action.value);
            return Results.Ok();
        }

        if (action.action_id.StartsWith("sort_"))
        {
            await homeService.PublishHomeAsync(userId, sort: action.value);
            return Results.Ok();
        }

        if (Guid.TryParse(action.value, out Guid itemId))
        {
            var newStatus = (action.action_id == "home_complete" || action.action_id == "complete_item")
                            ? "completed" : "removed";

            var result = await service.UpdateStatusAsync(itemId, newStatus);

            if (action.action_id.StartsWith("home_"))
            {
                await homeService.PublishHomeAsync(userId);
            }
            else if (!string.IsNullOrEmpty(responseUrl))
            {
                var client = httpClientFactory.CreateClient();
                await client.PostAsJsonAsync(responseUrl, new
                {
                    text = result.Message,
                    replace_original = true,
                    response_type = "ephemeral"
                });
            }
        }

        return Results.Ok();
    }

    private static async Task<IResult> HandleActions(
        HttpRequest request,
        ActionItemService service)
    {
        var form = await request.ReadFormAsync();
        var userId = form["user_id"].ToString();
        var text = form["text"].ToString().Trim().ToLower();


        if (!string.IsNullOrEmpty(text) && (text.StartsWith("complete") || text.StartsWith("remove")))
        {
            var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2 && Guid.TryParse(parts[1], out Guid id))
            {
                var status = text.StartsWith("complete") ? "completed" : "removed";

                var result = await service.UpdateStatusAsync(id, status);

                return Results.Json(new
                {
                    text = result.Message,
                    response_type = "ephemeral"
                });
            }

            return Results.Json(new { text = ":warning: Usage: `/actions [complete/remove] [id]`", response_type = "ephemeral" });
        }

        var items = await service.GetActiveItemsAsync(userId);
        if (!items.Any())
        {
            return Results.Json(new { text = ":white_check_mark: You're all caught up!", response_type = "ephemeral" });
        }

        var response = SlackMessageBuilder.BuildActiveItemsList(items);
        return Results.Json(response);
    }

    private static async Task<IResult> HandleEvents(
        HttpRequest request,
        SlackEventProcessor processor)
    {
        using var reader = new StreamReader(request.Body);
        var body = await reader.ReadToEndAsync();
        var json = System.Text.Json.JsonDocument.Parse(body);

        if (json.RootElement.TryGetProperty("type", out var typeProp) &&
            typeProp.GetString() == "url_verification")
        {
            return Results.Json(new { challenge = json.RootElement.GetProperty("challenge").GetString() });
        }

        if (json.RootElement.TryGetProperty("event", out var eventProp))
        {
            if (eventProp.TryGetProperty("bot_id", out _)) return Results.Ok();

            var eventType = eventProp.GetProperty("type").GetString();
            if (eventType == "app_home_opened")
            {
                var userId = eventProp.GetProperty("user").GetString()!;
                var homeService = request.HttpContext.RequestServices.GetRequiredService<SlackHomeService>();
                await homeService.PublishHomeAsync(userId);
                return Results.Ok();
            }
            if (eventType == "message" && !eventProp.TryGetProperty("subtype", out _))
            {
                await processor.ProcessMessage(
                    eventProp.GetProperty("user").GetString()!,
                    eventProp.GetProperty("channel").GetString()!,
                    eventProp.GetProperty("text").GetString()!,
                    json.RootElement.GetProperty("event_id").GetString()!,
                    eventProp.GetProperty("ts").GetString()!
                );
            }
        }

        return Results.Ok();
    }
}