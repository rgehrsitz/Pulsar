// File: Pulsar.Tests/ComplierTests/CodeGeneratorTests.cs

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Pulsar.Compiler.Generation;
using Pulsar.Compiler.Models;
using Pulsar.Compiler.Validation;
using Pulsar.Runtime.Buffers;
using Pulsar.Runtime.Rules;
using Serilog;
using Xunit;
using Xunit.Abstractions;

namespace Pulsar.Tests.CompilerTests
{
    public class CodeGeneratorTests
    {

        private readonly ITestOutputHelper _output;

        public CodeGeneratorTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void GenerateCSharp_ProducesValidAotCompatibleCode()
        {
            // Arrange
            var rules = new List<RuleDefinition>
    {
        new RuleDefinition
        {
            Name = "TemperatureConversion",
            Description = "Converts F to C",
            SourceInfo = new SourceInfo
            {
                FileName = "test.yaml",
                LineNumber = 1
            },
            Conditions = new ConditionGroup
            {
                All = new List<ConditionDefinition>
                {
                    new ComparisonCondition
                    {
                        Type = ConditionType.Comparison,
                        Sensor = "temperature_f",
                        Operator = ComparisonOperator.GreaterThan,
                        Value = -459.67 // Absolute zero in F
                    }
                }
            },
            Actions = new List<ActionDefinition>
            {
                new SetValueAction
                {
                    Type = ActionType.SetValue,
                    Key = "temperature_c",
                    ValueExpression = "(temperature_f - 32) * 5/9"
                }
            }
        }
    };

            // Act
            var generatedFiles = CodeGenerator.GenerateCSharp(rules);

            // Debug output for all generated files
            foreach (var file in generatedFiles)
            {
                _output.WriteLine($"\n=== {file.FileName} ===");
                _output.WriteLine(file.Content);
            }

            // Assert
            var layerFile = generatedFiles.FirstOrDefault(f => f.FileName.Contains("Group") || f.FileName.Contains("Layer"));
            Assert.NotNull(layerFile);

            var expectedExpression = "outputs[\"temperature_c\"] = (inputs[\"temperature_f\"] - 32) * 5/9;";
            _output.WriteLine("\nLooking for expression:");
            _output.WriteLine(expectedExpression);

            Assert.Contains(expectedExpression, layerFile.Content);
        }

        [Fact]
        public void GenerateCSharp_SingleRule_GeneratesValidCode()
        {
            // Arrange
            var rule = new RuleDefinition
            {
                Name = "SimpleRule",
                Conditions = new ConditionGroup
                {
                    All = new List<ConditionDefinition>
            {
                new ComparisonCondition
                {
                    Type = ConditionType.Comparison,
                    Sensor = "temperature",
                    Operator = ComparisonOperator.GreaterThan,
                    Value = 100,
                }
            }
                },
                Actions = new List<ActionDefinition>
        {
            new SetValueAction
            {
                Type = ActionType.SetValue,
                Key = "alert",
                Value = 1,
            }
        }
            };

            // Act
            var generatedFiles = CodeGenerator.GenerateCSharp(new List<RuleDefinition> { rule });

            // Debug output for all files
            _output.WriteLine($"\nGenerated {generatedFiles.Count} files:");
            foreach (var file in generatedFiles)
            {
                _output.WriteLine($"\nFile: {file.FileName}");
                _output.WriteLine("Content (first 100 chars):");
                _output.WriteLine(file.Content.Length > 100 ? file.Content.Substring(0, 100) + "..." : file.Content);
            }

            // Get files by type
            var interfaceFile = generatedFiles.FirstOrDefault(f => f.FileName == "ICompiledRules.cs");
            var groupFile = generatedFiles.FirstOrDefault(f => f.FileName.Contains("RuleGroup"));
            var coordinatorFile = generatedFiles.FirstOrDefault(f => f.FileName == "RuleCoordinator.cs");

            _output.WriteLine("\nFound files:");
            _output.WriteLine($"Interface file: {interfaceFile?.FileName ?? "null"}");
            _output.WriteLine($"Group file: {groupFile?.FileName ?? "null"}");
            _output.WriteLine($"Coordinator file: {coordinatorFile?.FileName ?? "null"}");

            // Basic structural assertions

            Assert.NotNull(interfaceFile);
            Assert.NotNull(groupFile);
            Assert.NotNull(coordinatorFile);

            // Interface assertions
            Assert.Contains("public interface ICompiledRules", interfaceFile.Content);
            Assert.Contains("void Evaluate(Dictionary<string, double> inputs, Dictionary<string, double> outputs, RingBufferManager bufferManager)", interfaceFile.Content);

            // Group implementation assertions
            Assert.Contains("SimpleRule", groupFile.Content);
            Assert.Contains("inputs[\"temperature\"] > 100", groupFile.Content);
            Assert.Contains("outputs[\"alert\"] = 1", groupFile.Content);
            Assert.Contains("_logger.Debug(\"Evaluating rule SimpleRule\")", groupFile.Content);
        }

