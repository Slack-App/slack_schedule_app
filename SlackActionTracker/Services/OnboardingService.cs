using System.Collections.Concurrent;

namespace SlackActionTracker.Services;

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
        var intro = "I automatically detect action items from your Slack conversations so nothing gets lost.";

        var howTo = "*Here's how to get started:*\n\n"
            + ":one: *Just chat normally.* I will pick up commitments, requests, and questions automatically.\n\n"
            + ":two: *Use `/action` to add items manually.*\n\n"
            + ":three: *Use `/actions` to see your open items* any time.\n\n"
            + ":four: *Click ... on any message and choose Track as Action* to track a message.\n\n"
            + ":five: *Check your Home tab* for your personal dashboard.";

        var whatIDetect = "*What I detect automatically:*\n"
            + ":pencil2: *Commitments* -- e.g. I will send that over by Friday\n"
            + ":incoming_envelope: *Requests* -- e.g. Can you review the PR?\n"
            + ":question: *Questions* -- Unanswered questions in channels";

        var blocks = new object[]
        {
            new {
                type = "header",
                text = new { type = "plain_text", text = ":wave: Welcome to Action Tracker!", emoji = true }
            },
            new {
                type = "section",
                text = new { type = "mrkdwn", text = intro }
            },
            new { type = "divider" },
            new {
                type = "section",
                text = new { type = "mrkdwn", text = howTo }
            },
            new { type = "divider" },
            new {
                type = "section",
                text = new { type = "mrkdwn", text = whatIDetect }
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
