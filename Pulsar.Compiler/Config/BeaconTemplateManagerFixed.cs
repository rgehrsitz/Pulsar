// File: Pulsar.Compiler/Config/BeaconTemplateManagerFixed.cs

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Serilog;

namespace Pulsar.Compiler.Config
{
    /// <summary>
    /// Generates and manages templates for the Beacon AOT-compatible solution structure
    /// </summary>
    public class BeaconTemplateManagerFixed
    {
        private readonly ILogger _logger;
        private readonly TemplateManager _originalTemplateManager;
        
        public BeaconTemplateManagerFixed()
        {
            _logger = LoggingConfig.GetLogger().ForContext<BeaconTemplateManagerFixed>();
            _originalTemplateManager = new TemplateManager();
        }
        
        /// <summary>
        /// Creates the complete Beacon solution structure
        /// </summary>
        /// <param name="outputPath">Base path for the Beacon solution</param>
        /// <param name="buildConfig">Build configuration</param>
        public void CreateBeaconSolution(string outputPath, BuildConfig buildConfig)
        {
            _logger.Information("Creating Beacon solution at {Path}", outputPath);
            
            // Create the solution directory
            string solutionDir = outputPath;
            Directory.CreateDirectory(solutionDir);
            
            // Create the solution file
            GenerateBeaconSolutionFile(solutionDir, buildConfig);
            
            // Create the runtime project
            string runtimeDir = Path.Combine(solutionDir, "Beacon.Runtime");
            Directory.CreateDirectory(runtimeDir);
            
            // Create the runtime project structure
            CreateRuntimeProjectStructure(runtimeDir, buildConfig);
            
            // Create the test project if enabled
            bool generateTestProject = true; // Hard-coded for now, normally would use buildConfig.GenerateTestProject
            if (generateTestProject)
            {
                string testsDir = Path.Combine(solutionDir, "Beacon.Tests");
                Directory.CreateDirectory(testsDir);
                
                // Create the test project structure
                CreateTestProjectStructure(testsDir, buildConfig);
            }
            
            _logger.Information("Beacon solution structure created successfully");
        }

        /// <summary>
        /// Creates the runtime project structure
        /// </summary>
        private void CreateRuntimeProjectStructure(string runtimeDir, BuildConfig buildConfig)
        {
            _logger.Information("Creating runtime project structure at {Path}", runtimeDir);
            
            // Create project directories
            Directory.CreateDirectory(Path.Combine(runtimeDir, "Generated"));
            Directory.CreateDirectory(Path.Combine(runtimeDir, "Services"));
            Directory.CreateDirectory(Path.Combine(runtimeDir, "Buffers"));
            Directory.CreateDirectory(Path.Combine(runtimeDir, "Interfaces"));
            
            // Generate project file
            GenerateRuntimeProjectFile(runtimeDir, buildConfig);
            
            // Copy necessary templates
            CopyRuntimeTemplateFiles(runtimeDir, buildConfig);
            
            // Generate Program.cs with AOT compatibility
            GenerateProgramCs(runtimeDir, buildConfig);
        }
        
        /// <summary>
        /// Creates the test project structure
        /// </summary>
        private void CreateTestProjectStructure(string testsDir, BuildConfig buildConfig)
        {
            _logger.Information("Creating test project structure at {Path}", testsDir);
            
            // Create project directories
            Directory.CreateDirectory(Path.Combine(testsDir, "Generated"));
            Directory.CreateDirectory(Path.Combine(testsDir, "Fixtures"));
            
            // Generate test project file
            GenerateTestProjectFile(testsDir, buildConfig);
            
            // Generate basic test fixtures
            GenerateTestFixtures(testsDir, buildConfig);
        }
        
