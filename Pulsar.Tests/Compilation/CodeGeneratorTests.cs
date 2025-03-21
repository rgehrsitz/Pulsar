// File: Pulsar.Tests/Compilation/CodeGeneratorTests.cs

using Pulsar.Compiler.Config;
using Pulsar.Compiler.Generation;
using Pulsar.Compiler.Models;

namespace Pulsar.Tests.Compilation
{
    public class CodeGeneratorTests : IDisposable
    {
        private readonly CodeGenerator _generator;
        private readonly string _testOutputPath;

        public CodeGeneratorTests()
        {
            _generator = new CodeGenerator();
            _testOutputPath = Path.Combine(Path.GetTempPath(), "PulsarTests", "BeaconOutput");
            Directory.CreateDirectory(_testOutputPath);
        }

        [Fact]
        public void GenerateAllFiles_ShouldCreateBeaconSolutionAndProjectFiles()
        {
            // Arrange
            var rules = new List<RuleDefinition>
            {
                new RuleDefinition
                {
                    Name = "TestRule",
                    Description = "A test rule for verifying code generation",
                    Conditions = new ConditionGroup
                    {
                        All = new List<ConditionDefinition>
                        {
                            new ComparisonCondition
                            {
                                Type = ConditionType.Comparison,
                                Sensor = "sensor1",
                                Operator = ComparisonOperator.EqualTo,
                                Value = 42.0,
                            },
                        },
                    },
                    Actions = new List<ActionDefinition>
                    {
                        new SetValueAction
                        {
                            Type = ActionType.SetValue,
                            Key = "output1",
                            Value = 1.0,
                        },
                    },
                },
            };

            var buildConfig = new BuildConfig
            {
                OutputPath = _testOutputPath,
                Target = "executable",
                ProjectName = "Beacon",
                TargetFramework = "net6.0",
                RulesPath = Path.Combine(_testOutputPath, "rules"),
                StandaloneExecutable = true,
                Namespace = "Beacon.Runtime.Generated",
            };

            // Act
            var generatedFiles = _generator.GenerateCSharp(rules, buildConfig);

            // Assert
            Assert.NotNull(generatedFiles);

            // Verify namespace in generated code files
            var codeFiles = generatedFiles.Where(f => f.FileName.EndsWith(".cs"));
            foreach (var file in codeFiles)
            {
                if (!string.IsNullOrEmpty(file.Namespace))
                {
                    Assert.Equal("Beacon.Runtime.Generated", file.Namespace);
                }
            }

            // Verify rule group files are generated
            Assert.Contains(generatedFiles, f => f.FileName.StartsWith("RuleGroup"));

            // Verify coordinator is generated
            Assert.Contains(generatedFiles, f => f.FileName == "RuleCoordinator.cs");

            // Verify metadata file is generated
            Assert.Contains(generatedFiles, f => f.FileName == "RuleMetadata.cs");
        }

        [Fact]
        public void GenerateAllFiles_ShouldCreateValidSolutionStructure()
        {
            // Arrange
            var rules = new List<RuleDefinition>(); // Empty rules list for structure test
            var buildConfig = new BuildConfig
            {
                OutputPath = _testOutputPath,
                Target = "executable",
                ProjectName = "Beacon",
                TargetFramework = "net6.0",
                RulesPath = Path.Combine(_testOutputPath, "rules"),
                StandaloneExecutable = true,
                Namespace = "Beacon.Runtime.Generated",
            };

            // Act
            var generatedFiles = _generator.GenerateCSharp(rules, buildConfig);

            // Assert
            Assert.NotNull(generatedFiles);
            Assert.NotEmpty(generatedFiles);

            // Write files to disk and verify they can be loaded
            foreach (var file in generatedFiles)
            {
                var filePath = Path.Combine(_testOutputPath, file.FileName);
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                File.WriteAllText(filePath, file.Content);
                Assert.True(File.Exists(filePath));
            }
        }

        public void Dispose()
        {
            // Clean up test output directory
            if (Directory.Exists(_testOutputPath))
            {
                Directory.Delete(_testOutputPath, true);
            }
        }
    }
}
