using SlackActionTracker.Domain;
using SlackActionTracker.Parsers;
using System.Threading.Tasks;

namespace SlackActionTracker.Services;

public class SlackEventProcessor
{
    private readonly IEnumerable<IActionParser> _parsers;
    private readonly ActionItemService _actionService;

    public SlackEventProcessor(IEnumerable<IActionParser> parsers, ActionItemService actionService)
    {
        _parsers = parsers;
        _actionService = actionService;
    }
    public async Task ProcessMessage(string user, string channel, string text, string eventId, string messageTs)
    {
        foreach (var parser in _parsers)
        {
            var result = parser.Parse(text);
            if (result.HasValue)
            {
                await _actionService.TryCreateFromMessage(user, channel, result.Value.Text, text, result.Value.Type, eventId, messageTs);            }
            }
    }
}
