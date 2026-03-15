using SlackActionTracker.Domain;
using SlackActionTracker.Parsers;
using System.Text.RegularExpressions;

namespace SlackActionTracker.Services;

public class SlackEventProcessor
{
    private readonly IEnumerable<IActionParser> _parsers;
    private readonly ActionItemService _actionService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _botToken;

    public SlackEventProcessor(
        IEnumerable<IActionParser> parsers,
        ActionItemService actionService,
        IHttpClientFactory httpClientFactory)
    {
        _parsers = parsers;
        _actionService = actionService;
        _httpClientFactory = httpClientFactory;
        _botToken = Environment.GetEnvironmentVariable("SLACK_BOT_TOKEN") ?? "";
    }

    public async Task ProcessMessage(
        string user, string channel, string text,
        string eventId, string messageTs, string? threadTs = null)
    {
        foreach (var parser in _parsers)
        {
            var result = parser.Parse(text);
            if (result.HasValue)
            {
                var createdItem = await _actionService.TryCreateFromMessage(
                    user, channel,
                    result.Value.Text, text,
                    result.Value.Type, eventId, messageTs,
                    result.Value.DueDate);

                if (createdItem != null)
                {
                    // Only send ephemeral to the creator — no DM spam
                    await SendEphemeralConfirmation(user, channel, createdItem);

                    // If assigned to someone else, DM the assignee
                    if (!string.IsNullOrEmpty(createdItem.AssigneeId) && createdItem.AssigneeId != user)
                        await SendAssigneeDm(createdItem);
                }
                break;
            }
        }
    }

    // Sends a private ephemeral in-channel confirmation to the creator only
    private async Task SendEphemeralConfirmation(string userId, string channelId, ActionItem item)
    {
        var client = _httpClientFactory.CreateClient("Slack");

        string displayText = item.FullMessageText;
        if (!string.IsNullOrEmpty(item.DueDateText))
        {
            displayText = Regex.Replace(
                displayText,
                Regex.Escape(item.DueDateText),
                $"*{item.DueDateText}*",
                RegexOptions.IgnoreCase);
        }

        var blocks = new object[]
        {
            new {
                type = "section",
                text = new { type = "mrkdwn", text = $":white_check_mark: *Action item tracked:*\n{displayText}" }
            },
            new {
                type = "context",
                elements = new object[] {
                    new { type = "mrkdwn", text = $"{UI.SlackMessageBuilder.PriorityEmoji(item.Priority)} {item.Priority} priority · {item.Type}" }
                }
            }
        };

        var payload = new { channel = channelId, user = userId, text = "Action item tracked!", blocks };
        await client.PostAsJsonAsync("https://slack.com/api/chat.postEphemeral", payload);
    }

    // DMs the assignee when someone assigns an action to them
    public async Task SendAssigneeDm(ActionItem item)
    {
        var client = _httpClientFactory.CreateClient("Slack");

        var blocks = new object[]
        {
            new {
                type = "section",
                text = new { type = "mrkdwn", text = $":bell: *You've been assigned an action item:*\n*{item.Text}*" }
            },
            new {
                type = "context",
                elements = new object[] {
                    new { type = "mrkdwn", text =
                        $"Assigned by <@{item.UserId}> · " +
                        $"{UI.SlackMessageBuilder.PriorityEmoji(item.Priority)} {item.Priority} priority" +
                        (item.DueDate.HasValue ? $" · Due: {item.DueDate:dd MMM yyyy}" : "") }
                }
            },
            new {
                type = "actions",
                elements = new object[] {
                    new {
                        type = "button",
                        text = new { type = "plain_text", text = ":heavy_check_mark: Mark Complete" },
                        style = "primary",
                        value = item.Id.ToString(),
                        action_id = "home_complete"
                    }
                }
            }
        };

        var payload = new { channel = item.AssigneeId, text = "You have a new action item!", blocks };
        await client.PostAsJsonAsync("https://slack.com/api/chat.postMessage", payload);
    }
}