        /// <summary>
        /// Generates the Beacon solution file
        /// </summary>
        private void GenerateBeaconSolutionFile(string solutionDir, BuildConfig buildConfig)
        {
            string solutionPath = Path.Combine(solutionDir, "Beacon.sln");
            
            var sb = new StringBuilder();
            sb.AppendLine("Microsoft Visual Studio Solution File, Format Version 12.00");
            sb.AppendLine("# Visual Studio Version 17");
            sb.AppendLine("VisualStudioVersion = 17.0.31903.59");
            sb.AppendLine("MinimumVisualStudioVersion = 10.0.40219.1");
            
            // Add Runtime project
            string runtimeGuid = Guid.NewGuid().ToString("B").ToUpper();
            sb.AppendLine($"Project(\"{{{Guid.NewGuid().ToString("B").ToUpper()}}}\") = \"Beacon.Runtime\", \"Beacon.Runtime\\Beacon.Runtime.csproj\", \"{runtimeGuid}\"");
            sb.AppendLine("EndProject");
            
            // Add Tests project if enabled
            string testsGuid = "";
            bool generateTestProject = true; // Hard-coded for now, normally would use buildConfig.GenerateTestProject
            if (generateTestProject)
            {
                testsGuid = Guid.NewGuid().ToString("B").ToUpper();
                sb.AppendLine($"Project(\"{{{Guid.NewGuid().ToString("B").ToUpper()}}}\") = \"Beacon.Tests\", \"Beacon.Tests\\Beacon.Tests.csproj\", \"{testsGuid}\"");
                sb.AppendLine("EndProject");
            }
            
            // Add solution configurations
            sb.AppendLine("Global");
            sb.AppendLine("    GlobalSection(SolutionConfigurationPlatforms) = preSolution");
            sb.AppendLine("        Debug|Any CPU = Debug|Any CPU");
            sb.AppendLine("        Release|Any CPU = Release|Any CPU");
            sb.AppendLine("    EndGlobalSection");
            
            // Add project configurations
            sb.AppendLine("    GlobalSection(ProjectConfigurationPlatforms) = postSolution");
            sb.AppendLine($"        {runtimeGuid}.Debug|Any CPU.ActiveCfg = Debug|Any CPU");
            sb.AppendLine($"        {runtimeGuid}.Debug|Any CPU.Build.0 = Debug|Any CPU");
            sb.AppendLine($"        {runtimeGuid}.Release|Any CPU.ActiveCfg = Release|Any CPU");
            sb.AppendLine($"        {runtimeGuid}.Release|Any CPU.Build.0 = Release|Any CPU");
            
            if (generateTestProject)
            {
                sb.AppendLine($"        {testsGuid}.Debug|Any CPU.ActiveCfg = Debug|Any CPU");
                sb.AppendLine($"        {testsGuid}.Debug|Any CPU.Build.0 = Debug|Any CPU");
                sb.AppendLine($"        {testsGuid}.Release|Any CPU.ActiveCfg = Release|Any CPU");
                sb.AppendLine($"        {testsGuid}.Release|Any CPU.Build.0 = Release|Any CPU");
            }
            
            sb.AppendLine("    EndGlobalSection");
            sb.AppendLine("EndGlobal");
            
            File.WriteAllText(solutionPath, sb.ToString());
            _logger.Information("Generated solution file: {Path}", solutionPath);
        }
        
