using SlackActionTracker.Domain;

namespace SlackActionTracker.Parsers;

public class QuestionParser : IActionParser
{
    public (string Text, ActionItemType Type, string? DueDate)? Parse(string message)
    {
        if (!string.IsNullOrWhiteSpace(message) && message.Contains("?"))
        {
            return (message.Trim(), ActionItemType.Question, null);
        }
        return null;
    }
}