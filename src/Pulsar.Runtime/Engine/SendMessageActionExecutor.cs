using System.Threading.Tasks;
using Pulsar.Models.Actions;
using Serilog;

namespace Pulsar.Runtime.Engine;

/// <summary>
/// Executes actions that send messages to external systems
/// </summary>
public class SendMessageActionExecutor : IActionExecutor
{
    private readonly ILogger _logger;

    public SendMessageActionExecutor(ILogger logger)
    {
        _logger = logger.ForContext<SendMessageActionExecutor>();
    }

    public Task<bool> ExecuteAsync(CompiledRuleAction action)
    {
        if (action.SendMessage == null)
        {
            return Task.FromResult(false);
        }

        _logger.Information(
            "Sending message to channel {Channel}: {Message}",
            action.SendMessage.Channel,
            action.SendMessage.Message
        );

        return Task.FromResult(true);
    }
}