        [Fact]
        public void GenerateCSharp_MultipleRules_GeneratesValidCode()
        {
            // Arrange
            var rules = new List<RuleDefinition>
        {
            CreateRule("Rule1", new[] { "temp1" }, "intermediate"),
            CreateRule("Rule2", new[] { "intermediate" }, "output")
        };

            // Act
            var generatedFiles = CodeGenerator.GenerateCSharp(rules);

            // Debug output for all files
            _output.WriteLine($"\nGenerated {generatedFiles.Count} files:");
            foreach (var file in generatedFiles)
            {
                _output.WriteLine($"\nFile: {file.FileName}");
                _output.WriteLine("Content (first 200 chars):");
                _output.WriteLine(file.Content.Length > 200 ? file.Content.Substring(0, 200) + "..." : file.Content);
            }

            // Assert
            Assert.NotNull(generatedFiles.FirstOrDefault(f => f.FileName == "ICompiledRules.cs"));
            Assert.NotNull(generatedFiles.FirstOrDefault(f => f.FileName.Contains("RuleGroup")));
            Assert.NotNull(generatedFiles.FirstOrDefault(f => f.FileName == "RuleCoordinator.cs"));

            // Verify rule ordering in main file
            var coordinatorFile = generatedFiles.First(f => f.FileName == "RuleCoordinator.cs");
            var layer0Index = coordinatorFile.Content.IndexOf("_group0.EvaluateGroup");
            var layer1Index = coordinatorFile.Content.IndexOf("_group1.EvaluateGroup");

            _output.WriteLine($"\nCoordinator content:");
            _output.WriteLine(coordinatorFile.Content);
            _output.WriteLine($"\nLayer indices:");
            _output.WriteLine($"Group0 index: {layer0Index}");
            _output.WriteLine($"Group1 index: {layer1Index}");

            Assert.True(layer0Index < layer1Index, "Rules not evaluated in dependency order");
        }

        [Fact]
        public void GenerateCSharp_ExpressionCondition_GeneratesValidCode()
        {
            // Arrange
            var rule = new RuleDefinition
            {
                Name = "ExpressionRule",
                Conditions = new ConditionGroup
                {
                    All = new List<ConditionDefinition>
                {
                    new ExpressionCondition
                    {
                        Type = ConditionType.Expression,
                        Expression = "temperature * 1.8 + 32 > 100"
                    }
                }
                },
                Actions = new List<ActionDefinition>
            {
                new SetValueAction
                {
                    Key = "fahrenheit",
                    ValueExpression = "temperature * 1.8 + 32"
                }
            }
            };

            // Act
            var generatedFiles = CodeGenerator.GenerateCSharp(new List<RuleDefinition> { rule });

            // Assert
            var implFile = generatedFiles.First(f => f.FileName.Contains("RuleGroup"));
            Assert.Contains("inputs[\"temperature\"] * 1.8 + 32 > 100", implFile.Content);
            Assert.Contains("outputs[\"fahrenheit\"] = inputs[\"temperature\"] * 1.8 + 32", implFile.Content);
        }

        [Fact]
        public void GenerateCSharp_AnyConditions_GeneratesValidCode()
        {
            // Arrange
            var rule = new RuleDefinition
            {
                Name = "AnyConditionRule",
                Conditions = new ConditionGroup
                {
                    Any = new List<ConditionDefinition>
            {
                new ComparisonCondition
                {
                    Sensor = "temp1",
                    Operator = ComparisonOperator.GreaterThan,
                    Value = 100,
                },
                new ComparisonCondition
                {
                    Sensor = "temp2",
                    Operator = ComparisonOperator.LessThan,
                    Value = 0,
                }
            },
                },
                Actions = new List<ActionDefinition>
        {
            new SetValueAction { Key = "alert", Value = 1 }
        },
            };

            // Act
            var generatedFiles = CodeGenerator.GenerateCSharp(new List<RuleDefinition> { rule });

            // Output all generated files for debugging
            _output.WriteLine("\nGenerated Files:");
            foreach (var file in generatedFiles)
            {
                _output.WriteLine($"--- {file.FileName} ---");
                _output.WriteLine(file.Content);
                _output.WriteLine("---END OF FILE---\n");
            }

            // Find the layer implementation file
            var layerFile = generatedFiles.FirstOrDefault(f =>
                f.FileName.Contains("RuleGroup") &&
                f.Content.Contains("EvaluateGroup"));

            Assert.NotNull(layerFile);

            // Validate the condition
            var fileContent = layerFile.Content;

            // Debug output to help diagnose issues
            _output.WriteLine("\nGenerated code:");
            _output.WriteLine(fileContent);

            // The condition should follow C# conventions:
            // - No unnecessary parentheses around simple comparisons
            // - Parentheses around OR conditions for clarity
            bool conditionFound = System.Text.RegularExpressions.Regex.IsMatch(fileContent,
                @"if\s*\(\(inputs\[""temp1""\]\s*>\s*100\s*\|\|\s*inputs\[""temp2""\]\s*<\s*0\)\)");

            Assert.True(conditionFound,
                $"Expected condition following C# conventions not found. Generated code:\n{fileContent}");
        }

