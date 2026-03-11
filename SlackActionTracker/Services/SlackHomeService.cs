using SlackActionTracker.Domain;
using SlackActionTracker.Data;
using Microsoft.EntityFrameworkCore;
using System.Dynamic;

namespace SlackActionTracker.Services;

public class SlackHomeService
{
    private readonly AppDbContext _context;
    private readonly string _botToken;

    public SlackHomeService(AppDbContext context)
    {
        _context = context;
        _botToken = Environment.GetEnvironmentVariable("SLACK_BOT_TOKEN") ?? "";
    }

    public async Task PublishHomeAsync(string userId, string? filter = "all", string? sort = "newest")
    {
        var query = _context.ActionItems.Where(a => a.UserId == userId);

        if (filter != "all" && filter != "completed")
        {
            if (Enum.TryParse<ActionItemType>(filter, true, out var type))
                query = query.Where(a => a.Type == type && a.Status == "active");
        }
        else if (filter == "completed")
        {
            query = query.Where(a => a.Status == "completed");
        }
        else
        {
            query = query.Where(a => a.Status == "active");
        }

        query = sort == "oldest" 
            ? query.OrderBy(a => a.CreatedAt) 
            : query.OrderByDescending(a => a.CreatedAt);

        var items = await query.ToListAsync();
        
        var allActive = await _context.ActionItems.Where(a => a.UserId == userId && a.Status == "active").ToListAsync();
        int cCount = allActive.Count(a => a.Type == ActionItemType.Commitment);
        int rCount = allActive.Count(a => a.Type == ActionItemType.Request);
        int dCount = allActive.Count(a => a.Type == ActionItemType.Deadline);
        int qCount = allActive.Count(a => a.Type == ActionItemType.Question);

        var blocks = BuildHomeBlocks(items, cCount, rCount, dCount,qCount, filter, sort);

        await SendToSlack(userId, blocks);
    }

   private List<object> BuildHomeBlocks(List<ActionItem> items, int c, int r, int d, int q, string? filter, string? sort)
    {
        var blocks = new List<object> {
            new { type = "header", text = new { type = "plain_text", text = ":clipboard:Your Action Dashboard" } },
            new { 
                type = "section", 
                text = new { type = "mrkdwn", text = $"*Stats:* {c} Commitments | {r} Requests | {d} Deadlines | {q} Questions" } 
            },
            new { type = "divider" },
            new {
                type = "actions",
                elements = new List<object> {
                    CreateFilterBtn("All", "all", filter == "all"),
                    CreateFilterBtn("Commitments", "Commitment", filter == "Commitment"),
                    CreateFilterBtn("Requests", "Request", filter == "Request"),
                    CreateFilterBtn("Deadlines", "Deadline", filter == "Deadline"),
                    CreateFilterBtn("Questions", "Question", filter == "Question"),
                    CreateFilterBtn("Completed", "completed", filter == "completed")
                }
            },
            new {
                type = "actions",
                elements = new List<object> {
                    CreateSortBtn("Newest First", "newest", sort == "newest"),
                    CreateSortBtn("Oldest First", "oldest", sort == "oldest")
                }
            },
            new { type = "divider" }
        };

        if (!items.Any()) {
            blocks.Add(new { type = "section", text = new { type = "mrkdwn", text = "_No items found for this view._" } });
        }

        foreach (var item in items)
        {
            blocks.Add(new {
                type = "section",
                text = new { type = "mrkdwn", text = $"*{item.Type}:* {item.Text}\n_Added {item.CreatedAt:dd.MM.yyyy}_" }
            });

           if (item.Status != "completed") 
            {
                blocks.Add(new {
                    type = "actions",
                    elements = new List<object> {
                        new { 
                            type = "button", 
                            text = new { type = "plain_text", text = "Complete" }, 
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
                blocks.Add(new {
                    type = "context",
                    elements = new [] { new { type = "mrkdwn", text = ":white_check_mark: *This task is completed.*" } }
                });
            }
            blocks.Add(new { type = "divider" });
        }

        return blocks;
    }

   private object CreateFilterBtn(string text, string value, bool isActive)
    {
        var btn = new Dictionary<string, object>
        {
            { "type", "button" },
            { "text", new { type = "plain_text", text = isActive ? $"• {text} •" : text } },
            { "value", value },
            { "action_id", $"filter_{value.ToLower()}" }
        };

        if (isActive) {
            btn.Add("style", "primary");
        }

        return btn;
    }

    private object CreateSortBtn(string text, string value, bool isActive)
    {
        var btn = new Dictionary<string, object>
        {
            { "type", "button" },
            { "text", new { type = "plain_text", text = text } },
            { "value", value },
            { "action_id", $"sort_{value}" }
        };

        if (isActive) {
            btn.Add("style", "primary");
        }

        return btn;
    }

    private async Task SendToSlack(string userId, List<object> blocks)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _botToken);
        
        var payload = new { user_id = userId, view = new { type = "home", blocks = blocks } };
        var response = await client.PostAsJsonAsync("https://slack.com/api/views.publish", payload);
        var responseContent = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"Slack Response: {responseContent}");
    }
}