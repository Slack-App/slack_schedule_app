namespace SlackActionTracker.DTOs
{
    public class SlackAction
    {
        public string action_id { get; set; } = string.Empty;
        public string value { get; set; } = string.Empty;
        public string action_ts { get; set; } = string.Empty;
    }
}
