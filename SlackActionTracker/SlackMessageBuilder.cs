using SlackActionTracker.Domain;
using System.Text.RegularExpressions;

namespace SlackActionTracker.UI;

public static class SlackMessageBuilder
{
    // ─── Personal item list (/actions) ──────────────────────────────────────────

    public static object BuildActiveItemsList(IEnumerable<ActionItem> items)
    {
        var itemList = items.Take(20).ToList();
        var total = items.Count();

        var blocks = new List<object>
        {
            new {
                type = "header",
                text = new { type = "plain_text", text = ":clipboard: Your Action Items", emoji = true }
            },
            new {
                type = "context",
                elements = new[] {
                    new { type = "mrkdwn", text = $"Showing *{itemList.Count}* of *{total}* open items." }
                }
            },
            new { type = "divider" }
        };

        foreach (var item in itemList)
        {
            var emoji = GetEmoji(item.Type);
            var priorityEmoji = PriorityEmoji(item.Priority);
            var overdueTag = item.IsOverdue ? " 🔴 *OVERDUE*" : "";
            var dueTag = item.DueDate.HasValue
                ? $"\n:alarm_clock: Due *{item.DueDate:dd MMM yyyy}*{overdueTag}"
                : (!string.IsNullOrEmpty(item.DueDateText) ? $"\n:alarm_clock: Due *{item.DueDateText}*" : "");
            var assigneeTag = !string.IsNullOrEmpty(item.AssigneeId)
                ? $"\n:bust_in_silhouette: Assigned to <@{item.AssigneeId}>"
                : "";
            var cleanTs = item.MessageTimestamp?.Replace(".", "") ?? "";

            blocks.Add(new
            {
                type = "section",
                text = new
                {
                    type = "mrkdwn",
                    text = $"{emoji} {priorityEmoji} *{FormatItemText(item)}*{dueTag}{assigneeTag}\n" +
                           $"_Type: {item.Type} · Added {item.CreatedAt:dd MMM}_"
                }
            });

            var actionElements = new List<object>
            {
                CreateButton("✅ Complete", "complete_item", item.Id.ToString(), "primary"),
                new {
                    type = "button",
                    text = new { type = "plain_text", text = "🔗 View" },
                    url = $"https://slack.com/archives/{item.ChannelId}/p{cleanTs}",
                    action_id = "view_original"
                },
                CreateButton("Remove", "remove_item", item.Id.ToString(), "danger", confirm: true)
            };

            blocks.Add(new { type = "actions", elements = actionElements });
            blocks.Add(new { type = "divider" });
        }

        if (total > 20)
        {
            blocks.Add(new {
                type = "context",
                elements = new[] {
                    new { type = "mrkdwn", text = $":point_right: _+{total - 20} more. Open the *Home* tab to see everything._" }
                }
            });
        }

        return new { response_type = "ephemeral", blocks };
    }

    // ─── Channel board (/actions list) ──────────────────────────────────────────

    public static object BuildChannelBoard(IEnumerable<ActionItem> items)
    {
        var itemList = items.ToList();
        var overdueCount = itemList.Count(i => i.IsOverdue);

        var blocks = new List<object>
        {
            new {
                type = "header",
                text = new { type = "plain_text", text = ":clipboard: Channel Action Board", emoji = true }
            },
            new {
                type = "section",
                text = new { type = "mrkdwn", text =
                    $"*{itemList.Count} open item{(itemList.Count == 1 ? "" : "s")}*" +
                    (overdueCount > 0 ? $" · 🔴 *{overdueCount} overdue*" : " · All on track ✅") }
            },
            new { type = "divider" }
        };

        foreach (var item in itemList)
        {
            var priorityEmoji = PriorityEmoji(item.Priority);
            var overdueTag = item.IsOverdue ? " 🔴" : "";
            var dueTag = item.DueDate.HasValue ? $" · Due *{item.DueDate:dd MMM}*{overdueTag}" : "";
            var assigneeTag = !string.IsNullOrEmpty(item.AssigneeId)
                ? $" · <@{item.AssigneeId}>"
                : $" · <@{item.UserId}>";

            blocks.Add(new {
                type = "section",
                text = new {
                    type = "mrkdwn",
                    text = $"{GetEmoji(item.Type)} {priorityEmoji} *{item.Text}*{dueTag}{assigneeTag}"
                },
                accessory = new {
                    type = "button",
                    text = new { type = "plain_text", text = "✅ Done" },
                    style = "primary",
                    value = item.Id.ToString(),
                    action_id = "complete_item"
                }
            });
        }

        blocks.Add(new {
            type = "context",
            elements = new[] {
                new { type = "mrkdwn", text = "_Use `/action` to add a new item · `/actions` to see your personal list_" }
            }
        });

        return new { response_type = "in_channel", blocks };
    }

    // ─── Emoji helpers ───────────────────────────────────────────────────────────

    public static string GetEmoji(ActionItemType type) => type switch
    {
        ActionItemType.Commitment => ":pencil2:",
        ActionItemType.Request    => ":incoming_envelope:",
        ActionItemType.Question   => ":question:",
        ActionItemType.Deadline   => ":alarm_clock:",
        _                         => ":small_blue_diamond:"
    };

    public static string PriorityEmoji(ActionPriority priority) => priority switch
    {
        ActionPriority.High   => "🔴",
        ActionPriority.Medium => "🟡",
        ActionPriority.Low    => "🔵",
        _                     => "🟡"
    };

    // ─── Text formatting ─────────────────────────────────────────────────────────

    public static string FormatItemText(ActionItem item)
    {
        var text = item.Text;

        if (item.Type == ActionItemType.Request && !string.IsNullOrEmpty(item.DueDateText))
        {
            text = Regex.Replace(text, Regex.Escape(item.DueDateText), "", RegexOptions.IgnoreCase)
                        .TrimEnd('?', '.', ' ', ',').Trim();
        }

        return text;
    }

    // ─── Button builder ──────────────────────────────────────────────────────────

    private static object CreateButton(string text, string actionId, string value, string style, bool confirm = false)
    {
        if (confirm)
        {
            return new
            {
                type = "button",
                text = new { type = "plain_text", text },
                style,
                value,
                action_id = actionId,
                confirm = new
                {
                    title = new { type = "plain_text", text = "Are you sure?" },
                    text = new { type = "plain_text", text = "This will remove the item from your list." },
                    confirm = new { type = "plain_text", text = "Remove" },
                    deny = new { type = "plain_text", text = "Cancel" }
                }
            };
        }

        return new { type = "button", text = new { type = "plain_text", text }, style, value, action_id = actionId };
    }
}
