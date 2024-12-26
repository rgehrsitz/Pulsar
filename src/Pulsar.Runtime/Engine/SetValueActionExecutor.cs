using System.Collections.Concurrent;
using System.Threading.Tasks;
using Pulsar.Models.Actions;
using Pulsar.Runtime.Storage;
using Serilog;

namespace Pulsar.Runtime.Engine;

public class SetValueActionExecutor : IActionExecutor
{
    private readonly ConcurrentDictionary<string, object> _pendingUpdates;
    protected readonly ILogger _logger;
    private readonly IDataStore _dataStore;

    public SetValueActionExecutor(ILogger logger, IDataStore dataStore)
    {
        _pendingUpdates = new ConcurrentDictionary<string, object>();
        _logger = logger.ForContext<SetValueActionExecutor>();
        _dataStore = dataStore;
    }

    public virtual Task<bool> ExecuteAsync(CompiledRuleAction action)
    {
        if (action.SetValue == null)
        {
            return Task.FromResult(false);
        }

        var value = action.SetValue.Value;
        if (action.SetValue.ValueExpression != null)
        {
            // TODO: Implement expression evaluation
            _logger.Warning("Value expressions not yet implemented");
            return Task.FromResult(false);
        }

        if (value == null)
        {
            _logger.Warning("Null value in SetValue action for key: {Key}", action.SetValue.Key);
            return Task.FromResult(false);
        }

        _logger.Information(
            "Setting value for {Key} to {Value}",
            action.SetValue.Key,
            value
        );

        if (string.IsNullOrWhiteSpace(action.SetValue.Key))
        {
            _logger.Warning("Invalid key in SetValue action: {Key}", action.SetValue.Key);
            return Task.FromResult(true); // Skip invalid keys
        }

        _pendingUpdates.AddOrUpdate(action.SetValue.Key, value, (_, _) => value);
        return Task.FromResult(true);
    }

    /// <summary>
    /// Gets all pending updates and clears the internal buffer
    /// </summary>
    /// <returns>A dictionary containing all pending updates</returns>
    public virtual ConcurrentDictionary<string, object> GetAndClearPendingUpdates()
    {
        var updates = new ConcurrentDictionary<string, object>(_pendingUpdates);
        _pendingUpdates.Clear();
        return updates;
    }

    /// <summary>
    /// Gets the current pending updates without clearing them
    /// </summary>
    /// <returns>A dictionary containing current pending updates</returns>
    public virtual ConcurrentDictionary<string, object> GetPendingUpdates()
    {
        return new ConcurrentDictionary<string, object>(_pendingUpdates);
    }
}