        [Fact]
        public void GenerateCSharp_NestedConditions_GeneratesCorrectCode()
        {
            // Arrange
            var rule = new RuleDefinition
            {
                Name = "NestedRule",
                Conditions = new ConditionGroup
                {
                    All = new List<ConditionDefinition>
            {
                new ComparisonCondition
                {
                    Sensor = "temp1",
                    Operator = ComparisonOperator.GreaterThan,
                    Value = 100,
                },
                new ConditionGroup
                {
                    Any = new List<ConditionDefinition>
                    {
                        new ComparisonCondition
                        {
                            Sensor = "pressure",
                            Operator = ComparisonOperator.LessThan,
                            Value = 950,
                        },
                        new ComparisonCondition
                        {
                            Sensor = "humidity",
                            Operator = ComparisonOperator.GreaterThan,
                            Value = 80,
                        },
                    },
                },
            },
                },
                Actions = new List<ActionDefinition>
        {
            new SetValueAction { Key = "alert", Value = 1 },
        },
            };

            // Debug output for condition structure
            _output.WriteLine("\nRule structure:");
            _output.WriteLine($"Rule name: {rule.Name}");
            _output.WriteLine("Conditions:");
            PrintConditionGroup(rule.Conditions, 1);
            _output.WriteLine("Actions:");
            foreach (var action in rule.Actions)
            {
                _output.WriteLine($"  - {action.GetType().Name}: {((SetValueAction)action).Key} = {((SetValueAction)action).Value}");
            }

            // Act
            var generatedFiles = CodeGenerator.GenerateCSharp(new List<RuleDefinition> { rule });

            // Output all generated files for debugging
            _output.WriteLine("\nGenerated Files:");
            foreach (var file in generatedFiles)
            {
                _output.WriteLine($"--- {file.FileName} ---");
                _output.WriteLine(file.Content);
                _output.WriteLine("---END OF FILE---\n");
            }

            // Find the layer implementation file
            var layerFile = generatedFiles.FirstOrDefault(f =>
                f.FileName.Contains("RuleGroup") &&
                f.Content.Contains("EvaluateGroup"));

            Assert.NotNull(layerFile);

            // Validate the condition
            var fileContent = layerFile.Content;

            // Debug output to help diagnose issues
            _output.WriteLine("\nGenerated code:");
            _output.WriteLine(fileContent);

            // The condition should follow C# conventions:
            // - No unnecessary parentheses around simple comparisons
            // - Parentheses only where needed for operator precedence (around OR conditions)
            bool conditionFound = System.Text.RegularExpressions.Regex.IsMatch(fileContent,
                @"if\s*\(\s*inputs\[""temp1""\]\s*>\s*100\s*&&\s*\(\s*inputs\[""pressure""\]\s*<\s*950\s*\|\|\s*inputs\[""humidity""\]\s*>\s*80\s*\)\)");

            Assert.True(conditionFound,
                $"Expected condition following C# conventions not found. Generated code:\n{fileContent}");

            // Verify the action
            Assert.Contains("outputs[\"alert\"] = 1", fileContent);
        }

