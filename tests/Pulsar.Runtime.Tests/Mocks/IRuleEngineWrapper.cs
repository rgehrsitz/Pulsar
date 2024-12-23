using System.Threading;
using System.Threading.Tasks;
using Pulsar.Runtime.Engine;

namespace Pulsar.Runtime.Tests.Mocks;

public interface IRuleEngineWrapper
{
    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
}

public class RuleEngineWrapper : IRuleEngineWrapper
{
    private readonly RuleEngine _ruleEngine;

    public RuleEngineWrapper(RuleEngine ruleEngine)
    {
        _ruleEngine = ruleEngine;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return _ruleEngine.StartAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return _ruleEngine.StopAsync(cancellationToken);
    }
}
