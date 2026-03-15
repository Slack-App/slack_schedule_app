namespace SlackActionTracker.Services;

public class WeeklyDigestService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WeeklyDigestService> _logger;
    private readonly string _botToken;

    public WeeklyDigestService(
        IServiceScopeFactory scopeFactory,
        IHttpClientFactory httpClientFactory,
        ILogger<WeeklyDigestService> logger)
    {
        _scopeFactory = scopeFactory;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _botToken = Environment.GetEnvironmentVariable("SLACK_BOT_TOKEN") ?? "";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[WeeklyDigest] Service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            // Next Friday at 16:00 UTC
            var now = DateTime.UtcNow;
            var daysUntilFriday = ((int)DayOfWeek.Friday - (int)now.DayOfWeek + 7) % 7;
            if (daysUntilFriday == 0 && now.Hour >= 16) daysUntilFriday = 7;

            var nextRun = now.Date.AddDays(daysUntilFriday).AddHours(16);
            var delay = nextRun - now;

            _logger.LogInformation("[WeeklyDigest] Next digest at {NextRun} UTC (in {Delay})", nextRun, delay);

            try
            {
                await Task.Delay(delay, stoppingToken);
                await SendWeeklyDigestsAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task SendWeeklyDigestsAsync(CancellationToken ct)
    {
        _logger.LogInformation("[WeeklyDigest] Sending weekly summaries…");

        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ActionItemService>();

        var channelIds = await service.GetActiveChannelsThisWeekAsync();

        foreach (var channelId in channelIds)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                var stats = await service.GetWeeklyStatsForChannelAsync(channelId);
                var openItems = await service.GetChannelActiveItemsAsync(channelId);
                await SendWeeklySummaryAsync(channelId, stats, openItems);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[WeeklyDigest] Failed for channel {ChannelId}", channelId);
            }
        }

        _logger.LogInformation("[WeeklyDigest] Done.");
    }

    private async Task SendWeeklySummaryAsync(string channelId, WeeklyStats stats, List<Domain.ActionItem> openItems)
    {
        var completionRate = stats.Created > 0
            ? (int)((double)stats.Completed / stats.Created * 100)
            : 0;

        var progressBar = BuildProgressBar(completionRate);

        var blocks = new List<object>
        {
            new { type = "header", text = new { type = "plain_text", text = ":bar_chart: Weekly Action Item Summary", emoji = true } },
            new {
                type = "section",
                fields = new object[] {
                    new { type = "mrkdwn", text = $"*Created this week:*\n{stats.Created}" },
                    new { type = "mrkdwn", text = $"*Completed:*\n{stats.Completed}" },
                    new { type = "mrkdwn", text = $"*Still open:*\n{stats.StillActive}" },
                    new { type = "mrkdwn", text = $"*Overdue:*\n{(stats.Overdue > 0 ? $"🔴 {stats.Overdue}" : "None ✅")}" }
                }
            },
            new {
                type = "section",
                text = new { type = "mrkdwn", text = $"*Completion rate:* {completionRate}%\n{progressBar}" }
            },
            new { type = "divider" }
        };

        if (openItems.Any())
        {
            blocks.Add(new { type = "section", text = new { type = "mrkdwn", text = $"*:clipboard: {openItems.Count} open item{(openItems.Count == 1 ? "" : "s")} heading into next week:*" } });

            foreach (var item in openItems.Take(10))
            {
                var due = item.DueDate.HasValue ? $" · Due {item.DueDate:dd MMM}" : "";
                var overdueTag = item.IsOverdue ? " 🔴" : "";
                blocks.Add(new {
                    type = "section",
                    text = new { type = "mrkdwn", text =
                        $"{UI.SlackMessageBuilder.GetEmoji(item.Type)} <@{item.UserId}> — *{item.Text}*{due}{overdueTag}" }
                });
            }

            if (openItems.Count > 10)
                blocks.Add(new { type = "context", elements = new[] { new { type = "mrkdwn", text = $"_…and {openItems.Count - 10} more. Use `/actions` to see all._" } } });
        }
        else
        {
            blocks.Add(new { type = "section", text = new { type = "mrkdwn", text = ":tada: *No open items heading into next week. Great work, team!*" } });
        }

        var client = _httpClientFactory.CreateClient("Slack");
        var payload = new { channel = channelId, text = "Weekly action item summary", blocks };
        await client.PostAsJsonAsync("https://slack.com/api/chat.postMessage", payload);
    }

    private static string BuildProgressBar(int percent)
    {
        var filled = (int)Math.Round(percent / 10.0);
        var empty = 10 - filled;
        return string.Concat(Enumerable.Repeat("█", filled)) + string.Concat(Enumerable.Repeat("░", empty)) + $" {percent}%";
    }
}
