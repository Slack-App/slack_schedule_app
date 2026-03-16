using SlackActionTracker.Services;
using SlackActionTracker.UI;
using SlackActionTracker.DTOs;
using SlackActionTracker.Domain;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace SlackActionTracker.Endpoints;

public static class SlackEndpoints
{
    public static void MapSlackRoutes(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/slack");

        group.MapPost("/interactions", HandleInteractions);
        group.MapPost("/actions", HandleActionsCommand);    // /actions slash command (list)
        group.MapPost("/action", HandleActionCommand);      // /action slash command  (create)
        group.MapPost("/events", HandleEvents);
    }

    // ─── Interactions (buttons, modals, shortcuts) ──────────────────────────────

    private static async Task<IResult> HandleInteractions(
        HttpRequest request,
        ActionItemService service,
        SlackHomeService homeService,
        SlackEventProcessor processor,
        IHttpClientFactory httpClientFactory)
    {
        var form = await request.ReadFormAsync();
        var payloadJson = form["payload"].ToString();
        var payload = JsonSerializer.Deserialize<SlackInteractionPayload>(payloadJson);
        if (payload == null) return Results.Ok();

        var userId = payload.user?.id ?? "";

        // ── Modal submission ──────────────────────────────────────────────────
        if (payload.type == "view_submission" && payload.view?.callback_id == "create_action_modal")
        {
            await HandleModalSubmissionAsync(payload, service, processor, httpClientFactory);
            return Results.Ok();
        }

        // ── Message shortcut ─────────────────────────────────────────────────
        if (payload.type == "message_action" && payload.callback_id == "track_message_action")
        {
            await OpenTrackMessageModalAsync(payload, httpClientFactory);
            return Results.Ok();
        }

        var action = payload?.actions?[0];
        if (action == null) return Results.Ok();

        // ── Filter / sort on home tab ─────────────────────────────────────────
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
// ── Open home button ──────────────────────────────────────────────
if (action.action_id == "open_home")
{
    await homeService.PublishHomeAsync(userId);
    return Results.Ok();
}
        // ── Complete / remove buttons ─────────────────────────────────────────
        if (Guid.TryParse(action.value, out Guid itemId))
        {
            var newStatus = (action.action_id == "home_complete" || action.action_id == "complete_item")
                ? "completed" : "removed";

            var result = await service.UpdateStatusAsync(itemId, newStatus);

            if (action.action_id.StartsWith("home_"))
            {
                await homeService.PublishHomeAsync(userId);
            }
            else if (!string.IsNullOrEmpty(payload?.response_url))
            {
                var client = httpClientFactory.CreateClient("Slack");
                await client.PostAsJsonAsync(payload.response_url, new
                {
                    text = result.Message,
                    replace_original = true,
                    response_type = "ephemeral"
                });
            }
        }

        return Results.Ok();
    }

    private static async Task HandleModalSubmissionAsync(
        SlackInteractionPayload payload,
        ActionItemService service,
        SlackEventProcessor processor,
        IHttpClientFactory httpClientFactory)
    {
        var values = payload.view?.state?.values;
        if (values == null) return;

        var userId = payload.user?.id ?? "";
        var channelId = payload.view?.private_metadata ?? "";

        // Extract modal values
        var text = GetModalValue(values, "action_text_block", "action_text_input");
        var typeStr = GetModalValue(values, "action_type_block", "action_type_select");
        var priorityStr = GetModalValue(values, "action_priority_block", "action_priority_select");
        var assigneeId = GetModalValue(values, "action_assignee_block", "action_assignee_select");
        var dueDateStr = GetModalValue(values, "action_duedate_block", "action_duedate_picker");

        if (string.IsNullOrWhiteSpace(text)) return;

        Enum.TryParse<ActionItemType>(typeStr, true, out var type);
        Enum.TryParse<ActionPriority>(priorityStr, true, out var priority);
        DateTime? dueDate = DateTime.TryParse(dueDateStr, out var d) ? d.ToUniversalTime() : null;

        var item = await service.CreateFromModalAsync(
            userId, channelId, text, type, priority,
            assigneeId, dueDate, dueDateStr);

        // DM assignee if it's someone else
        if (!string.IsNullOrEmpty(item.AssigneeId) && item.AssigneeId != userId)
            await processor.SendAssigneeDm(item);
    }

