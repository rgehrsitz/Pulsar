using YamlDotNet.Serialization;

namespace Pulsar.RuleDefinition.Models.Conditions;

/// <summary>
/// Base class for all condition types
/// </summary>
[YamlSerializable]
public abstract class Condition
{
    [YamlMember(Alias = "type")]
    public string Type { get; set; } = string.Empty;
}