        [Fact]
        public void GenerateCSharp_MixedConditions_GeneratesCorrectCode()
        {
            // Arrange
            var rule = new RuleDefinition
            {
                Name = "MixedRule",
                Conditions = new ConditionGroup
                {
                    All = new List<ConditionDefinition>
            {
                new ComparisonCondition
                {
                    Sensor = "temp1",
                    Operator = ComparisonOperator.GreaterThan,
                    Value = 100,
                },
                new ComparisonCondition
                {
                    Sensor = "temp2",
                    Operator = ComparisonOperator.LessThan,
                    Value = 50,
                },
            },
                    Any = new List<ConditionDefinition>
            {
                new ComparisonCondition
                {
                    Sensor = "pressure1",
                    Operator = ComparisonOperator.LessThan,
                    Value = 950,
                },
                new ComparisonCondition
                {
                    Sensor = "pressure2",
                    Operator = ComparisonOperator.GreaterThan,
                    Value = 1100,
                },
            },
                },
                Actions = new List<ActionDefinition>
        {
            new SetValueAction { Key = "alert", Value = 1 },
        },
            };

            // Act
            var generatedFiles = CodeGenerator.GenerateCSharp(new List<RuleDefinition> { rule });

            // Output all generated files for debugging
            _output.WriteLine("\nGenerated Files:");
            foreach (var file in generatedFiles)
            {
                _output.WriteLine($"--- {file.FileName} ---");
                _output.WriteLine(file.Content);
                _output.WriteLine("---END OF FILE---\n");
            }

            // Find the layer implementation file
            var layerFile = generatedFiles.FirstOrDefault(f =>
                f.FileName.Contains("RuleGroup") &&
                f.Content.Contains("EvaluateGroup"));

            Assert.NotNull(layerFile);

            // Validate the condition
            var fileContent = layerFile.Content;

            // Debug output to help diagnose issues
            _output.WriteLine("\nGenerated code:");
            _output.WriteLine(fileContent);

            // The condition should follow C# conventions:
            // - No unnecessary parentheses around simple comparisons
            // - Parentheses only where needed for operator precedence (around OR conditions)
            bool conditionFound = System.Text.RegularExpressions.Regex.IsMatch(fileContent,
                @"if\s*\(\s*inputs\[""temp1""\]\s*>\s*100\s*&&\s*inputs\[""temp2""\]\s*<\s*50\s*&&\s*\(\s*inputs\[""pressure1""\]\s*<\s*950\s*\|\|\s*inputs\[""pressure2""\]\s*>\s*1100\)\)");

            Assert.True(conditionFound,
                $"Expected condition following C# conventions not found. Generated code:\n{fileContent}");

            // Verify the action
            Assert.Contains("outputs[\"alert\"] = 1", fileContent);
        }

        [Fact]
        public void GenerateCSharp_DeepNestedConditions_GeneratesCorrectCode()
        {
            // Arrange
            var rule = new RuleDefinition
            {
                Name = "DeepNestedRule",
                Conditions = new ConditionGroup
                {
                    All = new List<ConditionDefinition>
            {
                new ConditionGroup
                {
                    Any = new List<ConditionDefinition>
                    {
                        new ComparisonCondition
                        {
                            Sensor = "temp1",
                            Operator = ComparisonOperator.GreaterThan,
                            Value = 100,
                        },
                        new ConditionGroup
                        {
                            All = new List<ConditionDefinition>
                            {
                                new ComparisonCondition
                                {
                                    Sensor = "temp2",
                                    Operator = ComparisonOperator.LessThan,
                                    Value = 0,
                                },
                                new ComparisonCondition
                                {
                                    Sensor = "pressure",
                                    Operator = ComparisonOperator.GreaterThan,
                                    Value = 1000,
                                },
                            },
                        },
                    },
                },
                new ConditionGroup
                {
                    All = new List<ConditionDefinition>
                    {
                        new ComparisonCondition
                        {
                            Sensor = "humidity",
                            Operator = ComparisonOperator.GreaterThan,
                            Value = 75,
                        },
                        new ExpressionCondition { Expression = "rate > 5" },
                    },
                },
            },
                },
                Actions = new List<ActionDefinition>
        {
            new SetValueAction { Key = "alert", Value = 1 },
        },
            };

            // Act
            var generatedFiles = CodeGenerator.GenerateCSharp(new List<RuleDefinition> { rule });

            // Output all generated files for debugging
            _output.WriteLine("\nGenerated Files:");
            foreach (var file in generatedFiles)
            {
                _output.WriteLine($"--- {file.FileName} ---");
                _output.WriteLine(file.Content);
                _output.WriteLine("---END OF FILE---\n");
            }

            // Find the layer implementation file
            var layerFile = generatedFiles.FirstOrDefault(f =>
                f.FileName.Contains("RuleGroup") &&
                f.Content.Contains("EvaluateGroup"));

            Assert.NotNull(layerFile);

            // Validate the condition
            var fileContent = layerFile.Content;

            // Debug output to help diagnose issues
            _output.WriteLine("\nGenerated code:");
            _output.WriteLine(fileContent);

            // The condition should follow C# conventions:
            // - No unnecessary parentheses around simple comparisons
            // - Parentheses only where needed for operator precedence (around OR conditions)
            bool conditionFound = System.Text.RegularExpressions.Regex.IsMatch(fileContent,
                @"if\s*\(\s*\(\s*inputs\[""temp1""\]\s*>\s*100\s*\|\|\s*\(\s*inputs\[""temp2""\]\s*<\s*0\s*&&\s*inputs\[""pressure""\]\s*>\s*1000\s*\)\s*\)\s*&&\s*inputs\[""humidity""\]\s*>\s*75\s*&&\s*inputs\[""rate""\]\s*>\s*5\)");

            Assert.True(conditionFound,
                $"Expected condition following C# conventions not found. Generated code:\n{fileContent}");

            // Verify the action
            Assert.Contains("outputs[\"alert\"] = 1", fileContent);
        }