    private static async Task OpenTrackMessageModalAsync(
        SlackInteractionPayload payload,
        IHttpClientFactory httpClientFactory)
    {
        var messageText = payload.message?.text ?? "";
        var channelId = payload.channel?.id ?? "";
        var triggerId = payload.trigger_id ?? "";

        var modal = new
        {
            type = "modal",
            callback_id = "create_action_modal",
            private_metadata = channelId,
            title = new { type = "plain_text", text = "Track as Action Item" },
            submit = new { type = "plain_text", text = "Track" },
            close = new { type = "plain_text", text = "Cancel" },
            blocks = BuildModalBlocks(messageText)
        };

        var client = httpClientFactory.CreateClient("Slack");
        await client.PostAsJsonAsync("https://slack.com/api/views.open",
            new { trigger_id = triggerId, view = modal });
    }

    // ─── /actions slash command (list current items or channel board) ────────────

    private static async Task<IResult> HandleActionsCommand(
        HttpRequest request,
        ActionItemService service)
    {
        var form = await request.ReadFormAsync();
        var userId = form["user_id"].ToString();
        var channelId = form["channel_id"].ToString();
        var text = form["text"].ToString().Trim().ToLower();

        // /actions list — channel board
        if (text == "list" || text == "board")
        {
            var channelItems = await service.GetChannelActiveItemsAsync(channelId);
            if (!channelItems.Any())
                return Results.Json(new { text = ":white_check_mark: No open action items in this channel.", response_type = "in_channel" });

            return Results.Json(SlackMessageBuilder.BuildChannelBoard(channelItems));
        }

        // /actions complete <id> or remove <id>
        if (text.StartsWith("complete") || text.StartsWith("remove"))
        {
            var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2 && Guid.TryParse(parts[1], out Guid id))
            {
                var status = text.StartsWith("complete") ? "completed" : "removed";
                var result = await service.UpdateStatusAsync(id, status);
                return Results.Json(new { text = result.Message, response_type = "ephemeral" });
            }
            return Results.Json(new { text = ":warning: Usage: `/actions complete <id>` or `/actions remove <id>`", response_type = "ephemeral" });
        }

        // Default — show personal list
        var items = await service.GetActiveItemsAsync(userId);
        if (!items.Any())
            return Results.Json(new { text = ":white_check_mark: You have no open action items. Great work!", response_type = "ephemeral" });

