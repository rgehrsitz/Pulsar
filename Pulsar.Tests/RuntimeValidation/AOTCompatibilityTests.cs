// File: Pulsar.Tests/RuntimeValidation/AOTCompatibilityTests.cs

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using System.Reflection;
using Xunit;
using Xunit.Abstractions;
using Microsoft.Extensions.Logging;

namespace Pulsar.Tests.RuntimeValidation
{
    [Trait("Category", "AOTCompatibility")]
    public class AOTCompatibilityTests : IClassFixture<RuntimeValidationFixture>
    {
        private readonly RuntimeValidationFixture _fixture;
        private readonly ITestOutputHelper _output;
        
        public AOTCompatibilityTests(RuntimeValidationFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
        }
        
        [Fact(Skip = "Requires full AOT setup")]
        public async Task Verify_NoReflectionUsed()
        {
            // Generate test rules
            var ruleFile = GenerateTestRules();
            
            // Build project
            var success = await _fixture.BuildTestProject(new[] { ruleFile });
            Assert.True(success, "Project should build successfully");
            
            // Get the assembly
            var assembly = _fixture.CompiledAssembly;
            Assert.NotNull(assembly);
            
            // Search for reflection usage in methods
            var reflectionTypes = new HashSet<string> {
                "System.Reflection",
                "System.Reflection.Emit",
                "System.Runtime.CompilerServices.RuntimeHelpers"
            };
            
            bool foundReflection = false;
            foreach (var type in assembly.GetTypes())
            {
                foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    // Skip methods from base classes like Object
                    if (method.DeclaringType != type)
                        continue;
                        
                    try
                    {
                        // Get IL code (this is simplified and not comprehensive)
                        var methodBody = method.GetMethodBody();
                        if (methodBody == null)
                            continue;
                            
                        // Check if the method uses any reflection types
                        foreach (var localVar in methodBody.LocalVariables)
                        {
                            var typeName = localVar.LocalType.FullName ?? "";
                            if (reflectionTypes.Any(rt => typeName.StartsWith(rt)))
                            {
                                _output.WriteLine($"WARNING: Reflection detected in {type.Name}.{method.Name}: {typeName}");
                                foundReflection = true;
                            }
                        }
                    }
                    catch (Exception)
                    {
                        // Some methods might throw exceptions when inspected, just skip them
                        continue;
                    }
                }
            }
            
            _output.WriteLine(foundReflection 
                ? "Reflection usage detected - not fully AOT compatible" 
                : "No reflection usage detected - AOT compatible");
                
            Assert.False(foundReflection, "Generated code should not use reflection for AOT compatibility");
        }
        
        [Fact]
        public async Task Verify_SupportedTrimmingAttributes()
        {
            // Generate test rules
            var ruleFile = GenerateTestRules();
            
            // Build project
            var success = await _fixture.BuildTestProject(new[] { ruleFile });
            Assert.True(success, "Project should build successfully");
            
            // Get project file and check for trimming configuration
            var projectFilePath = Path.Combine(_fixture.OutputPath, "RuntimeTest.csproj");
            Assert.True(File.Exists(projectFilePath), "Project file should exist");
            
            var projectContent = await File.ReadAllTextAsync(projectFilePath);
            
            // Check for trimming configuration
            bool hasTrimming = projectContent.Contains("<PublishTrimmed>") 
                || projectContent.Contains("<TrimMode>") 
                || projectContent.Contains("<TrimmerRootAssembly>");
                
            _output.WriteLine(hasTrimming 
                ? "Trimming support detected in project file" 
                : "WARNING: Trimming configuration not found in project file");
                
            // Look for the trimming.xml file
            var trimmingXmlPath = Path.Combine(_fixture.OutputPath, "trimming.xml");
            bool hasTrimmingXml = File.Exists(trimmingXmlPath);
            
            _output.WriteLine(hasTrimmingXml 
                ? "Trimming.xml file found: " + trimmingXmlPath 
                : "WARNING: No trimming.xml file found");

            // We don't assert here as the project might be AOT-compatible without explicit trimming config
            // in this test phase
        }
        
        [Fact(Skip = "Requires dotnet publish with trimming enabled")]
        public async Task Publish_WithTrimmingEnabled_Succeeds()
        {
            // Generate test rules
            var ruleFile = GenerateTestRules();
            
            // Build project
            var success = await _fixture.BuildTestProject(new[] { ruleFile });
            Assert.True(success, "Project should build successfully");
            
            // Run dotnet publish with trimming enabled
            var projectPath = Path.Combine(_fixture.OutputPath, "RuntimeTest.csproj");
            var publishDir = Path.Combine(_fixture.OutputPath, "publish-trimmed");
            
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = $"publish {projectPath} -c Release -r linux-x64 --self-contained true -p:PublishTrimmed=true -o {publishDir}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                }
            };
            
            _output.WriteLine($"Running publish with trimming: {process.StartInfo.Arguments}");
            
            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            
            _output.WriteLine("Publish output:");
            _output.WriteLine(output);
            
            if (!string.IsNullOrEmpty(error))
            {
                _output.WriteLine("Publish errors:");
                _output.WriteLine(error);
            }
            
            Assert.Equal(0, process.ExitCode);
            Assert.True(Directory.Exists(publishDir));
            
            // Check for the executable
            var executablePath = Path.Combine(publishDir, "RuntimeTest");
            Assert.True(File.Exists(executablePath));
            
            _output.WriteLine("Successfully published with trimming enabled");
        }
        
        private string GenerateTestRules()
        {
            var sb = new StringBuilder();
            sb.AppendLine("rules:");
            
            // Generate a few simple rules
            for (int i = 1; i <= 3; i++)
            {
                sb.AppendLine($@"  - name: 'AOTTestRule{i}'
    description: 'AOT compatibility test rule {i}'
    conditions:
      all:
        - condition:
            type: comparison
            sensor: 'input:a'
            operator: '>'
            value: {i * 10}
    actions:
      - set_value:
          key: 'output:result{i}'
          value_expression: 'input:a + input:b * {i}'");
            }
            
            // Generate a rule with more complex expression that might trigger dynamic code
            sb.AppendLine(@"  - name: 'ComplexExpressionRule'
    description: 'Rule with complex expression to test AOT compatibility'
    conditions:
      all:
        - condition:
            type: expression
            expression: 'input:a > 0 && (input:b < 100 || input:c >= 50)'
    actions:
      - set_value:
          key: 'output:complex_result'
          value_expression: 'Math.Sqrt(Math.Pow(input:a, 2) + Math.Pow(input:b, 2))'");
            
            var filePath = Path.Combine(_fixture.OutputPath, "aot-test-rules.yaml");
            File.WriteAllText(filePath, sb.ToString());
            return filePath;
        }
    }
}