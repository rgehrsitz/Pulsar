// File: Pulsar.Tests/Compilation/CompilationTests.cs


using Pulsar.Compiler.Core;
using Pulsar.Compiler.Models;
using Serilog;

namespace Pulsar.Tests.Compilation
{
    public class CompilationTests
    {
        private readonly ILogger _logger = Pulsar.Tests.TestUtilities.LoggingConfig.ToSerilogLogger(
            Pulsar.Tests.TestUtilities.LoggingConfig.GetLogger()
        );

        [Fact]
        public void Compilation_ValidRules_GeneratesValidOutput()
        {
            _logger.Debug("Starting valid rules compilation test");

            // Arrange

            var rules = new[]
            {
                new RuleDefinition
                {
                    Name = "TestRule",

                    Description = "Test rule for compilation",

                    Conditions = new ConditionGroup
                    {
                        All = new List<ConditionDefinition>
                        {
                            new ComparisonCondition
                            {
                                Type = ConditionType.Comparison,

                                Sensor = "temp",

                                Operator = ComparisonOperator.GreaterThan,

                                Value = 30.0,
                            },
                        },
                    },

                    Actions = new List<ActionDefinition>
                    {
                        new SetValueAction
                        {
                            Type = ActionType.SetValue,

                            Key = "output",

                            Value = 1.0,
                        },
                    },
                },
            };

            var options = new CompilerOptions
            {
                BuildConfig = new Pulsar.Compiler.Config.BuildConfig
                {
                    OutputPath = "TestOutput",

                    Target = "win-x64",

                    ProjectName = "TestProject",

                    TargetFramework = "net9.0",

                    RulesPath = "TestRules",

                    GenerateDebugInfo = true,
                },
            };

            var compiler = new AOTRuleCompiler();

            // Act

            var result = compiler.Compile(rules, options);

            // Assert

            Assert.True(result.Success);

            Assert.NotNull(result.GeneratedFiles);

            Assert.NotEmpty(result.GeneratedFiles);

            _logger.Debug("Valid rules compilation test completed successfully");
        }

        [Fact]
        public void Compilation_InvalidRules_ReturnsErrors()
        {
            _logger.Debug("Starting invalid rules compilation test");

            // Arrange

            var rules = new[]
            {
                new RuleDefinition(), // Empty rule with no name or actions
            };

            var options = new CompilerOptions
            {
                BuildConfig = new Pulsar.Compiler.Config.BuildConfig
                {
                    OutputPath = "TestOutput",

                    Target = "win-x64",

                    ProjectName = "TestProject",

                    TargetFramework = "net9.0",

                    RulesPath = "TestRules",

                    GenerateDebugInfo = true,
                },
            };

            var compiler = new AOTRuleCompiler();

            // Act

            var result = compiler.Compile(rules, options);

            // Assert

            Assert.False(result.Success);

            Assert.NotNull(result.Errors);

            Assert.NotEmpty(result.Errors);

            _logger.Debug("Invalid rules compilation test completed with expected errors");
        }
    }
}
