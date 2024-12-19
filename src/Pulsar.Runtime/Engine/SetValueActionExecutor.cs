using System.Collections.Concurrent;
using System.Threading.Tasks;
using Pulsar.RuleDefinition.Models;
using Serilog;

namespace Pulsar.Runtime.Engine;

/// <summary>
/// Executes actions that set values in a shared state dictionary, batching updates for efficient Redis operations
/// </summary>
public class SetValueActionExecutor : IActionExecutor
{
    private readonly ConcurrentDictionary<string, object> _pendingUpdates;
    protected readonly ILogger _logger;

    public SetValueActionExecutor(ILogger logger)
    {
        _pendingUpdates = new ConcurrentDictionary<string, object>();
        _logger = logger.ForContext<SetValueActionExecutor>();
    }

    public virtual Task<bool> ExecuteAsync(RuleAction action)
    {
        if (action.SetValue == null || action.SetValue.Count == 0)
        {
            return Task.FromResult(true);
        }

        try
        {
            foreach (var (key, value) in action.SetValue)
            {
                _pendingUpdates.AddOrUpdate(key, value, (_, _) => value);
                _logger.Debug("Queued value update {Key} to {Value}", key, value);
            }

            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to queue value updates");
            return Task.FromResult(false);
        }
    }

    /// <summary>
    /// Gets all pending updates and clears the internal buffer
    /// </summary>
    /// <returns>A dictionary containing all pending updates</returns>
    public System.Collections.Generic.IDictionary<string, object> GetAndClearPendingUpdates()
    {
        var updates = new System.Collections.Generic.Dictionary<string, object>(_pendingUpdates);
        _pendingUpdates.Clear();
        return updates;
    }

    /// <summary>
    /// Gets the current pending updates without clearing them
    /// </summary>
    /// <returns>A dictionary containing current pending updates</returns>
    public System.Collections.Generic.IDictionary<string, object> GetPendingUpdates()
    {
        return new System.Collections.Generic.Dictionary<string, object>(_pendingUpdates);
    }
}
