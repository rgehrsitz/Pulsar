using System.Threading.Tasks;

namespace Pulsar.Runtime.Engine;

/// <summary>
/// Interface for a rule engine that can execute rules against sensor data
/// </summary>
public interface IRuleEngine
{
    /// <summary>
    /// Executes one cycle of rule evaluation
    /// </summary>
    Task ExecuteCycleAsync();
}
