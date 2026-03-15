using SlackActionTracker.Domain;
using SlackActionTracker.Data;
using SlackActionTracker.UI;
using Microsoft.EntityFrameworkCore;

namespace SlackActionTracker.Services;

public class SlackHomeService
{
    private readonly AppDbContext _context;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _botToken;

    public SlackHomeService(AppDbContext context, IHttpClientFactory httpClientFactory)
    {
        _context = context;
        _httpClientFactory = httpClientFactory;
        _botToken = Environment.GetEnvironmentVariable("SLACK_BOT_TOKEN") ?? "";
    }

    public async Task PublishHomeAsync(string userId, string? filter = "all", string? sort = "newest")
    {
        var query = _context.ActionItems
            .Where(a => (a.UserId == userId || a.AssigneeId == userId) && a.Type != ActionItemType.Deadline);

        if (filter == "completed")
        {
            query = query.Where(a => a.Status == "completed");
        }
        else if (filter != null && filter != "all" && Enum.TryParse<ActionItemType>(filter, true, out var type))
        {
            query = query.Where(a => a.Type == type && a.Status == "active");
        }
        else if (filter == "overdue")
        {
            query = query.Where(a => a.Status == "active" && a.DueDate.HasValue && a.DueDate.Value < DateTime.UtcNow);
        }
        else
        {
            query = query.Where(a => a.Status == "active");
        }

        query = sort == "oldest"
            ? query.OrderBy(a => a.CreatedAt)
            : query.OrderByDescending(a => a.Priority)
                   .ThenBy(a => a.DueDate.HasValue ? 0 : 1)
                   .ThenBy(a => a.DueDate)
                   .ThenByDescending(a => a.CreatedAt);

        var items = await query.ToListAsync();

        var allActive = await _context.ActionItems
            .Where(a => (a.UserId == userId || a.AssigneeId == userId) && a.Status == "active")
            .ToListAsync();

        int cCount = allActive.Count(a => a.Type == ActionItemType.Commitment);
        int rCount = allActive.Count(a => a.Type == ActionItemType.Request);
        int qCount = allActive.Count(a => a.Type == ActionItemType.Question);
        int overdueCount = allActive.Count(a => a.IsOverdue);

        var blocks = BuildHomeBlocks(items, cCount, rCount, qCount, overdueCount, filter, sort);
        await SendToSlack(userId, blocks);
    }

    private List<object> BuildHomeBlocks(
        List<ActionItem> items, int c, int r, int q, int overdue,
        string? filter, string? sort)
    {
        var overdueWarning = overdue > 0
            ? $" :red_circle: *{overdue} overdue*"
            : "";

        var blocks = new List<object>
        {
            new { type = "header", text = new { type = "plain_text", text = ":clipboard: Your Action Dashboard", emoji = true } },
            new {
                type = "section",
                text = new { type = "mrkdwn", text =
                    $"*Active:* {c + r + q} total — {c} Commitments · {r} Requests · {q} Questions{overdueWarning}" }
            },
            new { type = "divider" },
            // Filter row
            new {
                type = "actions",
                elements = new List<object> {
                    CreateFilterBtn("All", "all", filter == "all"),
                    CreateFilterBtn("Commitments", "Commitment", filter == "Commitment"),
                    CreateFilterBtn("Requests", "Request", filter == "Request"),
                    CreateFilterBtn("Questions", "Question", filter == "Question"),
                    CreateFilterBtn("Completed", "completed", filter == "completed"),
                }
            },
            new {
                type = "actions",
                elements = new List<object> {
                    CreateFilterBtn("🔴 Overdue", "overdue", filter == "overdue"),
                    CreateSortBtn("Newest First", "newest", sort == "newest"),
                    CreateSortBtn("Oldest First", "oldest", sort == "oldest")
                }
            },
            new { type = "divider" }
        };

        if (!items.Any())
        {
            blocks.Add(new { type = "section", text = new { type = "mrkdwn", text = "_No items found for this view._ :tada:" } });
            return blocks;
        }

        foreach (var item in items)
        {
            var isOverdue = item.IsOverdue;
            var overdueTag = isOverdue ? " 🔴 *OVERDUE*" : "";
            var assigneeTag = !string.IsNullOrEmpty(item.AssigneeId) ? $" · Assigned to <@{item.AssigneeId}>" : "";
            var dueDateTag = item.DueDate.HasValue ? $" · Due {item.DueDate:dd MMM}" : (!string.IsNullOrEmpty(item.DueDateText) ? $" · Due {item.DueDateText}" : "");

            blocks.Add(new
            {
                type = "section",
                text = new
                {
                    type = "mrkdwn",
                    text = $"{SlackMessageBuilder.GetEmoji(item.Type)} {SlackMessageBuilder.PriorityEmoji(item.Priority)} *{item.Text}*{overdueTag}\n" +
                           $"_{item.Type}{dueDateTag}{assigneeTag} · Added {item.CreatedAt:dd MMM yyyy}_"
                }
            });

            if (item.Status != "completed")
            {
                blocks.Add(new
                {
                    type = "actions",
                    elements = new List<object> {
                        new {
                            type = "button",
                            text = new { type = "plain_text", text = "✅ Complete" },
                            style = "primary",
                            value = item.Id.ToString(),
                            action_id = "home_complete"
                        },
                        new {
                            type = "button",
                            text = new { type = "plain_text", text = "Remove" },
                            style = "danger",
                            value = item.Id.ToString(),
                            action_id = "home_remove"
                        }
                    }
                });
            }
            else
            {
                blocks.Add(new
                {
                    type = "context",
                    elements = new[] { new { type = "mrkdwn", text = $":white_check_mark: Completed {item.CompletedAt:dd MMM yyyy}" } }
                });
            }

            blocks.Add(new { type = "divider" });
        }

        return blocks;
    }

    private static object CreateFilterBtn(string text, string value, bool isActive)
    {
        var btn = new Dictionary<string, object>
        {
            { "type", "button" },
            { "text", new { type = "plain_text", text = isActive ? $"• {text} •" : text, emoji = true } },
            { "value", value },
            { "action_id", $"filter_{value.ToLower()}" }
        };
        if (isActive) btn["style"] = "primary";
        return btn;
    }

    private static object CreateSortBtn(string text, string value, bool isActive)
    {
        var btn = new Dictionary<string, object>
        {
            { "type", "button" },
            { "text", new { type = "plain_text", text = text } },
            { "value", value },
            { "action_id", $"sort_{value}" }
        };
        if (isActive) btn["style"] = "primary";
        return btn;
    }

    private async Task SendToSlack(string userId, List<object> blocks)
    {
        var client = _httpClientFactory.CreateClient("Slack");
        var payload = new { user_id = userId, view = new { type = "home", blocks } };
        var response = await client.PostAsJsonAsync("https://slack.com/api/views.publish", payload);
        var body = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"[HomeService] views.publish: {body}");
    }
}
