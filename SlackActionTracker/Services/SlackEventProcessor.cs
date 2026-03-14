using SlackActionTracker.Domain;
using SlackActionTracker.Parsers;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace SlackActionTracker.Services;

public class SlackEventProcessor
{
    private readonly IEnumerable<IActionParser> _parsers;
    private readonly ActionItemService _actionService;
    private readonly string _botToken;

    public SlackEventProcessor(IEnumerable<IActionParser> parsers, ActionItemService actionService)
    {
        _parsers = parsers;
        _actionService = actionService;
        _botToken = Environment.GetEnvironmentVariable("SLACK_BOT_TOKEN") ?? "";
    }

    public async Task ProcessMessage(string user, string channel, string text, string eventId, string messageTs, string? threadTs = null)
    {
        foreach (var parser in _parsers)
        {
            var result = parser.Parse(text);
            if (result.HasValue)
            {
                var createdItem = await _actionService.TryCreateFromMessage(user, channel, result.Value.Text, text, result.Value.Type, eventId, messageTs, result.Value.DueDate);

                if (createdItem != null)
                {
                    await SendConfirmationNotification(user, channel, createdItem);
                }
                break;
            }
        }
    }

    private async Task SendConfirmationNotification(string userId, string channelId, ActionItem item)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _botToken);

        string displayText = item.FullMessageText;

        if (!string.IsNullOrEmpty(item.DueDateText))
        {
            displayText = Regex.Replace(
                displayText,
                Regex.Escape(item.DueDateText),
                $"*{item.DueDateText}*",
                RegexOptions.IgnoreCase);
        }

        var notificationBlocks = new object[]
        {
            new {
                type = "section",
                text = new { type = "mrkdwn", text = $":white_check_mark: *Action Item Tracked:*\n{displayText}" }
            },
            new {
                type = "context",
                elements = new object[] { new { type = "mrkdwn", text = $"Type: {item.Type} | Status: *{item.Status}*" } }
            }
        };

        var ephemeralPayload = new
        {
            channel = channelId,
            user = userId,
            text = "New item tracked!",
            blocks = notificationBlocks
        };
        await client.PostAsJsonAsync("https://slack.com/api/chat.postEphemeral", ephemeralPayload);

        var dmPayload = new
        {
            channel = userId,
            text = "I've added a new item to your list!",
            blocks = notificationBlocks
        };
        var dmResponse = await client.PostAsJsonAsync("https://slack.com/api/chat.postMessage", dmPayload);

        var result = await dmResponse.Content.ReadAsStringAsync();
        Console.WriteLine($"DM Notification Result: {result}");
    }
}
