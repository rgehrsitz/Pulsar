using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Xunit;
using Pulsar.Compiler.Generation;
using Pulsar.Compiler.Models;
using Pulsar.Compiler.Config;

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
                                Value = 42.0
                            }
                        }
                    },
                    Actions = new List<ActionDefinition>
                    {
                        new SetValueAction
                        {
                            Type = ActionType.SetValue,
                            Key = "output1",
                            Value = 1.0
                        }
                    }
                }
            };

            var buildConfig = new BuildConfig
            {
                OutputPath = _testOutputPath,
                Target = "executable",
                ProjectName = "Beacon",
                TargetFramework = "net6.0",
                RulesPath = Path.Combine(_testOutputPath, "rules"),
                StandaloneExecutable = true,
                Namespace = "Beacon.Runtime.Generated"
            };

            // Act
            var generatedFiles = _generator.GenerateAllFiles(rules, buildConfig);

            // Assert
            Assert.NotNull(generatedFiles);
            
            // Verify solution file
            var solutionFile = generatedFiles.FirstOrDefault(f => f.FileName == "Beacon.sln");
            Assert.NotNull(solutionFile);
            Assert.Contains("Project(\"{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}\") = \"Beacon.Runtime\"", solutionFile.Content);
            Assert.Contains("Project(\"{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}\") = \"Beacon.Tests\"", solutionFile.Content);

            // Verify Runtime project file
            var runtimeProject = generatedFiles.FirstOrDefault(f => f.FileName == "Beacon.Runtime.csproj");
            Assert.NotNull(runtimeProject);
            Assert.Contains("<OutputType>Exe</OutputType>", runtimeProject.Content);
            Assert.Contains("<TargetFramework>net6.0</TargetFramework>", runtimeProject.Content);

            // Verify Tests project file
            var testsProject = generatedFiles.FirstOrDefault(f => f.FileName == "Beacon.Tests.csproj");
            Assert.NotNull(testsProject);
            Assert.Contains("<IsTestProject>true</IsTestProject>", testsProject.Content);

            // Verify namespace in generated code files
            var codeFiles = generatedFiles.Where(f => f.FileName.EndsWith(".cs"));
            foreach (var file in codeFiles)
            {
                if (!string.IsNullOrEmpty(file.Namespace))
                {
                    Assert.Equal("Beacon.Runtime.Generated", file.Namespace);
                }
            }
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
                Namespace = "Beacon.Runtime.Generated"
            };

            // Act
            var generatedFiles = _generator.GenerateAllFiles(rules, buildConfig);

            // Assert
            // Verify basic solution structure
            Assert.Contains(generatedFiles, f => f.FileName == "Beacon.sln");
            Assert.Contains(generatedFiles, f => f.FileName == "Beacon.Runtime.csproj");
            Assert.Contains(generatedFiles, f => f.FileName == "Beacon.Tests.csproj");

            // Write files to disk and verify they can be loaded
            foreach (var file in generatedFiles)
            {
                var filePath = Path.Combine(_testOutputPath, file.FileName);
                File.WriteAllText(filePath, file.Content);
                Assert.True(File.Exists(filePath));
            }

            // Verify solution file can be parsed
            var solutionPath = Path.Combine(_testOutputPath, "Beacon.sln");
            Assert.True(File.Exists(solutionPath));
            var solutionContent = File.ReadAllText(solutionPath);
            Assert.Contains("Microsoft Visual Studio Solution File", solutionContent);
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
