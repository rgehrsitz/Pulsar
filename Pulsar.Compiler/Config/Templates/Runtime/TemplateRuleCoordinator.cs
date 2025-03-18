// File: Pulsar.Compiler/Config/Templates/Runtime/TemplateRuleCoordinator.cs
// Version: 1.0.0

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Beacon.Runtime.Buffers;
using Beacon.Runtime.Interfaces;
using Beacon.Runtime.Services;
using Serilog;

namespace Beacon.Runtime
{
    /// <summary>
    /// Base class for rule coordinators that handle organizing and executing rule groups
    /// </summary>
    public class TemplateRuleCoordinator : IRuleCoordinator
    {
        protected readonly IRedisService _redis;
        protected readonly ILogger _logger;
        protected readonly RingBufferManager _bufferManager;
        protected readonly List<IRuleGroup> _ruleGroups;

        public string[] RequiredSensors => Array.Empty<string>();

        public int RuleCount => _ruleGroups.Count;

        public TemplateRuleCoordinator(
            IRedisService redis,
            ILogger logger,
            RingBufferManager bufferManager
        )
        {
            _redis = redis;
            _logger = logger.ForContext<TemplateRuleCoordinator>();
            _bufferManager = bufferManager;
            _ruleGroups = new List<IRuleGroup>();
        }

        /// <summary>
        /// Process all sensor values through the buffer manager
        /// </summary>
        protected virtual void UpdateBuffers(Dictionary<string, object> inputs)
        {
            var now = DateTime.UtcNow;
            foreach (var (sensor, value) in inputs)
            {
                // Only handle numeric values for the buffer
                if (value is double doubleValue)
                {
                    _bufferManager.UpdateBuffer(sensor, doubleValue, now);
                }
                else if (double.TryParse(value.ToString(), out doubleValue))
                {
                    _bufferManager.UpdateBuffer(sensor, doubleValue, now);
                }
            }
        }

        /// <summary>
        /// Evaluates all rule groups with the given inputs
        /// </summary>
        public virtual async Task<Dictionary<string, object>> ExecuteRulesAsync(
            Dictionary<string, object> inputs
        )
        {
            try
            {
                // Update the buffer with new values
                UpdateBuffers(inputs);

                // Create an output dictionary to hold results
                var outputs = new Dictionary<string, object>();

                // Evaluate each rule group
                foreach (var ruleGroup in _ruleGroups)
                {
                    try
                    {
                        // Execute rules for this group using the interface method
                        await ruleGroup.EvaluateRulesAsync(inputs, outputs);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(
                            ex,
                            "Error evaluating rule group {RuleGroup}",
                            ruleGroup.Name
                        );
                    }
                }

                return outputs;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error executing rules");
                return new Dictionary<string, object>();
            }
        }
    }
}