        /// <summary>
        /// Generates the runtime project file
        /// </summary>
        private void GenerateRuntimeProjectFile(string runtimeDir, BuildConfig buildConfig)
        {
            string projectPath = Path.Combine(runtimeDir, "Beacon.Runtime.csproj");
            
            var sb = new StringBuilder();
            sb.AppendLine("<Project Sdk=\"Microsoft.NET.Sdk\">");
            sb.AppendLine("  <PropertyGroup>");
            sb.AppendLine($"    <TargetFramework>{buildConfig.TargetFramework}</TargetFramework>");
            sb.AppendLine("    <ImplicitUsings>enable</ImplicitUsings>");
            sb.AppendLine("    <Nullable>enable</Nullable>");
            sb.AppendLine("    <OutputType>Exe</OutputType>");
            
            // Add AOT compatibility properties
            sb.AppendLine("    <EnableTrimAnalyzer>true</EnableTrimAnalyzer>");
            sb.AppendLine("    <IsTrimmable>true</IsTrimmable>");
            sb.AppendLine("    <TrimmerSingleWarn>false</TrimmerSingleWarn>");
            
            if (buildConfig.StandaloneExecutable)
            {
                sb.AppendLine("    <PublishSingleFile>true</PublishSingleFile>");
                sb.AppendLine("    <SelfContained>true</SelfContained>");
                sb.AppendLine($"    <RuntimeIdentifier>{buildConfig.Target}</RuntimeIdentifier>");
            }
            
            if (buildConfig.OptimizeOutput)
            {
                sb.AppendLine("    <PublishReadyToRun>true</PublishReadyToRun>");
                sb.AppendLine("    <PublishTrimmed>true</PublishTrimmed>");
                sb.AppendLine("    <TrimMode>link</TrimMode>");
                sb.AppendLine("    <TrimmerRemoveSymbols>true</TrimmerRemoveSymbols>");
                sb.AppendLine("    <DebuggerSupport>false</DebuggerSupport>");
                sb.AppendLine("    <EnableUnsafeBinaryFormatterSerialization>false</EnableUnsafeBinaryFormatterSerialization>");
                sb.AppendLine("    <EnableUnsafeUTF7Encoding>false</EnableUnsafeUTF7Encoding>");
                sb.AppendLine("    <InvariantGlobalization>true</InvariantGlobalization>");
                sb.AppendLine("    <HttpActivityPropagationSupport>false</HttpActivityPropagationSupport>");
            }
            
            sb.AppendLine("  </PropertyGroup>");
            sb.AppendLine();
            
            // Add trimming configuration
            sb.AppendLine("  <ItemGroup>");
            sb.AppendLine("    <TrimmerRootDescriptor Include=\"trimming.xml\" />");
            sb.AppendLine("  </ItemGroup>");
            sb.AppendLine();
            
            // Add package references
            sb.AppendLine("  <ItemGroup>");
            sb.AppendLine("    <PackageReference Include=\"Microsoft.Extensions.Logging\" Version=\"8.0.0\" />");
            sb.AppendLine("    <PackageReference Include=\"NRedisStack\" Version=\"0.13.1\" />");
            sb.AppendLine("    <PackageReference Include=\"Polly\" Version=\"8.3.0\" />");
            sb.AppendLine("    <PackageReference Include=\"prometheus-net\" Version=\"8.2.1\" />");
            sb.AppendLine("    <PackageReference Include=\"Serilog\" Version=\"4.2.0\" />");
            sb.AppendLine("    <PackageReference Include=\"Serilog.Enrichers.Thread\" Version=\"3.1.0\" />");
            sb.AppendLine("    <PackageReference Include=\"Serilog.Formatting.Compact\" Version=\"2.0.0\" />");
            sb.AppendLine("    <PackageReference Include=\"Serilog.Sinks.Console\" Version=\"5.0.1\" />");
            sb.AppendLine("    <PackageReference Include=\"Serilog.Sinks.File\" Version=\"5.0.0\" />");
            sb.AppendLine("    <PackageReference Include=\"StackExchange.Redis\" Version=\"2.8.16\" />");
            sb.AppendLine("    <PackageReference Include=\"YamlDotNet\" Version=\"16.3.0\" />");
            sb.AppendLine("  </ItemGroup>");
            sb.AppendLine("</Project>");
            
            File.WriteAllText(projectPath, sb.ToString());
            _logger.Information("Generated runtime project file: {Path}", projectPath);
        }
        
        /// <summary>
        /// Generates the test project file
        /// </summary>
        private void GenerateTestProjectFile(string testsDir, BuildConfig buildConfig)
        {
            string projectPath = Path.Combine(testsDir, "Beacon.Tests.csproj");
            
            var sb = new StringBuilder();
            sb.AppendLine("<Project Sdk=\"Microsoft.NET.Sdk\">");
            sb.AppendLine("  <PropertyGroup>");
            sb.AppendLine($"    <TargetFramework>{buildConfig.TargetFramework}</TargetFramework>");
            sb.AppendLine("    <ImplicitUsings>enable</ImplicitUsings>");
            sb.AppendLine("    <Nullable>enable</Nullable>");
            sb.AppendLine("    <IsPackable>false</IsPackable>");
            sb.AppendLine("    <IsTestProject>true</IsTestProject>");
            sb.AppendLine("  </PropertyGroup>");
            sb.AppendLine();
            
            // Add package references
            sb.AppendLine("  <ItemGroup>");
            sb.AppendLine("    <PackageReference Include=\"Microsoft.NET.Test.Sdk\" Version=\"17.10.0\" />");
            sb.AppendLine("    <PackageReference Include=\"xunit\" Version=\"2.7.0\" />");
            sb.AppendLine("    <PackageReference Include=\"xunit.runner.visualstudio\" Version=\"2.5.7\">");
            sb.AppendLine("      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>");
            sb.AppendLine("      <PrivateAssets>all</PrivateAssets>");
            sb.AppendLine("    </PackageReference>");
            sb.AppendLine("    <PackageReference Include=\"coverlet.collector\" Version=\"6.0.2\">");
            sb.AppendLine("      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>");
            sb.AppendLine("      <PrivateAssets>all</PrivateAssets>");
            sb.AppendLine("    </PackageReference>");
            sb.AppendLine("    <PackageReference Include=\"Testcontainers.Redis\" Version=\"3.8.0\" />");
            sb.AppendLine("  </ItemGroup>");
            sb.AppendLine();
            
            // Add project reference to runtime project
            sb.AppendLine("  <ItemGroup>");
            sb.AppendLine("    <ProjectReference Include=\"..\\Beacon.Runtime\\Beacon.Runtime.csproj\" />");
            sb.AppendLine("  </ItemGroup>");
            sb.AppendLine("</Project>");
            
            File.WriteAllText(projectPath, sb.ToString());
            _logger.Information("Generated test project file: {Path}", projectPath);
        }
        
