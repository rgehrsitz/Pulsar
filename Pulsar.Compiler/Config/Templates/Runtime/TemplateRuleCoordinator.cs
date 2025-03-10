// File: Pulsar.Compiler/Config/Templates/Runtime/TemplateRuleCoordinator.cs
// Version: 1.0.0

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Beacon.Runtime.Buffers;
using Beacon.Runtime.Interfaces;
using Beacon.Runtime.Services;
using Microsoft.Extensions.Logging;

namespace Beacon.Runtime.Rules
{
    public abstract class TemplateRuleCoordinator : IRuleCoordinator
    {
        protected readonly IRedisService _redis;
        protected readonly ILogger _logger;
        protected readonly RingBufferManager _bufferManager;
        protected readonly List<IRuleGroup> _ruleGroups;

        public TemplateRuleCoordinator(
            IRedisService redis,
            ILogger logger,
            RingBufferManager bufferManager
        )
        {
            _redis = redis ?? throw new ArgumentNullException(nameof(redis));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _bufferManager =
                bufferManager ?? throw new ArgumentNullException(nameof(bufferManager));
            _ruleGroups = new List<IRuleGroup>();

            InitializeRuleGroups();
        }

        public string[] RequiredSensors => GetRequiredSensors();

        public async Task EvaluateRulesAsync(
            Dictionary<string, object> inputs,
            Dictionary<string, object> outputs
        )
        {
            foreach (var group in _ruleGroups)
            {
                try
                {
                    await group.EvaluateRulesAsync(inputs, outputs);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error evaluating rule group");
                }
            }
        }

        protected void AddRuleGroup(IRuleGroup group)
        {
            _ruleGroups.Add(group);
        }

        protected abstract void InitializeRuleGroups();

        private string[] GetRequiredSensors()
        {
            var sensors = new HashSet<string>();
            foreach (var group in _ruleGroups)
            {
                sensors.UnionWith(group.RequiredSensors);
            }
            return sensors.ToArray();
        }
    }
}