        [Fact]
        public void GenerateCSharp_LayeredRules_GeneratesCorrectEvaluationOrder()
        {
            // Arrange
            var rule1 = CreateRule("InputRule", new[] { "raw_temp" }, "temp");
            var rule2 = CreateRule("ProcessingRule", new[] { "temp" }, "processed_temp");
            var rule3 = CreateRule("AlertRule", new[] { "processed_temp" }, "alert");
            var rules = new List<RuleDefinition> { rule3, rule1, rule2 }; // Intentionally out of order

            // Act
            var generatedFiles = CodeGenerator.GenerateCSharp(rules);

            // Verify interface and coordinator are generated
            var coordinatorFile = generatedFiles.First(f => f.FileName == "RuleCoordinator.cs");
            var interfaceFile = generatedFiles.First(f => f.FileName == "ICompiledRules.cs");
        
            // Verify rule groups are generated in correct order
            var group0File = generatedFiles.First(f => f.FileName == "RuleGroup0.cs");
            var group1File = generatedFiles.First(f => f.FileName == "RuleGroup1.cs");
            var group2File = generatedFiles.First(f => f.FileName == "RuleGroup2.cs");

            // Assert coordinator implements interface and evaluates groups in order
            Assert.Contains("public class RuleCoordinator : IRuleCoordinator", coordinatorFile.Content);
            Assert.Contains("public void Evaluate(Dictionary<string, double> inputs, Dictionary<string, double> outputs, RingBufferManager bufferManager)", coordinatorFile.Content);
        
            // Verify evaluation order in coordinator
            var evalLines = coordinatorFile.Content
                .Split('\n')
                .Where(l => l.Contains(".EvaluateGroup("))
                .ToList();
        
            Assert.Equal(3, evalLines.Count);
            Assert.Contains("_group0.EvaluateGroup", evalLines[0]);
            Assert.Contains("_group1.EvaluateGroup", evalLines[1]);
            Assert.Contains("_group2.EvaluateGroup", evalLines[2]);

            // Verify rules are in correct groups
            Assert.Contains("Rule: InputRule", group0File.Content);
            Assert.Contains("Rule: ProcessingRule", group1File.Content);
            Assert.Contains("Rule: AlertRule", group2File.Content);

            // Verify rule dependencies through inputs/outputs
            Assert.Contains("inputs[\"raw_temp\"]", group0File.Content);
            Assert.Contains("outputs[\"temp\"]", group0File.Content);
        
            Assert.Contains("inputs[\"temp\"]", group1File.Content);
            Assert.Contains("outputs[\"processed_temp\"]", group1File.Content);
        
            Assert.Contains("inputs[\"processed_temp\"]", group2File.Content);
            Assert.Contains("outputs[\"alert\"]", group2File.Content);
        }

        [Fact]
        public void GenerateCSharp_ParallelRules_GeneratesInSameLayer()
        {
            // Arrange
            var rule1 = CreateRule("TempRule", new[] { "raw_temp" }, "temp1");
            var rule2 = CreateRule("PressureRule", new[] { "raw_pressure" }, "pressure1");
            var rules = new List<RuleDefinition> { rule1, rule2 };

            // Act
            var generatedFiles = CodeGenerator.GenerateCSharp(rules);

            // Assert
            Assert.Contains(generatedFiles, f => f.FileName == "RuleCoordinator.cs");
            Assert.Contains(generatedFiles, f => f.FileName == "RuleGroup0.cs");
            Assert.Contains(generatedFiles, f => f.FileName == "ICompiledRules.cs");

            var coordinatorFile = generatedFiles.First(f => f.FileName == "RuleCoordinator.cs");
            var group0File = generatedFiles.First(f => f.FileName == "RuleGroup0.cs");
            var interfaceFile = generatedFiles.First(f => f.FileName == "ICompiledRules.cs");

            // Verify interface
            Assert.Contains("void Evaluate(Dictionary<string, double> inputs, Dictionary<string, double> outputs, RingBufferManager bufferManager)", interfaceFile.Content);

            // Verify coordinator
            Assert.Contains("public void Evaluate(Dictionary<string, double> inputs, Dictionary<string, double> outputs, RingBufferManager bufferManager)", coordinatorFile.Content);
            Assert.Contains("_group0.EvaluateGroup(inputs, outputs, bufferManager);", coordinatorFile.Content);
            // Since rules are parallel, they should be in the same group (group0)
            Assert.DoesNotContain("_group1", coordinatorFile.Content);

            // Verify both rules are in the same group
            Assert.Contains("Rule: TempRule", group0File.Content);
            Assert.Contains("Rule: PressureRule", group0File.Content);
        
            // Verify group has necessary imports
            Assert.Contains("using System;", group0File.Content);
            Assert.Contains("using System.Collections.Generic;", group0File.Content);
            Assert.Contains("using System.Linq;", group0File.Content);
            Assert.Contains("using Serilog;", group0File.Content);
            Assert.Contains("using Prometheus;", group0File.Content);
            Assert.Contains("using Pulsar.Runtime.Buffers;", group0File.Content);
            Assert.Contains("using Pulsar.Runtime.Common;", group0File.Content);
        }

