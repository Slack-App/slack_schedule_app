using System.Collections.Concurrent;

namespace SlackActionTracker.Services;

/// <summary>
/// Sends a one-time welcome DM when a user opens the app home for the first time.
/// The in-memory set resets on restart, but re-sending the welcome DM on the rare
/// occasion is harmless and preferable to adding a DB table.
/// </summary>
public class OnboardingService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OnboardingService> _logger;
    private static readonly ConcurrentDictionary<string, bool> _onboardedUsers = new();

    public OnboardingService(IHttpClientFactory httpClientFactory, ILogger<OnboardingService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task OnboardIfNewAsync(string userId)
    {
        if (_onboardedUsers.ContainsKey(userId)) return;

        _onboardedUsers[userId] = true;
        _logger.LogInformation("[Onboarding] Sending welcome to new user {UserId}", userId);
        await SendWelcomeDmAsync(userId);
    }

    private async Task SendWelcomeDmAsync(string userId)
    {
        var blocks = new object[]
        {
            new {
                type = "header",
                text = new { type = "plain_text", text = ":wave: Welcome to Action Tracker!", emoji = true }
            },
            new {
                type = "section",
                text = new { type = "mrkdwn", text =
                    "I automatically detect action items from your conversations and keep them organised so nothing gets lost." }
            },
            new { type = "divider" },
            new {
                type = "section",
                text = new { type = "mrkdwn", text =
                    "*Here's how to get started:*\n\n" +
                    ":one: *Just chat normally.* I'll pick up commitments, requests, and questions automatically.\n\n" +
                    ":two: *Use `/action` to add items manually.* Great for tasks that come up outside Slack.\n\n" +
                    ":three: *Use `/actions` to see your open items* any time.\n\n" +
                    ":four: *Use the message shortcut* (click `...` on any message → *Track as Action*) to turn any message into an action item.\n\n" +
                    ":five: *Check your Home tab* for your personal dashboard." }
            },
            new { type = "divider" },
            new {
                type = "section",
                text = new { type = "mrkdwn", text =
                    "*What I detect automatically:*\n" +
                    ":pencil2: *Commitments* — "I'll send that over by Friday"\n" +
                    ":incoming_envelope: *Requests* — "Can you review the PR?"\n" +
                    ":question: *Questions* — Unanswered questions in channels" }
            },
            new {
                type = "actions",
                elements = new object[] {
                    new {
                        type = "button",
                        text = new { type = "plain_text", text = "Open My Dashboard", emoji = true },
                        style = "primary",
                        action_id = "open_home",
                        value = "open_home"
                    }
                }
            }
        };

        var client = _httpClientFactory.CreateClient("Slack");
        var payload = new { channel = userId, text = "Welcome to Action Tracker!", blocks };
        await client.PostAsJsonAsync("https://slack.com/api/chat.postMessage", payload);
    }
}
