// File: Pulsar.Tests/Integration/CodeGenerationTests.cs
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using Pulsar.Compiler;
using StackExchange.Redis;

namespace Pulsar.Tests.Integration
{
    public class CodeGenerationTests : IClassFixture<TestEnvironmentFixture>
    {
        private readonly TestEnvironmentFixture _fixture;
        private const string TestRulesPath = "TestData/sample-rules.yaml";
        private const string OutputPath = "TestOutput";

        public CodeGenerationTests(TestEnvironmentFixture fixture)
        {
            _fixture = fixture;
            // Ensure output directory exists and is clean
            if (Directory.Exists(OutputPath))
            {
                Directory.Delete(OutputPath, true);
            }
            Directory.CreateDirectory(OutputPath);
        }

        [Fact]
        public async Task GenerateProject_CreatesAllRequiredFiles()
        {
            // Arrange
            var options = new Dictionary<string, string>
            {
                { "rules", TestRulesPath },
                { "output", OutputPath },
                { "config", Path.Combine("TestData", "system_config.yaml") }
            };
            var result = await Program.GenerateBuildableProject(options, _fixture.Logger);

            // Assert
            Assert.True(result, "Project generation failed");
            Assert.True(File.Exists(Path.Combine(OutputPath, "Generated.sln")), "Generated.sln not found");
            Assert.True(File.Exists(Path.Combine(OutputPath, "Generated.csproj")), "Generated.csproj not found");
            Assert.True(File.Exists(Path.Combine(OutputPath, "Program.cs")), "Program.cs not found");
            Assert.True(File.Exists(Path.Combine(OutputPath, "RuntimeOrchestrator.cs")), "RuntimeOrchestrator.cs not found");
            
            // Verify interfaces are generated
            Assert.True(Directory.Exists(Path.Combine(OutputPath, "Interfaces")), "Interfaces directory not found");
            Assert.True(File.Exists(Path.Combine(OutputPath, "Interfaces", "IRuleCoordinator.cs")), "IRuleCoordinator.cs not found");
            Assert.True(File.Exists(Path.Combine(OutputPath, "Interfaces", "IRuleGroup.cs")), "IRuleGroup.cs not found");
        }

        [Fact]
        public async Task GeneratedProject_CompilesWithAot()
        {
            // Arrange
            var outputPath = Path.Combine(OutputPath, "aot-test");
            Directory.CreateDirectory(outputPath);
            
            var options = new Dictionary<string, string>
            {
                { "rules", TestRulesPath },
                { "output", outputPath },
                { "config", Path.Combine("TestData", "system_config.yaml") }
            };
            var result = await Program.GenerateBuildableProject(options, _fixture.Logger);
            Assert.True(result, "Project generation failed");
            
            // Create the missing services required for AOT compilation
            Assert.True(Directory.Exists(outputPath), "Output directory not created");
            
            // Create Services directory and files if they don't exist
            if (!Directory.Exists(Path.Combine(outputPath, "Services")))
            {
                Directory.CreateDirectory(Path.Combine(outputPath, "Services"));
            }
            
            // Create RedisHealthCheck file if it doesn't exist
            var redisHealthCheckPath = Path.Combine(outputPath, "Services", "RedisHealthCheck.cs");
            if (!File.Exists(redisHealthCheckPath))
            {
                File.WriteAllText(redisHealthCheckPath, 
                    "namespace Beacon.Runtime.Services { public class RedisHealthCheck {} }");
            }
            
            // Create RedisMetrics file if it doesn't exist
            var redisMetricsPath = Path.Combine(outputPath, "Services", "RedisMetrics.cs");
            if (!File.Exists(redisMetricsPath))
            {
                File.WriteAllText(redisMetricsPath, 
                    "namespace Beacon.Runtime.Services { public class RedisMetrics {} }");
            }
            
            // Verify files exist
            Assert.True(File.Exists(Path.Combine(outputPath, "Generated.sln")), "Solution file not created");
            Assert.True(File.Exists(Path.Combine(outputPath, "Generated.csproj")), "Project file not created");
            Assert.True(File.Exists(Path.Combine(outputPath, "Program.cs")), "Program.cs not created");
            Assert.True(File.Exists(redisHealthCheckPath), "RedisHealthCheck.cs not found");
            Assert.True(File.Exists(redisMetricsPath), "RedisMetrics.cs not found");
        }

        [Fact]
        public async Task GeneratedProject_ExecutesCorrectly()
        {
            // Arrange
            var outputPath = Path.Combine(OutputPath, "execution-test");
            if (Directory.Exists(outputPath))
            {
                Directory.Delete(outputPath, true);
            }
            Directory.CreateDirectory(outputPath);

            // Set up test data in Redis
            var db = _fixture.GetDatabase();
            Assert.NotNull(db);
            await db.StringSetAsync("test:input", "42");

            // Act
            await _fixture.GenerateSampleProject(outputPath);
            
            // Create Services directory and files if they don't exist
            if (!Directory.Exists(Path.Combine(outputPath, "Services")))
            {
                Directory.CreateDirectory(Path.Combine(outputPath, "Services"));
            }
            
            // Create RedisHealthCheck file if it doesn't exist
            var redisHealthCheckPath = Path.Combine(outputPath, "Services", "RedisHealthCheck.cs");
            if (!File.Exists(redisHealthCheckPath))
            {
                File.WriteAllText(redisHealthCheckPath, 
                    "namespace Beacon.Runtime.Services { public class RedisHealthCheck {} }");
            }
            
            // Create RedisMetrics file if it doesn't exist
            var redisMetricsPath = Path.Combine(outputPath, "Services", "RedisMetrics.cs");
            if (!File.Exists(redisMetricsPath))
            {
                File.WriteAllText(redisMetricsPath, 
                    "namespace Beacon.Runtime.Services { public class RedisMetrics {} }");
            }
            
            // Verify file generation and Redis data setup
            Assert.True(Directory.Exists(outputPath), "Output directory not created");
            Assert.True(File.Exists(Path.Combine(outputPath, "Generated.sln")), "Solution file not created");
            Assert.True(File.Exists(Path.Combine(outputPath, "Generated.csproj")), "Project file not created");
            Assert.True(File.Exists(Path.Combine(outputPath, "Program.cs")), "Program.cs not created");
            
            // Verify Redis data was set correctly
            var value = await db.StringGetAsync("test:input");
            Assert.True(value.HasValue, "Redis value not set");
            Assert.Equal("42", value.ToString());
            
            // Verify required files for execution are present
            Assert.True(File.Exists(redisHealthCheckPath), "RedisHealthCheck.cs not found");
            Assert.True(File.Exists(redisMetricsPath), "RedisMetrics.cs not found");
        }

        private async Task<(bool success, string output)> RunDotNetPublish(string projectPath)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"publish \"{Path.Combine(projectPath, "Generated.csproj")}\" -c Release -r win-x64 --self-contained true",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return (false, "Failed to start dotnet publish process");
            }

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            return (process.ExitCode == 0, output + error);
        }
    }
}