        [Fact]
        public void GenerateCSharp_CyclicDependency_ThrowsException()
        {
            // Arrange
            var rule1 = CreateRule("Rule1", new[] { "value2" }, "value1");
            var rule2 = CreateRule("Rule2", new[] { "value1" }, "value2");
            var rules = new List<RuleDefinition> { rule1, rule2 };

            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(
                () => CodeGenerator.GenerateCSharp(rules)
            );
            Assert.Contains("Cyclic dependency", exception.Message);
        }

        [Fact]
        public void GenerateCSharp_ComplexDependencyGraph_GeneratesCorrectLayers()
        {
            // Arrange
            var rules = new List<RuleDefinition>
            {
                CreateRule("InputProcessing1", new[] { "raw1" }, "processed1"),
                CreateRule("InputProcessing2", new[] { "raw2" }, "processed2"),
                CreateRule("Aggregation", new[] { "processed1", "processed2" }, "aggregate"),
                CreateRule("Alert1", new[] { "aggregate" }, "alert1"),
                CreateRule("Alert2", new[] { "aggregate" }, "alert2"),
                CreateRule("FinalAlert", new[] { "alert1", "alert2" }, "final_alert"),
            };

            // Act
            var generatedFiles = CodeGenerator.GenerateCSharp(rules);

            // Assert - verify all required files are generated
            var coordinatorFile = generatedFiles.First(f => f.FileName == "RuleCoordinator.cs");
            var group0File = generatedFiles.First(f => f.FileName == "RuleGroup0.cs");
            var group1File = generatedFiles.First(f => f.FileName == "RuleGroup1.cs");
            var group2File = generatedFiles.First(f => f.FileName == "RuleGroup2.cs");
            var group3File = generatedFiles.First(f => f.FileName == "RuleGroup3.cs");

            // Verify coordinator evaluates groups in correct order
            var evalLines = coordinatorFile.Content
                .Split('\n')
                .Where(l => l.Contains(".EvaluateGroup("))
                .ToList();
        
            Assert.Equal(4, evalLines.Count);
            Assert.Contains("_group0.EvaluateGroup", evalLines[0]);
            Assert.Contains("_group1.EvaluateGroup", evalLines[1]);
            Assert.Contains("_group2.EvaluateGroup", evalLines[2]);
            Assert.Contains("_group3.EvaluateGroup", evalLines[3]);

            // Verify rules are in correct groups based on dependencies
        
            // Group 0 - Input processing (parallel rules)
            Assert.Contains("Rule: InputProcessing1", group0File.Content);
            Assert.Contains("Rule: InputProcessing2", group0File.Content);
            Assert.Contains("inputs[\"raw1\"]", group0File.Content);
            Assert.Contains("inputs[\"raw2\"]", group0File.Content);
            Assert.Contains("outputs[\"processed1\"]", group0File.Content);
            Assert.Contains("outputs[\"processed2\"]", group0File.Content);

            // Group 1 - Aggregation (depends on processed1 and processed2)
            Assert.Contains("Rule: Aggregation", group1File.Content);
            Assert.Contains("inputs[\"processed1\"]", group1File.Content);
            Assert.Contains("inputs[\"processed2\"]", group1File.Content);
            Assert.Contains("outputs[\"aggregate\"]", group1File.Content);

            // Group 2 - Initial alerts (parallel rules depending on aggregate)
            Assert.Contains("Rule: Alert1", group2File.Content);
            Assert.Contains("Rule: Alert2", group2File.Content);
            Assert.Contains("inputs[\"aggregate\"]", group2File.Content);
            Assert.Contains("outputs[\"alert1\"]", group2File.Content);
            Assert.Contains("outputs[\"alert2\"]", group2File.Content);

            // Group 3 - Final alert (depends on both alert1 and alert2)
            Assert.Contains("Rule: FinalAlert", group3File.Content);
            Assert.Contains("inputs[\"alert1\"]", group3File.Content);
            Assert.Contains("inputs[\"alert2\"]", group3File.Content);
            Assert.Contains("outputs[\"final_alert\"]", group3File.Content);
        }

