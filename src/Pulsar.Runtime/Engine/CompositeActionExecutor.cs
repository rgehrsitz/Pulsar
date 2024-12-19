using System.Collections.Generic;
using System.Threading.Tasks;
using Pulsar.RuleDefinition.Models;
using Serilog;

namespace Pulsar.Runtime.Engine;

/// <summary>
/// A composite action executor that delegates to specific executors based on action type
/// </summary>
public class CompositeActionExecutor : IActionExecutor
{
    private readonly IReadOnlyDictionary<string, IActionExecutor> _executors;
    private readonly ILogger _logger;

    public CompositeActionExecutor(IReadOnlyDictionary<string, IActionExecutor> executors, ILogger logger)
    {
        _executors = executors;
        _logger = logger.ForContext<CompositeActionExecutor>();
    }

    public async Task<bool> ExecuteAsync(RuleAction action)
    {
        var results = new List<bool>();

        // Handle SetValue actions
        if (action.SetValue?.Count > 0 && _executors.TryGetValue("setValue", out var setValueExecutor))
        {
            var result = await setValueExecutor.ExecuteAsync(action);
            results.Add(result);
        }

        // Handle SendMessage actions
        if (action.SendMessage?.Count > 0 && _executors.TryGetValue("sendMessage", out var sendMessageExecutor))
        {
            var result = await sendMessageExecutor.ExecuteAsync(action);
            results.Add(result);
        }

        // If no executors were found for any actions, log a warning
        if (results.Count == 0)
        {
            _logger.Warning("No executors found for action");
            return false;
        }

        // Return true only if all actions were executed successfully
        return results.All(r => r);
    }

    /// <summary>
    /// Gets all pending updates from the SetValueActionExecutor
    /// </summary>
    /// <returns>A dictionary containing all pending updates, or an empty dictionary if no SetValueActionExecutor is available</returns>
    public IDictionary<string, object> GetAndClearPendingUpdates()
    {
        if (_executors.TryGetValue("setValue", out var executor) && executor is SetValueActionExecutor setValueExecutor)
        {
            return setValueExecutor.GetAndClearPendingUpdates();
        }

        _logger.Warning("No SetValueActionExecutor found");
        return new Dictionary<string, object>();
    }

    /// <summary>
    /// Gets the current pending updates without clearing them
    /// </summary>
    /// <returns>A dictionary containing current pending updates, or an empty dictionary if no SetValueActionExecutor is available</returns>
    public IDictionary<string, object> GetPendingUpdates()
    {
        if (_executors.TryGetValue("setValue", out var executor) && executor is SetValueActionExecutor setValueExecutor)
        {
            return setValueExecutor.GetPendingUpdates();
        }

        _logger.Warning("No SetValueActionExecutor found");
        return new Dictionary<string, object>();
    }
}