        /// <summary>
        /// Copies runtime template files from the original TemplateManager
        /// </summary>
        private void CopyRuntimeTemplateFiles(string runtimeDir, BuildConfig buildConfig)
        {
            // Copy interface templates
            CopyTemplateFile("Interfaces/ICompiledRules.cs", Path.Combine(runtimeDir, "Interfaces", "ICompiledRules.cs"));
            CopyTemplateFile("Interfaces/IRuleCoordinator.cs", Path.Combine(runtimeDir, "Interfaces", "IRuleCoordinator.cs"));
            CopyTemplateFile("Interfaces/IRuleGroup.cs", Path.Combine(runtimeDir, "Interfaces", "IRuleGroup.cs"));
            
            // Copy service templates
            CopyTemplateFile("Runtime/Services/RedisConfiguration.cs", Path.Combine(runtimeDir, "Services", "RedisConfiguration.cs"));
            CopyTemplateFile("Runtime/Services/RedisService.cs", Path.Combine(runtimeDir, "Services", "RedisService.cs"));
            CopyTemplateFile("Runtime/Services/RedisMonitoring.cs", Path.Combine(runtimeDir, "Services", "RedisMonitoring.cs"));
            CopyTemplateFile("Runtime/Services/RedisLoggingConfiguration.cs", Path.Combine(runtimeDir, "Services", "RedisLoggingConfiguration.cs"));
            
            // Copy buffer templates
            CopyTemplateFile("Runtime/Buffers/CircularBuffer.cs", Path.Combine(runtimeDir, "Buffers", "CircularBuffer.cs"));
            CopyTemplateFile("Runtime/Buffers/IDateTimeProvider.cs", Path.Combine(runtimeDir, "Buffers", "IDateTimeProvider.cs"));
            CopyTemplateFile("Runtime/Buffers/SystemDateTimeProvider.cs", Path.Combine(runtimeDir, "Buffers", "SystemDateTimeProvider.cs"));
            
            // Copy RuntimeOrchestrator and other core templates
            CopyTemplateFile("Runtime/RuntimeOrchestrator.cs", Path.Combine(runtimeDir, "RuntimeOrchestrator.cs"));
            CopyTemplateFile("RuntimeConfig.cs", Path.Combine(runtimeDir, "RuntimeConfig.cs"));
            
            // Copy AOT compatibility file
            CopyTemplateFile("trimming.xml", Path.Combine(runtimeDir, "trimming.xml"));
        }
        
