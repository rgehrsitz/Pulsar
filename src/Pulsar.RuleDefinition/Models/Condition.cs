using YamlDotNet.Serialization;

namespace Pulsar.RuleDefinition.Models;

public abstract class Condition
{
    [YamlMember(Alias = "type")]
    public string Type { get; set; } = string.Empty;
}

public class ComparisonCondition : Condition
{
    public string Sensor { get; set; } = string.Empty;
    public string Operator { get; set; } = string.Empty;
    public double Value { get; set; }
}

public class ThresholdOverTimeCondition : Condition
{
    public string Sensor { get; set; } = string.Empty;
    public double Threshold { get; set; }
    public string Duration { get; set; } = string.Empty;

    public int DurationMs => ParseDuration(Duration);

    private static int ParseDuration(string duration)
    {
        if (duration.EndsWith("ms"))
        {
            return int.Parse(duration[..^2]);
        }
        throw new System.FormatException($"Invalid duration format: {duration}");
    }
}

public class ExpressionCondition : Condition
{
    public string Expression { get; set; } = string.Empty;
}