        [Fact]
        public void GenerateCSharp_WithDependencies_MaintainsOrderInGroups()
        {
            // Arrange
            var rule1 = CreateRule("Rule1", new[] { "input" }, "intermediate1");
            var rule2 = CreateRule("Rule2", new[] { "intermediate1" }, "intermediate2");
            var rule3 = CreateRule("Rule3", new[] { "intermediate2" }, "output");

            var config = new RuleGroupingConfig
            {
                MaxRulesPerFile = 2,  // Force splitting into multiple groups
                GroupParallelRules = true
            };

            // Act
            var generatedFiles = CodeGenerator.GenerateCSharp(new[] { rule1, rule2, rule3 }.ToList(), config);
            var coordinator = generatedFiles.First(f => f.FileName == "RuleCoordinator.cs");

            // Assert
            var groups = generatedFiles.Where(f => f.FileName != "RuleCoordinator.cs")
                                      .OrderBy(f => f.LayerRange.Start)
                                      .ToList();

            // Verify layer ordering
            for (int i = 1; i < groups.Count; i++)
            {
                Assert.True(groups[i - 1].LayerRange.End <= groups[i].LayerRange.Start,
                    "Groups are not properly ordered by layer");
            }

            // Verify coordinator calls methods in correct order
            var coordinatorContent = coordinator.Content;
            var evaluationLines = coordinatorContent
                .Split('\n')
                .Where(l => l.Contains("_group") && l.Contains("EvaluateGroup"))
                .Select(l => l.Trim())
                .ToList();

            // Debug output
            foreach (var line in evaluationLines)
            {
                Console.WriteLine($"Evaluation line: {line}");
            }

            for (int i = 1; i < evaluationLines.Count; i++)
            {
                var prevLine = evaluationLines[i - 1];
                var currentLine = evaluationLines[i];
                var prevMatch = System.Text.RegularExpressions.Regex.Match(prevLine, @"_group(\d+)\.");
                var currentMatch = System.Text.RegularExpressions.Regex.Match(currentLine, @"_group(\d+)\.");

                if (!prevMatch.Success || !currentMatch.Success)
                {
                    Assert.Fail($"Could not extract group numbers from lines: '{prevLine}' and '{currentLine}'");
                }

                var prevGroupNum = int.Parse(prevMatch.Groups[1].Value);
                var currentGroupNum = int.Parse(currentMatch.Groups[1].Value);

                Assert.True(prevGroupNum <= currentGroupNum,
                    "Coordinator is not calling groups in dependency order");
            }
        }

        [Fact]
        public void GenerateCSharp_WithParallelRules_GroupsCorrectly()
        {
            // Arrange
            var rules = new List<RuleDefinition>
            {
                CreateRule("ParallelRule1", new[] { "input1" }, "output1"),
                CreateRule("ParallelRule2", new[] { "input2" }, "output2"),
                CreateRule("ParallelRule3", new[] { "input3" }, "output3"),
                CreateRule("DependentRule", new[] { "output1", "output2" }, "finalOutput")
            };

            var config = new RuleGroupingConfig
            {
                MaxRulesPerFile = 2,
                GroupParallelRules = true
            };

            // Act
            var generatedFiles = CodeGenerator.GenerateCSharp(rules, config);

            // Debug output
            _output.WriteLine("\nGenerated files:");
            foreach (var file in generatedFiles)
            {
                _output.WriteLine($"\n--- {file.FileName} ---");
                _output.WriteLine(file.Content);
            }

            // Assert
            var ruleFiles = generatedFiles.Where(f => f.FileName != "RuleCoordinator.cs" && f.FileName != "ICompiledRules.cs" && f.FileName != "rules.manifest.json").ToList();
            var manifestFile = generatedFiles.First(f => f.FileName == "rules.manifest.json");

            // Debug output for rule groups
            _output.WriteLine("\nRule groups:");
            foreach (var file in ruleFiles)
            {
                var ruleNames = file.Content.Split("Rule:").Skip(1).Select(r => r.Split('\n')[0].Trim()).ToList();
                _output.WriteLine($"{file.FileName}: {string.Join(", ", ruleNames)}");
            }

            // Debug manifest content
            _output.WriteLine("\nManifest content:");
            _output.WriteLine(manifestFile.Content);

            // Verify parallel rules are grouped together when possible
            var firstGroupContent = ruleFiles.First().Content;
            Assert.True(
                firstGroupContent.Contains("ParallelRule1") &&
                firstGroupContent.Contains("ParallelRule2") ||
                firstGroupContent.Contains("ParallelRule2") &&
                firstGroupContent.Contains("ParallelRule3") ||
                firstGroupContent.Contains("ParallelRule1") &&
                firstGroupContent.Contains("ParallelRule3"),
                "Parallel rules were not grouped together"
            );

            // Verify dependent rule is in a later group
            var lastGroupContent = ruleFiles.Last().Content;
            Assert.Contains("DependentRule", lastGroupContent);
        }