        /// <summary>
        /// Generates the Program.cs file with AOT compatibility
        /// </summary>
        private void GenerateProgramCs(string runtimeDir, BuildConfig buildConfig)
        {
            string programPath = Path.Combine(runtimeDir, "Program.cs");
            
            var sb = new StringBuilder();
            
            // Add file header 
            sb.AppendLine("// Auto-generated Program.cs");
            sb.AppendLine("// Generated: " + DateTime.UtcNow.ToString("O"));
            sb.AppendLine("// This file contains the main entry point and AOT compatibility attributes");
            sb.AppendLine();
            
            // Add AOT compatibility attributes
            sb.AppendLine("using System.Runtime.CompilerServices;");
            sb.AppendLine("using System.Text.Json.Serialization;");
            sb.AppendLine();
            sb.AppendLine("[assembly: JsonSerializable(typeof(Dictionary<string, object>))]");
            sb.AppendLine($"[assembly: DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof({buildConfig.Namespace}.RuntimeOrchestrator))]");
            sb.AppendLine($"[assembly: DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof({buildConfig.Namespace}.Services.RedisService))]");
            sb.AppendLine();
            
            // Add standard using statements
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.IO;");
            sb.AppendLine("using System.Threading;");
            sb.AppendLine("using System.Threading.Tasks;");
            sb.AppendLine("using Microsoft.Extensions.Logging;");
            sb.AppendLine("using Serilog;");
            sb.AppendLine($"using {buildConfig.Namespace}.Buffers;");
            sb.AppendLine($"using {buildConfig.Namespace}.Services;");
            sb.AppendLine($"using {buildConfig.Namespace}.Interfaces;");
            sb.AppendLine();
            
            // Add namespace and class declaration
            sb.AppendLine($"namespace {buildConfig.Namespace}");
            sb.AppendLine("{");
            sb.AppendLine("    public class Program");
            sb.AppendLine("    {");
            
            // Add main method
            sb.AppendLine("        public static async Task Main(string[] args)");
            sb.AppendLine("        {");
            sb.AppendLine("            // Configure Serilog");
            sb.AppendLine("            var logger = ConfigureLogging();");
            sb.AppendLine("            logger.Information(\"Starting Beacon Runtime Engine\");");
            sb.AppendLine();
            sb.AppendLine("            try");
            sb.AppendLine("            {");
            sb.AppendLine("                // Load configuration");
            sb.AppendLine("                var config = RuntimeConfig.LoadFromEnvironment();");
            sb.AppendLine("                logger.Information(\"Loaded configuration with {SensorCount} sensors\", config.ValidSensors.Count);");
            sb.AppendLine();
            sb.AppendLine("                // Initialize Redis service");
            sb.AppendLine("                var redisConfig = config.Redis;");
            sb.AppendLine();
            sb.AppendLine("                // Create buffer manager for temporal rules");
            sb.AppendLine($"                var bufferManager = new CircularBuffer(config.BufferCapacity);");
            sb.AppendLine();
            sb.AppendLine("                // Initialize runtime orchestrator");
            sb.AppendLine("                using var redisService = new RedisService(redisConfig, logger);");
            sb.AppendLine("                var orchestrator = new RuntimeOrchestrator(redisService, logger, bufferManager);");
            sb.AppendLine();
            sb.AppendLine("                // Run the main cycle loop");
            sb.AppendLine("                await RunCycleLoop(orchestrator, config.CycleTime, logger);");
            sb.AppendLine("            }");
            sb.AppendLine("            catch (Exception ex)");
            sb.AppendLine("            {");
            sb.AppendLine("                logger.Error(ex, \"Fatal error in Beacon Runtime Engine\");");
            sb.AppendLine("                Environment.ExitCode = 1;");
            sb.AppendLine("            }");
            sb.AppendLine("            finally");
            sb.AppendLine("            {");
            sb.AppendLine("                Log.CloseAndFlush();");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();
            
            // Add helper methods
            sb.AppendLine("        private static ILogger ConfigureLogging()");
            sb.AppendLine("        {");
            sb.AppendLine("            var logConfig = new LoggerConfiguration()");
            sb.AppendLine("                .MinimumLevel.Information()");
            sb.AppendLine("                .Enrich.WithThreadId()");
            sb.AppendLine("                .WriteTo.Console()");
            sb.AppendLine("                .WriteTo.File(");
            sb.AppendLine("                    Path.Combine(\"logs\", \"beacon-.log\"),");
            sb.AppendLine("                    rollingInterval: RollingInterval.Day,");
            sb.AppendLine("                    retainedFileCountLimit: 7");
            sb.AppendLine("                );");
            sb.AppendLine();
            sb.AppendLine("            var logger = logConfig.CreateLogger();");
            sb.AppendLine("            Log.Logger = logger;");
            sb.AppendLine("            return logger;");
            sb.AppendLine("        }");
            sb.AppendLine();
            
            sb.AppendLine("        private static async Task RunCycleLoop(RuntimeOrchestrator orchestrator, int cycleTimeMs, ILogger logger)");
            sb.AppendLine("        {");
            sb.AppendLine("            var cancelSource = new CancellationTokenSource();");
            sb.AppendLine("            Console.CancelKeyPress += (s, e) =>");
            sb.AppendLine("            {");
            sb.AppendLine("                logger.Information(\"Shutdown requested\");");
            sb.AppendLine("                cancelSource.Cancel();");
            sb.AppendLine("                e.Cancel = true;");
            sb.AppendLine("            };");
            sb.AppendLine();
            sb.AppendLine("            logger.Information(\"Starting rule execution cycle loop with interval {CycleTimeMs}ms\", cycleTimeMs);");
            sb.AppendLine("            var cycleCount = 0;");
            sb.AppendLine();
            sb.AppendLine("            while (!cancelSource.Token.IsCancellationRequested)");
            sb.AppendLine("            {");
            sb.AppendLine("                var cycleStart = DateTime.UtcNow;");
            sb.AppendLine("                try");
            sb.AppendLine("                {");
            sb.AppendLine("                    await orchestrator.RunCycleAsync();");
            sb.AppendLine("                    cycleCount++;");
            sb.AppendLine();
            sb.AppendLine("                    if (cycleCount % 1000 == 0)");
            sb.AppendLine("                    {");
            sb.AppendLine("                        logger.Information(\"Completed {CycleCount} execution cycles\", cycleCount);");
            sb.AppendLine("                    }");
            sb.AppendLine("                }");
            sb.AppendLine("                catch (Exception ex)");
            sb.AppendLine("                {");
            sb.AppendLine("                    logger.Error(ex, \"Error in execution cycle {CycleCount}\", cycleCount);");
            sb.AppendLine("                }");
            sb.AppendLine();
            sb.AppendLine("                // Calculate time to wait until next cycle");
            sb.AppendLine("                var cycleTime = DateTime.UtcNow - cycleStart;");
            sb.AppendLine("                var delayMs = Math.Max(0, cycleTimeMs - (int)cycleTime.TotalMilliseconds);");
            sb.AppendLine("                if (delayMs > 0)");
            sb.AppendLine("                {");
            sb.AppendLine("                    await Task.Delay(delayMs, cancelSource.Token);");
            sb.AppendLine("                }");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("            logger.Information(\"Execution cycle loop stopped after {CycleCount} cycles\", cycleCount);");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            
            File.WriteAllText(programPath, sb.ToString());
            _logger.Information("Generated Program.cs: {Path}", programPath);
        }
        
