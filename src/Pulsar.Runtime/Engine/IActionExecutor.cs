using System.Threading.Tasks;
using Pulsar.Models.Actions;

namespace Pulsar.Runtime.Engine;

/// <summary>
/// Interface for executing rule actions
/// </summary>
public interface IActionExecutor
{
    /// <summary>
    /// Executes a rule action
    /// </summary>
    /// <param name="action">The action to execute</param>
    /// <returns>True if the action was executed successfully</returns>
    Task<bool> ExecuteAsync(CompiledRuleAction action);
}