        return Results.Json(SlackMessageBuilder.BuildActiveItemsList(items));
    }

    // ─── /action slash command (create an item) ───────────────────────────────

    private static async Task<IResult> HandleActionCommand(
        HttpRequest request,
        ActionItemService service,
        IHttpClientFactory httpClientFactory)
    {
        var form = await request.ReadFormAsync();
        var userId = form["user_id"].ToString();
        var channelId = form["channel_id"].ToString();
        var triggerId = form["trigger_id"].ToString();
        var text = form["text"].ToString().Trim();

        // If text is provided, create directly
        if (!string.IsNullOrEmpty(text))
        {
            var item = await service.CreateFromModalAsync(
                userId, channelId, text,
                ActionItemType.Commitment, ActionPriority.Medium,
                null, null, null);

            return Results.Json(new
            {
                response_type = "ephemeral",
                text = $":white_check_mark: Action item tracked: *{item.Text}*\n_Use `/actions` to manage your items._"
            });
        }

        // No text — open the full creation modal
        var modal = new
        {
            type = "modal",
            callback_id = "create_action_modal",
            private_metadata = channelId,
            title = new { type = "plain_text", text = "New Action Item" },
            submit = new { type = "plain_text", text = "Create" },
            close = new { type = "plain_text", text = "Cancel" },
            blocks = BuildModalBlocks()
        };

        var client = httpClientFactory.CreateClient("Slack");
        await client.PostAsJsonAsync("https://slack.com/api/views.open",
            new { trigger_id = triggerId, view = modal });

        return Results.Ok();
    }

    // ─── Events ───────────────────────────────────────────────────────────────

    private static async Task<IResult> HandleEvents(
        HttpRequest request,
        SlackEventProcessor processor,
        SlackHomeService homeService,
        OnboardingService onboarding)
    {
        using var reader = new StreamReader(request.Body);
        var body = await reader.ReadToEndAsync();
        var json = JsonDocument.Parse(body);

        // URL verification challenge
        if (json.RootElement.TryGetProperty("type", out var typeProp) &&
            typeProp.GetString() == "url_verification")
        {
            return Results.Json(new { challenge = json.RootElement.GetProperty("challenge").GetString() });
        }

        if (!json.RootElement.TryGetProperty("event", out var eventProp))
            return Results.Ok();

        // Ignore bot messages
        if (eventProp.TryGetProperty("bot_id", out _)) return Results.Ok();

        var eventType = eventProp.GetProperty("type").GetString();

        switch (eventType)
        {
            case "app_home_opened":
                var userId = eventProp.GetProperty("user").GetString()!;
                await onboarding.OnboardIfNewAsync(userId);
                await homeService.PublishHomeAsync(userId);
                break;

            case "message" when !eventProp.TryGetProperty("subtype", out _):
                await processor.ProcessMessage(
                    eventProp.GetProperty("user").GetString()!,
                    eventProp.GetProperty("channel").GetString()!,
                    eventProp.GetProperty("text").GetString()!,
                    json.RootElement.GetProperty("event_id").GetString()!,
                    eventProp.GetProperty("ts").GetString()!
                );
                break;
        }

        return Results.Ok();
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static object[] BuildModalBlocks(string? prefillText = null)
    {
        return new object[]
        {
            new {
                type = "input",
                block_id = "action_text_block",
                label = new { type = "plain_text", text = "What needs to be done?" },
                element = new {
                    type = "plain_text_input",
                    action_id = "action_text_input",
                    placeholder = new { type = "plain_text", text = "e.g. Review the Q2 report" },
                    initial_value = prefillText ?? ""
                }
            },
            new {
                type = "input",
                block_id = "action_assignee_block",
                label = new { type = "plain_text", text = "Assign to" },
                optional = true,
                element = new {
                    type = "users_select",
                    action_id = "action_assignee_select",
                    placeholder = new { type = "plain_text", text = "Yourself by default" }
                }
            },
            new {
                type = "input",
                block_id = "action_duedate_block",
                label = new { type = "plain_text", text = "Due date" },
                optional = true,
                element = new {
                    type = "datepicker",
                    action_id = "action_duedate_picker",
                    placeholder = new { type = "plain_text", text = "Select a date" }
                }
            },
            new {
                type = "input",
                block_id = "action_priority_block",
                label = new { type = "plain_text", text = "Priority" },
                element = new {
                    type = "static_select",
                    action_id = "action_priority_select",
                    initial_option = new { text = new { type = "plain_text", text = "🟡 Medium" }, value = "Medium" },
                    options = new object[] {
                        new { text = new { type = "plain_text", text = "🔴 High" }, value = "High" },
                        new { text = new { type = "plain_text", text = "🟡 Medium" }, value = "Medium" },
                        new { text = new { type = "plain_text", text = "🔵 Low" }, value = "Low" }
                    }
                }
            },
            new {
                type = "input",
                block_id = "action_type_block",
                label = new { type = "plain_text", text = "Type" },
                element = new {
                    type = "static_select",
                    action_id = "action_type_select",
                    initial_option = new { text = new { type = "plain_text", text = "✏️ Commitment" }, value = "Commitment" },
                    options = new object[] {
                        new { text = new { type = "plain_text", text = "✏️ Commitment" }, value = "Commitment" },
                        new { text = new { type = "plain_text", text = "📨 Request" }, value = "Request" },
                        new { text = new { type = "plain_text", text = "❓ Question" }, value = "Question" }
                    }
                }
            }
        };
    }

    private static string? GetModalValue(
        Dictionary<string, Dictionary<string, JsonElement>>? values,
        string blockId, string actionId)
    {
        if (values == null) return null;
        if (!values.TryGetValue(blockId, out var block)) return null;
        if (!block.TryGetValue(actionId, out var element)) return null;

        // plain_text_input
        if (element.TryGetProperty("value", out var val))
            return val.GetString();

        // static_select / users_select
        if (element.TryGetProperty("selected_option", out var opt))
            return opt.GetProperty("value").GetString();

        if (element.TryGetProperty("selected_user", out var user))
            return user.GetString();

        // datepicker
        if (element.TryGetProperty("selected_date", out var date))
            return date.GetString();

        return null;
    }
}
