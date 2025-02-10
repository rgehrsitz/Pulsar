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


using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;

namespace Pulsar.Runtime.Rules
{
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Types preserved in trimming.xml")]
    [UnconditionalSuppressMessage("Trimming", "IL2074", Justification = "Required interfaces preserved")]
    public class RuleCoordinator : IRuleCoordinator
    {
        private static readonly Meter s_meter = new("Pulsar.Runtime");
        private static readonly Counter<int> s_evaluationCount = s_meter.CreateCounter<int>("rule_evaluations_total");
        private static readonly Histogram<double> s_evaluationDuration = s_meter.CreateHistogram<double>("rule_evaluation_duration_seconds");

        private readonly ILogger _logger;
        private readonly RingBufferManager _bufferManager;

        // Rules for Group 0:
        // - TestRule from TestRules.yaml:15
        private readonly RuleGroup0 _group0;

        public RuleCoordinator(ILogger logger, RingBufferManager bufferManager)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _bufferManager = bufferManager ?? throw new ArgumentNullException(nameof(bufferManager));

            try
            {
                _group0 = new RuleGroup0(logger, bufferManager);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to initialize rule group 0");
                throw;
            }
        }

        public void EvaluateRules(Dictionary<string, double> inputs, Dictionary<string, double> outputs)
        {
            try
            {
                var startTime = DateTime.UtcNow;
                _logger.Debug("Starting rule evaluation");

                s_evaluationCount.Add(1);

                // Layer 0 rules:
                // - TestRule

                try
                {
                    _group0.EvaluateGroup(inputs, outputs, _bufferManager);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error evaluating rule group 0");
                    throw;
                }

                var duration = DateTime.UtcNow - startTime;
                s_evaluationDuration.Record(duration.TotalSeconds);

                _logger.Debug("Completed rule evaluation in {Duration}ms", duration.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error during rule evaluation");
                throw;
            }
        }

#if DEBUG
        public IEnumerable<string> GetRuleNames()
        {
            return new[]
            {
                "TestRule",
            };
        }
#endif
    }
}
