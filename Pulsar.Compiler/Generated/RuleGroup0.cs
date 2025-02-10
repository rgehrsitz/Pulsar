// Generated code - do not modify directly
// Generated at: 2025-02-09 23:26:45 UTC

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Serilog;
using Prometheus;
using Pulsar.Runtime.Buffers;
using Pulsar.Runtime;

namespace Pulsar.Runtime.Rules
{
    public class RuleGroup0 : IRuleGroup
    {
        private readonly ILogger _logger;
        private readonly RingBufferManager _bufferManager;

        public RuleGroup0(ILogger logger, RingBufferManager bufferManager)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _bufferManager = bufferManager ?? throw new ArgumentNullException(nameof(bufferManager));
        }

        public void EvaluateGroup(Dictionary<string, double> inputs, Dictionary<string, double> outputs, RingBufferManager bufferManager)
        {
            try
            {
                _logger.Debug("Evaluating rule group 0");
                // Source: TestRules.yaml:15
                // Rule: TestRule
                // Description: A test rule for temperature conversion
                _logger.Debug("Evaluating rule TestRule");
                if (inputs["temperature_f"] == 100)
                {
                    outputs["temperature_c"] = 212;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error evaluating rule group 0");
                throw;
            }
            _logger.Debug("Completed rule group 0");
        }
    }
}
