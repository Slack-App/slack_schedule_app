using SlackActionTracker.Domain;
using System.Text.RegularExpressions;

namespace SlackActionTracker.Parsers;

public class DeadlineParser:IActionParser
{
    private static readonly Regex[] Patterns =
    {
        new(@"by\s+(friday|monday|tuesday|wednesday|thursday|saturday|sunday|tomorrow|today)", RegexOptions.IgnoreCase),
        
        new(@"before\s+(.+)", RegexOptions.IgnoreCase),
        
        new(@"by\s+end\s+of\s+(day|week|month)", RegexOptions.IgnoreCase)
    };

    public (string Text, ActionItemType Type)? Parse(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return null;

        foreach (var pattern in Patterns)
        {
            var match = pattern.Match(message);
            if (match.Success)
                return (match.Value.Trim(), ActionItemType.Deadline);
        }
        return null;
    }
}