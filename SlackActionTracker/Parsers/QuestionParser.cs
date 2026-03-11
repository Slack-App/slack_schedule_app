using SlackActionTracker.Domain;

namespace SlackActionTracker.Parsers;

public class QuestionParser : IActionParser
{
    public (string Text, ActionItemType Type)? Parse(string message)
    {
        if (!string.IsNullOrWhiteSpace(message) && message.Contains("?"))
        {
            return (message.Trim(), ActionItemType.Question);
        }
        return null;
    }
}