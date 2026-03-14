using SlackActionTracker.Domain;
using System.Text.RegularExpressions;

namespace SlackActionTracker.Parsers;

public class CommitmentParser : IActionParser
{
    private static readonly Regex[] Patterns =
    {
        new(@"^\s*i['�]?ll\s+(?<text>.+)", RegexOptions.IgnoreCase),
        new(@"^\s*i\s+will\s+(?<text>.+)", RegexOptions.IgnoreCase),
        new(@"^\s*let\s+me\s+(?<text>.+)", RegexOptions.IgnoreCase),
        new(@"^\s*i\s+can\s+(?<text>.+)", RegexOptions.IgnoreCase),
        new(@"^\s*i['�]?m\s+going\s+to\s+(?<text>.+)", RegexOptions.IgnoreCase),
    };

    private static readonly string[] NegativeWords =
    {
        "never",
        "not",
        "cannot",
        "can't"
    };

    public (string Text, ActionItemType Type, string? DueDate)? Parse(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return null;

        foreach (var pattern in Patterns)
        {
            var match = pattern.Match(message);
            if (match.Success)
            {
                var text = match.Groups["text"].Value.Trim();
                if (ContainsNegative(text)) return null;

                return (text, ActionItemType.Commitment, null);
            }
        }
        return null;
    }
    private bool ContainsNegative(string text)
    {
        var lowerText = text.ToLower();

        return NegativeWords.Any(neg => lowerText.Contains(neg));
    }

}