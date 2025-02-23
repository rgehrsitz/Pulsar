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

            // Act
            var (success, output) = await RunDotNetPublish(outputPath);

            // Assert
            Assert.True(success, $"Build failed with output: {output}");
            Assert.True(File.Exists(Path.Combine(outputPath, "bin", "Release", "net8.0", "win-x64", "publish", "Generated.exe")));
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
            var (success, output) = await RunDotNetPublish(outputPath);

            // Assert
            Assert.True(success, $"Build failed with output: {output}");
            Assert.True(File.Exists(Path.Combine(outputPath, "bin", "Release", "net8.0", "win-x64", "publish", "Generated.exe")));
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
