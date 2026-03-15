using System.Text.Json;
using System.Text.Json.Serialization;

namespace SlackActionTracker.DTOs
{
    public class SlackAction
    {
        public string action_id { get; set; } = string.Empty;
        public string value { get; set; } = string.Empty;
        public string action_ts { get; set; } = string.Empty;
    }

    public class SlackInteractionPayload
    {
        public string type { get; set; } = string.Empty;
        public string? callback_id { get; set; }
        public SlackUser? user { get; set; }
        public List<SlackAction>? actions { get; set; }
        public string? response_url { get; set; }
        public string? trigger_id { get; set; }

        // For modal submissions
        public SlackView? view { get; set; }

        // For message shortcuts
        public SlackMessage? message { get; set; }
        public SlackChannel? channel { get; set; }
    }

    public class SlackView
    {
        public string? callback_id { get; set; }
        public string? private_metadata { get; set; }

        // Modal state values: block_id → action_id → element
        public SlackViewState? state { get; set; }
    }

    public class SlackViewState
    {
        public Dictionary<string, Dictionary<string, JsonElement>>? values { get; set; }
    }

    public class SlackMessage
    {
        public string? text { get; set; }
        public string? ts { get; set; }
        public string? user { get; set; }
    }

    public class SlackChannel
    {
        public string? id { get; set; }
        public string? name { get; set; }
    }
}

namespace SlackActionTracker.Domain
{
    public class SlackUser
    {
        public string id { get; set; } = string.Empty;
        public string username { get; set; } = string.Empty;
    }
}