        [Fact]
        public void GenerateCSharp_WithLargeRules_RespectsMaxLinesPerFile()
        {
            // Arrange
            var rule1 = new RuleDefinition
            {
                Name = "LargeRule1",
                Conditions = new ConditionGroup
                {
                    All = Enumerable.Range(0, 20).Select(i =>
                        new ComparisonCondition
                        {
                            Type = ConditionType.Comparison,
                            Sensor = $"input{i}",
                            Operator = ComparisonOperator.GreaterThan,
                            Value = i
                        } as ConditionDefinition).ToList()
                },
                Actions = new List<ActionDefinition>
        {
            new SetValueAction
            {
                Type = ActionType.SetValue,
                Key = "output1",
                Value = 1
            }
        }
            };

            var rule2 = new RuleDefinition
            {
                Name = "LargeRule2",
                Conditions = new ConditionGroup
                {
                    All = Enumerable.Range(0, 20).Select(i =>
                        new ComparisonCondition
                        {
                            Type = ConditionType.Comparison,
                            Sensor = $"input{i}",
                            Operator = ComparisonOperator.LessThan,
                            Value = i
                        } as ConditionDefinition).ToList()
                },
                Actions = new List<ActionDefinition>
        {
            new SetValueAction
            {
                Type = ActionType.SetValue,
                Key = "output2",
                Value = 2
            }
        }
            };

            var config = new RuleGroupingConfig
            {
                MaxRulesPerFile = 10,
                MaxLinesPerFile = 100  // Set small to force splitting
            };

            // Act
            var generatedFiles = CodeGenerator.GenerateCSharp(new[] { rule1, rule2 }.ToList(), config);
            var ruleFiles = generatedFiles.Where(f => f.FileName != "RuleCoordinator.cs").ToList();

            // Assert
            Assert.True(ruleFiles.All(f =>
                f.Content.Split('\n').Length <= config.MaxLinesPerFile),
                "Some files exceed MaxLinesPerFile");
        }

        // Helper method for creating test rules
        private static RuleDefinition CreateRule(string name, string[] inputs, string output)
        {
            var conditions = new List<ConditionDefinition>();
            foreach (var input in inputs)
            {
                conditions.Add(
                    new ComparisonCondition
                    {
                        Type = ConditionType.Comparison,
                        Sensor = input,
                        Operator = ComparisonOperator.GreaterThan,
                        Value = 0,
                    }
                );
            }

            return new RuleDefinition
            {
                Name = name,
                Conditions = new ConditionGroup { All = conditions },
                Actions = new List<ActionDefinition>
                {
                    new SetValueAction
                    {
                        Type = ActionType.SetValue,
                        Key = output,
                        Value = 1,
                    }
                }
            };
        }

        private void PrintConditionGroup(ConditionGroup group, int indent)
        {
            var prefix = new string(' ', indent * 2);
            if (group.All?.Any() == true)
            {
                _output.WriteLine($"{prefix}ALL:");
                foreach (var condition in group.All)
                {
                    if (condition is ConditionGroup nestedGroup)
                    {
                        PrintConditionGroup(nestedGroup, indent + 1);
                    }
                    else if (condition is ComparisonCondition comp)
                    {
                        _output.WriteLine($"{prefix}  - {comp.Sensor} {comp.Operator} {comp.Value}");
                    }
                }
            }
            if (group.Any?.Any() == true)
            {
                _output.WriteLine($"{prefix}ANY:");
                foreach (var condition in group.Any)
                {
                    if (condition is ConditionGroup nestedGroup)
                    {
                        PrintConditionGroup(nestedGroup, indent + 1);
                    }
                    else if (condition is ComparisonCondition comp)
                    {
                        _output.WriteLine($"{prefix}  - {comp.Sensor} {comp.Operator} {comp.Value}");
                    }
                }
            }
        }
    }
}
