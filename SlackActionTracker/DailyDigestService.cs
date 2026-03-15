using SlackActionTracker.Domain;
using SlackActionTracker.UI;

namespace SlackActionTracker.Services;

public class DailyDigestService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<DailyDigestService> _logger;
    private readonly string _botToken;

    public DailyDigestService(
        IServiceScopeFactory scopeFactory,
        IHttpClientFactory httpClientFactory,
        ILogger<DailyDigestService> logger)
    {
        _scopeFactory = scopeFactory;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _botToken = Environment.GetEnvironmentVariable("SLACK_BOT_TOKEN") ?? "";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[DailyDigest] Service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.UtcNow;
            // Run every day at 08:00 UTC
            var nextRun = now.Hour < 8
                ? now.Date.AddHours(8)
                : now.Date.AddDays(1).AddHours(8);

            var delay = nextRun - now;
            _logger.LogInformation("[DailyDigest] Next digest at {NextRun} UTC (in {Delay})", nextRun, delay);

            try
            {
                await Task.Delay(delay, stoppingToken);
                await SendDailyDigestsAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task SendDailyDigestsAsync(CancellationToken ct)
    {
        _logger.LogInformation("[DailyDigest] Sending daily digests…");

        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ActionItemService>();

        var userIds = await service.GetUsersWithActiveItemsAsync();

        foreach (var userId in userIds)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                var items = await service.GetActiveItemsAsync(userId);
                if (!items.Any()) continue;

                await SendDigestDmAsync(userId, items);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[DailyDigest] Failed to send digest to {UserId}", userId);
            }
        }

        _logger.LogInformation("[DailyDigest] Done. Sent to {Count} users.", userIds.Count);
    }

    private async Task SendDigestDmAsync(string userId, List<ActionItem> items)
    {
        var overdue = items.Where(i => i.IsOverdue).ToList();
        var upcoming = items.Where(i => !i.IsOverdue && i.DueDate.HasValue).OrderBy(i => i.DueDate).Take(3).ToList();
        var rest = items.Where(i => !i.IsOverdue && !i.DueDate.HasValue).Take(5).ToList();

        var blocks = new List<object>
        {
            new {
                type = "header",
                text = new { type = "plain_text", text = $":sunrise: Good morning! Your action items for today", emoji = true }
            },
            new {
                type = "section",
                text = new { type = "mrkdwn", text = $"You have *{items.Count} active item{(items.Count == 1 ? "" : "s")}*." +
                    (overdue.Any() ? $" 🔴 *{overdue.Count} overdue!*" : "") }
            },
            new { type = "divider" }
        };

        // Overdue section
        if (overdue.Any())
        {
            blocks.Add(new { type = "section", text = new { type = "mrkdwn", text = "*🔴 Overdue:*" } });
            foreach (var item in overdue.Take(5))
                blocks.Add(BuildDigestItem(item));
            blocks.Add(new { type = "divider" });
        }

        // Due soon section
        if (upcoming.Any())
        {
            blocks.Add(new { type = "section", text = new { type = "mrkdwn", text = "*:alarm_clock: Due soon:*" } });
            foreach (var item in upcoming)
                blocks.Add(BuildDigestItem(item));
            blocks.Add(new { type = "divider" });
        }

        // Other items
        if (rest.Any())
        {
            blocks.Add(new { type = "section", text = new { type = "mrkdwn", text = "*:clipboard: Other open items:*" } });
            foreach (var item in rest)
                blocks.Add(BuildDigestItem(item));
        }

        blocks.Add(new {
            type = "actions",
            elements = new object[] {
                new {
                    type = "button",
                    text = new { type = "plain_text", text = "Open My Dashboard" },
                    action_id = "open_home",
                    value = "open_home"
                }
            }
        });

        var client = _httpClientFactory.CreateClient("Slack");
        var payload = new { channel = userId, text = "Your daily action item digest", blocks };
        await client.PostAsJsonAsync("https://slack.com/api/chat.postMessage", payload);
    }

    private static object BuildDigestItem(ActionItem item)
    {
        var due = item.DueDate.HasValue ? $" · Due *{item.DueDate:dd MMM}*" : "";
        return new
        {
            type = "section",
            text = new
            {
                type = "mrkdwn",
                text = $"{SlackMessageBuilder.GetEmoji(item.Type)} {SlackMessageBuilder.PriorityEmoji(item.Priority)} *{item.Text}*{due}"
            },
            accessory = new
            {
                type = "button",
                text = new { type = "plain_text", text = "✅ Done" },
                style = "primary",
                value = item.Id.ToString(),
                action_id = "home_complete"
            }
        };
    }
}
