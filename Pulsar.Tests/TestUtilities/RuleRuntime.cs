// File: Pulsar.Tests/TestUtilities/RuleRuntime.cs

namespace Pulsar.Tests.TestUtilities
{
    public static class RuleRuntime
    {
        public static ExecutionResult Execute(string ruleContent)
        {
            // Simulated runtime logic for executing a rule
            if (ruleContent.Contains("invalid"))
            {
                return new ExecutionResult
                {
                    IsSuccess = false,
                    Errors = new List<string> { "Runtime error: Invalid rule execution." },
                    Output = string.Empty,
                };
            }
            return new ExecutionResult
            {
                IsSuccess = true,
                Errors = new List<string>(),
                Output = "Execution complete",
            };
        }
    }

    public class ExecutionResult
    {
        public bool IsSuccess { get; set; }
        public List<string>? Errors { get; set; }
        public string? Output { get; set; }
    }
}