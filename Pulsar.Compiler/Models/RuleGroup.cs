using System.Collections.Generic;

namespace Pulsar.Compiler.Models
{
    public class RuleGroup
    {
        public List<RuleDefinition> Rules { get; set; } = new List<RuleDefinition>();
    }
}
