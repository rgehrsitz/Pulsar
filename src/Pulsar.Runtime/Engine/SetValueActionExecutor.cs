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

    public virtual async Task<bool> ExecuteAsync(CompiledRuleAction action)
    {
        if (action.SetValue == null)
        {
            return false;
        }

        var value = action.SetValue.Value;
        if (action.SetValue.ValueExpression != null)
        {
            // TODO: Implement expression evaluation
            _logger.Warning("Value expressions not yet implemented");
            return false;
        }

        if (value == null)
        {
            _logger.Warning("Null value in SetValue action for key: {Key}", action.SetValue.Key);
            return false;
        }

        _logger.Information(
            "Setting value for {Key} to {Value}",
            action.SetValue.Key,
            value
        );

        if (string.IsNullOrWhiteSpace(action.SetValue.Key))
        {
            _logger.Warning("Invalid key in SetValue action: {Key}", action.SetValue.Key);
            return false;
        }

        try
        {
            // Store in pending updates
            _pendingUpdates.AddOrUpdate(action.SetValue.Key, value, (_, _) => value);

            // Write to data store
            if (double.TryParse(value.ToString(), out double doubleValue))
            {
                await _dataStore.SetValueAsync(action.SetValue.Key, doubleValue);
                _logger.Debug(
                    "Successfully wrote value {Value} to key {Key} in data store",
                    doubleValue,
                    action.SetValue.Key
                );
                return true;
            }
            else
            {
                _logger.Warning(
                    "Could not parse value {Value} as double for key {Key}",
                    value,
                    action.SetValue.Key
                );
                return false;
            }
        }
        catch (System.Exception ex)
        {
            _logger.Error(
                ex,
                "Error writing value {Value} to key {Key}",
                value,
                action.SetValue.Key
            );
            return false;
        }
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