        /// <summary>
        /// Generates the test fixture classes
        /// </summary>
        private void GenerateTestFixtures(string testsDir, BuildConfig buildConfig)
        {
            string fixturesDir = Path.Combine(testsDir, "Fixtures");
            string testFixturePath = Path.Combine(fixturesDir, "RuntimeTestFixture.cs");
            
            var sb = new StringBuilder();
            sb.AppendLine("// Auto-generated test fixture");
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.Threading.Tasks;");
            sb.AppendLine("using Microsoft.Extensions.Logging;");
            sb.AppendLine("using Xunit;");
            sb.AppendLine("using Testcontainers.Redis;");
            sb.AppendLine($"using {buildConfig.Namespace}.Services;");
            sb.AppendLine();
            
            sb.AppendLine("namespace Beacon.Tests.Fixtures");
            sb.AppendLine("{");
            sb.AppendLine("    public class RuntimeTestFixture : IAsyncLifetime");
            sb.AppendLine("    {");
            sb.AppendLine("        private RedisContainer _redisContainer;");
            sb.AppendLine();
            
            sb.AppendLine("        public string RedisConnectionString { get; private set; }");
            sb.AppendLine();
            
            sb.AppendLine("        public async Task InitializeAsync()");
            sb.AppendLine("        {");
            sb.AppendLine("            _redisContainer = new RedisBuilder()");
            sb.AppendLine("                .WithImage(\"redis:latest\")");
            sb.AppendLine("                .WithPortBinding(6379, true)");
            sb.AppendLine("                .Build();");
            sb.AppendLine();
            
            sb.AppendLine("            await _redisContainer.StartAsync();");
            sb.AppendLine("            RedisConnectionString = _redisContainer.GetConnectionString();");
            sb.AppendLine("        }");
            sb.AppendLine();
            
            sb.AppendLine("        public async Task DisposeAsync()");
            sb.AppendLine("        {");
            sb.AppendLine("            if (_redisContainer != null)");
            sb.AppendLine("            {");
            sb.AppendLine("                await _redisContainer.DisposeAsync();");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            
            // Create fixtures directory if it doesn't exist
            Directory.CreateDirectory(fixturesDir);
            
            // Write the test fixture
            File.WriteAllText(testFixturePath, sb.ToString());
            _logger.Information("Generated test fixture: {Path}", testFixturePath);
            
            // Generate a basic test class
            string basicTestPath = Path.Combine(testsDir, "BasicRuntimeTests.cs");
            
            sb = new StringBuilder();
            sb.AppendLine("// Auto-generated test class");
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Threading.Tasks;");
            sb.AppendLine("using Xunit;");
            sb.AppendLine("using Beacon.Tests.Fixtures;");
            sb.AppendLine($"using {buildConfig.Namespace}.Services;");
            sb.AppendLine();
            
            sb.AppendLine("namespace Beacon.Tests");
            sb.AppendLine("{");
            sb.AppendLine("    public class BasicRuntimeTests : IClassFixture<RuntimeTestFixture>");
            sb.AppendLine("    {");
            sb.AppendLine("        private readonly RuntimeTestFixture _fixture;");
            sb.AppendLine();
            
            sb.AppendLine("        public BasicRuntimeTests(RuntimeTestFixture fixture)");
            sb.AppendLine("        {");
            sb.AppendLine("            _fixture = fixture;");
            sb.AppendLine("        }");
            sb.AppendLine();
            
            sb.AppendLine("        [Fact]");
            sb.AppendLine("        public void RedisConnection_IsAvailable()");
            sb.AppendLine("        {");
            sb.AppendLine("            Assert.NotNull(_fixture.RedisConnectionString);");
            sb.AppendLine("            Assert.NotEmpty(_fixture.RedisConnectionString);");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            
            File.WriteAllText(basicTestPath, sb.ToString());
            _logger.Information("Generated basic test class: {Path}", basicTestPath);
        }

