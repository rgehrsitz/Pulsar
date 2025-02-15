namespace Pulsar.Tests.TestUtilities
{
    public static class RuleValidator
    {
        public static ValidationResult Validate(string ruleContent)
        {
            // Simulated validation logic
            if (ruleContent.Contains("missing mandatory"))
            {
                return new ValidationResult
                {
                    IsValid = false,
                    Errors = new List<string> { "Error: missing mandatory fields." },
                    Metadata = "",
                };
            }
            return new ValidationResult
            {
                IsValid = true,
                Errors = new List<string>(),
                Metadata = "extracted metadata",
            };
        }
    }

    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<string>? Errors { get; set; }
        public string? Metadata { get; set; }
    }
}