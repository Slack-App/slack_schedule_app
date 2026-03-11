using SlackActionTracker.Domain;
using System.Text.RegularExpressions;

namespace SlackActionTracker.Parsers;

public class RequestParser : IActionParser
{
    private static readonly Regex[] Patterns =
    {
        new Regex(@"^can you (?<text>.+?)\??$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"^could you (?<text>.+?)\??$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"^can someone (?<text>.+?)\??$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"^please (?<text>.+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"^would you (?<text>.+?)\??$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
    };

    public (string Text, ActionItemType Type)? Parse(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return null;

        foreach (var pattern in Patterns)
        {
            var match = pattern.Match(message.Trim());
            if (match.Success)
            {
                var extractedText = match.Groups["text"].Value.Trim();
                return (extractedText, ActionItemType.Request);
            }
        }
        return null;
    }
}