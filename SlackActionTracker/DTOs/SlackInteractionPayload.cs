using SlackActionTracker.Domain;

namespace SlackActionTracker.DTOs
{
    public class SlackInteractionPayload
    {
        public string type { get; set; } = string.Empty;
        public SlackUser? user { get; set; }
        public List<SlackAction>? actions { get; set; }
        public string? response_url { get; set; }
        public string? trigger_id { get; set; }
    }
}
