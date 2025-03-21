// File: Pulsar.Compiler/Config/RuleGroupingConfig.cs

namespace Pulsar.Compiler.Config
{
    public class RuleGroupingConfig
    {
        /// <summary>
        /// Maximum number of rules per group
        /// </summary>
        public int MaxRulesPerGroup { get; set; } = 10;

        /// <summary>
        /// Maximum number of conditions per group
        /// </summary>
        public int MaxConditionsPerGroup { get; set; } = 50;

        /// <summary>
        /// Maximum number of actions per group
        /// </summary>
        public int MaxActionsPerGroup { get; set; } = 50;
    }
}
