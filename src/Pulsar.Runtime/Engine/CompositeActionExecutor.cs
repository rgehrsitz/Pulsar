using System.Collections.Generic;
using System.Threading.Tasks;
using Pulsar.Models.Actions;
using Serilog;

namespace Pulsar.Runtime.Engine;

/// <summary>
/// A composite action executor that delegates to specific executors based on action type
/// </summary>
public class CompositeActionExecutor : IActionExecutor
{
    private readonly ILogger _logger;
    private readonly IEnumerable<IActionExecutor> _executors;

    public CompositeActionExecutor(ILogger logger, IEnumerable<IActionExecutor> executors)
    {
        _logger = logger.ForContext<CompositeActionExecutor>();
        _executors = executors;
    }

    public async Task<bool> ExecuteAsync(CompiledRuleAction action)
    {
        var success = true;
        foreach (var executor in _executors)
        {
            try
            {
                success &= await executor.ExecuteAsync(action);
            }
            catch (System.Exception ex)
            {
                _logger.Error(ex, "Action executor {Executor} failed", executor.GetType().Name);
                success = false;
            }
        }
        return success;
    }

    /// <summary>
    /// Gets all pending updates from the SetValueActionExecutor
    /// </summary>
    /// <returns>A dictionary containing all pending updates, or an empty dictionary if no SetValueActionExecutor is available</returns>
    public IDictionary<string, object> GetAndClearPendingUpdates()
    {
        // Note: This method is not updated to use CompiledRuleAction, it is left as is
        if (
            _executors is IReadOnlyDictionary<string, IActionExecutor> executors
            && executors.TryGetValue("setValue", out var executor)
            && executor is SetValueActionExecutor setValueExecutor
        )
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
        // Note: This method is not updated to use CompiledRuleAction, it is left as is
        if (
            _executors is IReadOnlyDictionary<string, IActionExecutor> executors
            && executors.TryGetValue("setValue", out var executor)
            && executor is SetValueActionExecutor setValueExecutor
        )
        {
            return setValueExecutor.GetPendingUpdates();
        }

        _logger.Warning("No SetValueActionExecutor found");
        return new Dictionary<string, object>();
    }
}
