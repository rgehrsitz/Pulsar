namespace Pulsar.RuleDefinition.Validation;

/// <summary>
/// Represents a validation error found during rule validation
/// </summary>
public class ValidationError
{
    /// <summary>
    /// Error message describing the validation failure
    /// </summary>
    public string Message { get; }

    public ValidationError(string message)
    {
        Message = message;
    }

    public override string ToString() => Message;
}
