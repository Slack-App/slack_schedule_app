using SlackActionTracker.Domain;

namespace SlackActionTracker.UI;

public static class SlackMessageBuilder
{
    public static object BuildActiveItemsList(IEnumerable<ActionItem> items)
    {
        if (!items.Any())
        {
            return new { 
                response_type = "ephemeral", 
                text = "You have no active action items. :tada:" 
            };
        }

        var itemList = items.Take(20).ToList();
        
        var blocks = new List<object>
        {
            new {
                type = "header",
                text = new { type = "plain_text", text = ":clipboard: Your Active Action Items", emoji = true }
            },
            new {
                type = "context",
                elements = new[] { 
                    new { type = "mrkdwn", text = $"Showing *{itemList.Count}* of *{items.Count()}* pending items." } 
                }
            },
            new { type = "divider" }
        };

        foreach (var item in itemList)
        {
            var emoji = GetEmoji(item.Type);
            var cleanTs = item.MessageTimestamp?.Replace(".", "") ?? "";

            blocks.Add(new
            {
                type = "section",
                text = new { 
                    type = "mrkdwn", 
                    text = $"{emoji} *{item.Text}*\n_Type: {item.Type}  |  Created: {item.CreatedAt:dd MMM}_ | :id: {item.Id.ToString()}" 
                }
            });

            blocks.Add(new
            {
                type = "actions",
                elements = new object[] {
                    CreateButton(":heavy_check_mark: Complete", "complete_item", item.Id.ToString(), "primary"),
                    new {
                        type = "button",
                        text = new { type = "plain_text", text = "View" },
                        url = $"https://slack.com/archives/{item.ChannelId}/p{cleanTs}",
                        action_id = "view_original"
                    },
                    CreateButton("Remove", "remove_item", item.Id.ToString(), "danger", true)
                }
            });

            blocks.Add(new { type = "divider" });
        }

        if (items.Count() > 20)
        {
            blocks.Add(new {
                type = "context",
                elements = new[] { 
                    new { type = "mrkdwn", text = ":point_right: _Visit the *Home* tab to see all items._" } 
                }
            });
        }

        return new
        {
            response_type = "ephemeral",
            blocks = blocks
        };
    }

    private static string GetEmoji(ActionItemType type) => type switch
    {
        ActionItemType.Commitment => ":pencil2:",
        ActionItemType.Request => ":incoming_envelope:",
        ActionItemType.Question => ":question:",
        ActionItemType.Deadline => ":alarm_clock:",
        _ => ":small_blue_diamond:"
    };

    private static object CreateButton(string text, string actionId, string value, string style, bool confirm = false)
    {
        if (confirm)
        {
            return new
            {
                type = "button",
                text = new { type = "plain_text", text = text },
                style = style,
                value = value,
                action_id = actionId,
                confirm = new
                {
                    title = new { type = "plain_text", text = "Are you sure?" },
                    text = new { type = "plain_text", text = "This will remove the item from your active list." },
                    confirm = new { type = "plain_text", text = text },
                    deny = new { type = "plain_text", text = "Cancel" }
                }
            };
        }

        return new
        {
            type = "button",
            text = new { type = "plain_text", text = text },
            style = style,
            value = value,
            action_id = actionId
        };
    }
}