        /// <summary>
        /// Copy a template file from the source templates to a destination path
        /// </summary>
        private void CopyTemplateFile(string templatePath, string destinationPath)
        {
            try
            {
                // Get the content from the original template manager's helper method
                string sourceContent = GetTemplateContent(templatePath);
                
                // Ensure the directory exists
                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
                
                // Write to the destination
                File.WriteAllText(destinationPath, sourceContent);
                _logger.Information("Copied template: {Source} to {Destination}", templatePath, destinationPath);
            }
            catch (Exception ex)
            {
                _logger.Warning("Error copying template {TemplatePath}: {Error}", templatePath, ex.Message);
            }
        }
        
        /// <summary>
        /// Gets the content of a template file
        /// </summary>
        private string GetTemplateContent(string templateFileName)
        {
            // Try multiple possible locations for the template files
            var possiblePaths = new[]
            {
                // Direct path from working directory
                Path.Combine("Pulsar.Compiler", "Config", "Templates", templateFileName),
                // Path relative to assembly location
                Path.Combine(
                    Path.GetDirectoryName(typeof(TemplateManager).Assembly.Location) ?? "",
                    "Config",
                    "Templates",
                    templateFileName
                ),
                // Path from assembly base directory
                Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "Config", 
                    "Templates", 
                    templateFileName
                ),
                // Path relative to project root (go up from bin directory)
                Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "..",
                    "..",
                    "..",
                    "Pulsar.Compiler",
                    "Config",
                    "Templates",
                    templateFileName
                ),
                // Absolute path based on solution directory structure
                Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "..",
                    "..",
                    "..",
                    "..",
                    "Pulsar.Compiler",
                    "Config",
                    "Templates",
                    templateFileName
                ),
            };

            foreach (var path in possiblePaths)
            {
                var normalizedPath = Path.GetFullPath(path);
                if (File.Exists(normalizedPath))
                {
                    _logger.Debug("Found template at: {Path}", normalizedPath);
                    return File.ReadAllText(normalizedPath);
                }
            }

            _logger.Error("Template file not found: {TemplateFile}. Searched in: {Paths}", 
                templateFileName, string.Join(", ", possiblePaths));
                
            throw new FileNotFoundException(
                $"Template file not found: {templateFileName}. Searched in: {string.Join(", ", possiblePaths)}"
            );
        }
    }
}