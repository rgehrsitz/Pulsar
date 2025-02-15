namespace Pulsar.Tests.TestUtilities
{
    public static class RuleParser
    {
        public static RuleParseResult Parse(string ruleContent)
        {
            // Dummy implementation for test scaffolding
            if (ruleContent.Contains("invalid"))
            {
                return new RuleParseResult
                {
                    IsValid = false,
                    Errors = new List<string> { "Error: Invalid rule." },
                    Metadata = string.Empty,
                };
            }
            else if (ruleContent.Contains("complex"))
            {
                return new RuleParseResult
                {
                    IsValid = true,
                    Errors = new List<string>(),
                    Metadata = "contains nested conditions",
                };
            }
            else
            {
                return new RuleParseResult
                {
                    IsValid = true,
                    Errors = new List<string>(),
                    Metadata = "complete metadata",
                };
            }
        }
    }

    public class RuleParseResult
    {
        public bool IsValid { get; set; }
        public List<string>? Errors { get; set; }
        public string? Metadata { get; set; }
    }
}