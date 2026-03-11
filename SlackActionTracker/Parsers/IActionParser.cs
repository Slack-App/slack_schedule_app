
namespace SlackActionTracker.Parsers;

using SlackActionTracker.Domain;

public interface IActionParser
{
    (string Text, ActionItemType Type)? Parse(string message);
}