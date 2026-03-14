using SlackActionTracker.Domain;
using System.Text.RegularExpressions;

namespace SlackActionTracker.Parsers;

public class RequestParser : IActionParser
{
    private static readonly Regex[] RequesterPatterns =
    {
        new Regex(@"^can you (?<text>.+?)\??$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"^could you (?<text>.+?)\??$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"^can someone (?<text>.+?)\??$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"^please (?<text>.+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"^would you (?<text>.+?)\??$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
    };

    private static readonly Regex[] DeadlinePatterns =
    {
        new(@"by\s+(friday|monday|tuesday|wednesday|thursday|saturday|sunday|tomorrow|today)", RegexOptions.IgnoreCase),

        new(@"before\s+(.+)", RegexOptions.IgnoreCase),

        new(@"by\s+end\s+of\s+(day|week|month)", RegexOptions.IgnoreCase)
    };

    public (string Text, ActionItemType Type, string? DueDate)? Parse(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return null;

        foreach (var pattern in RequesterPatterns)
        {
            var match = pattern.Match(message.Trim());
            if (match.Success)
            {
                var extractedText = match.Groups["text"].Value.Trim();

                string? detectedDeadline = null;
                foreach (var deadlinePattern in DeadlinePatterns)
                {
                    var deadlineMatch = deadlinePattern.Match(message);
                    if (deadlineMatch.Success)
                    {
                        detectedDeadline = deadlineMatch.Value
                            .TrimEnd('?', '.', '!', ',', ';', ' ')
                            .Trim();
                        break;
                    }
                }

                return (extractedText, ActionItemType.Request, detectedDeadline);
            }
        }
        return null;
    }
}