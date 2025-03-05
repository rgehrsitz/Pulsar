// File: Pulsar.Tests/RuntimeValidation/RuntimeValidationFixture.cs

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Testcontainers.Redis;
using DotNet.Testcontainers.Containers;
using Pulsar.Compiler;
using Pulsar.Compiler.Config;
using Pulsar.Compiler.Core;
using Pulsar.Compiler.Models;
using Pulsar.Compiler.Parsers;
using Pulsar.Tests.TestUtilities;
using Xunit;
using System.Linq;
using Serilog;
using Serilog.Extensions.Logging;
using Beacon.Runtime.Services;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Pulsar.Tests.RuntimeValidation
{
    /// <summary>
    /// Test fixture that builds and compiles real rule projects
    /// </summary>
    public class RuntimeValidationFixture : IAsyncLifetime, IDisposable
    {
        private readonly ILogger _logger;
        private readonly DslParser _parser;
        private RedisContainer? _redisContainer;
        private ConnectionMultiplexer? _redisConnection;
        private string _testOutputPath;
        private Assembly? _compiledAssembly;
        private bool _disposed;

        public RuntimeValidationFixture()
        {
            // Convert Serilog logger to Microsoft.Extensions.Logging.ILogger
            var serilogLogger = Pulsar.Tests.TestUtilities.LoggingConfig.GetLogger();
            _logger = new SerilogLoggerFactory(serilogLogger).CreateLogger<RuntimeValidationFixture>();
            
            _parser = new DslParser();
            _testOutputPath = Path.Combine(Directory.GetCurrentDirectory(), "RuntimeValidation", "test-output");
            
            // Ensure parent directories exist
            var parentDir = Path.GetDirectoryName(_testOutputPath);
            if (!string.IsNullOrEmpty(parentDir) && !Directory.Exists(parentDir))
            {
                Directory.CreateDirectory(parentDir);
            }

            // Now safely delete and recreate the test output directory
            if (Directory.Exists(_testOutputPath))
            {
                try
                {
                    Directory.Delete(_testOutputPath, true);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Could not delete directory {_testOutputPath}: {ex.Message}");
                    // Try to clean individual files instead
                    try
                    {
                        foreach (var file in Directory.GetFiles(_testOutputPath))
                        {
                            File.Delete(file);
                        }
                    }
                    catch 
                    {
                        // Ignore cleanup errors, proceed with test
                    }
                }
            }
            
            // Create output directory if it doesn't exist
            Directory.CreateDirectory(_testOutputPath);
        }

        public IDatabase? Redis => _redisConnection?.GetDatabase();
        public Assembly? CompiledAssembly => _compiledAssembly;
        public string OutputPath => _testOutputPath;
        public ILogger Logger => _logger;
        
        public async Task InitializeAsync()
        {
            // Start Redis container
            await StartRedisContainer();
            
            // Create test rules
            await CreateTestRules();
        }

        private async Task StartRedisContainer()
        {
            _logger.LogInformation("Starting Redis container...");
            
            _redisContainer = new RedisBuilder()
                .WithImage("redis:latest")
                .WithPortBinding(6379, true)
                .Build();

            await _redisContainer.StartAsync();
            
            // Connect to Redis
            var connectionString = _redisContainer.GetConnectionString();
            _redisConnection = await ConnectionMultiplexer.ConnectAsync(connectionString);
            
            _logger.LogInformation("Connected to Redis at {ConnectionString}", connectionString);
        }

        private async Task CreateTestRules()
        {
            // Create test rule files
            var simpleRule = @"
rules:
  - name: 'SimpleRule'
    description: 'Simple rule that adds two values'
    conditions:
      all:
        - condition:
            type: comparison
            sensor: 'input:a'
            operator: '>'
            value: 0
    actions:
      - set_value:
          key: 'output:sum'
          value_expression: 'input:a + input:b'";

            var complexRule = @"
rules:
  - name: 'ComplexRule'
    description: 'Complex rule with multiple conditions'
    conditions:
      any:
        - condition:
            all:
              - condition:
                  type: comparison
                  sensor: 'input:a'
                  operator: '>'
                  value: 100
              - condition:
                  type: comparison
                  sensor: 'input:b'
                  operator: '<'
                  value: 50
        - condition:
            type: expression
            expression: 'input:c > (input:a + input:b)'
    actions:
      - set_value:
          key: 'output:complex'
          value: 1";

            // Create test rule files
            await File.WriteAllTextAsync(Path.Combine(_testOutputPath, "simple-rule.yaml"), simpleRule);
            await File.WriteAllTextAsync(Path.Combine(_testOutputPath, "complex-rule.yaml"), complexRule);
            
            // Create system config
            var systemConfig = @"
version: 1
validSensors:
  - input:a
  - input:b
  - input:c
  - output:sum
  - output:complex
cycleTime: 100  # ms
redis:
  endpoints: 
    - localhost:6379
  poolSize: 4
  retryCount: 3
  retryBaseDelayMs: 100
  connectTimeout: 5000
  syncTimeout: 1000
  keepAlive: 60
  password: null
  ssl: false
  allowAdmin: false
bufferCapacity: 100
";
            await File.WriteAllTextAsync(Path.Combine(_testOutputPath, "system_config.yaml"), systemConfig);
        }

        /// <summary>
        /// Builds a test project with the given rule files
        /// </summary>
        public async Task<bool> BuildTestProject(string[] ruleFiles)
        {
            try
            {
                // Parse the rules
                var rules = new List<RuleDefinition>();
                var validSensors = new List<string> { "input:a", "input:b", "input:c", "output:sum", "output:complex" };
                
                foreach (var ruleFile in ruleFiles)
                {
                    _logger.LogDebug("Reading rule file: {FilePath}", ruleFile);
                    var content = await File.ReadAllTextAsync(ruleFile);
                    _logger.LogDebug("Parsing rules from file: {FileName}", Path.GetFileName(ruleFile));
                    var parsedRules = _parser.ParseRules(content, validSensors, Path.GetFileName(ruleFile));
                    _logger.LogInformation("Parsed {Count} rules from {File}", parsedRules.Count, Path.GetFileName(ruleFile));
                    rules.AddRange(parsedRules);
                }
                
                // Create a system config
                var systemConfig = new SystemConfig
                {
                    ValidSensors = validSensors,
                    CycleTime = 100,
                    BufferCapacity = 100,
                    Redis = new Beacon.Runtime.Services.RedisConfiguration
                    {
                        Endpoints = new List<string> { "localhost:6379" },
                        PoolSize = 4,
                        RetryCount = 3,
                        RetryBaseDelayMs = 100,
                        ConnectTimeoutMs = 5000,
                        SyncTimeoutMs = 1000
                    }
                };
                
                // Add extra helper files needed for successful build
                CreateRedisHelperClasses();
                
                // Use the BeaconBuildOrchestratorFixed implementation
                _logger.LogInformation("Building project with {Count} rules", rules.Count);
                var buildConfig = new BuildConfig
                {
                    OutputPath = _testOutputPath,
                    Target = "linux-x64", // Changed to Linux for tests running on Linux
                    ProjectName = "RuntimeTest",
                    AssemblyName = "RuntimeTest",
                    TargetFramework = "net9.0",
                    RulesPath = _testOutputPath,
                    RuleDefinitions = rules,
                    SystemConfig = systemConfig,
                    StandaloneExecutable = false, // We want to load this dynamically
                    GenerateDebugInfo = true,
                    OptimizeOutput = false, // Better for debugging
                    RedisConnection = "localhost:6379",
                    CycleTime = 100,
                    BufferCapacity = 100,
                    MaxRulesPerFile = 50,
                    MaxLinesPerFile = 1000,
                    ComplexityThreshold = 10,
                    GroupParallelRules = true,
                    Namespace = "Beacon.Runtime", // Namespace needs to match the expected namespace
                    CreateSeparateDirectory = false // Don't create a subdirectory
                };
                
                var orchestrator = new BeaconBuildOrchestratorFixed();
                var buildResult = await orchestrator.BuildBeaconAsync(buildConfig);
                
                if (!buildResult.Success)
                {
                    _logger.LogError("Build failed: {Errors}", 
                        string.Join(Environment.NewLine, buildResult.Errors));
                    return false;
                }
                
                _logger.LogInformation("Build successful, loading assembly");
                
                // Try different possible assembly paths
                var potentialPaths = new[]
                {
                    // BeaconBuildOrchestratorFixed paths
                    Path.Combine(_testOutputPath, "bin", "Debug", "net9.0", "RuntimeTest.dll"),
                    Path.Combine(_testOutputPath, "bin", "Release", "net9.0", "RuntimeTest.dll"),
                    // With RID paths
                    Path.Combine(_testOutputPath, "bin", "Debug", "net9.0", "linux-x64", "RuntimeTest.dll"),
                    Path.Combine(_testOutputPath, "bin", "Release", "net9.0", "linux-x64", "RuntimeTest.dll"),
                    // Legacy builder paths
                    Path.Combine(_testOutputPath, "bin", "Debug", "net9.0", "publish", "RuntimeTest.dll"),
                    Path.Combine(_testOutputPath, "bin", "Release", "net9.0", "publish", "RuntimeTest.dll")
                };
                
                string? assemblyPath = null;
                foreach (var path in potentialPaths)
                {
                    if (File.Exists(path))
                    {
                        assemblyPath = path;
                        _logger.LogInformation("Found assembly at: {Path}", assemblyPath);
                        break;
                    }
                }
                
                if (assemblyPath == null)
                {
                    // If not found, try to find the assembly by searching
                    // If not found, try to find the assembly by searching
                    _logger.LogWarning("Assembly not found at expected paths, searching in output directory...");
                    string[] searchPatterns = new[] { "RuntimeTest.dll", "Beacon.Runtime.dll" };
                
                    foreach (var searchPattern in searchPatterns)
                    {
                        var foundFiles = Directory.GetFiles(_testOutputPath, searchPattern, SearchOption.AllDirectories);
                        
                        if (foundFiles.Length > 0)
                        {
                            assemblyPath = foundFiles[0]; // Take the first one found
                            _logger.LogInformation("Found assembly by search at: {Path}", assemblyPath);
                            break;
                        }
                    }
                
                    if (string.IsNullOrEmpty(assemblyPath))
                    {
                        // Try building it manually
                        _logger.LogInformation("Assembly not found, trying to build it manually");
                        var buildCommand = $"dotnet build {_testOutputPath}/Beacon.Runtime/Beacon.Runtime.csproj";
                        try
                        {
                            var process = new Process
                            {
                                StartInfo = new ProcessStartInfo
                                {
                                    FileName = "/bin/bash",
                                    Arguments = $"-c \"{buildCommand}\"",
                                    RedirectStandardOutput = true,
                                    RedirectStandardError = true,
                                    UseShellExecute = false
                                }
                            };
                            process.Start();
                            string output = process.StandardOutput.ReadToEnd();
                            string error = process.StandardError.ReadToEnd();
                            process.WaitForExit();
                            
                            if (process.ExitCode == 0)
                            {
                                _logger.LogInformation("Manual build succeeded: {Output}", output);
                                // Search again after build
                                var binFiles = Directory.GetFiles(_testOutputPath, "Beacon.Runtime.dll", SearchOption.AllDirectories);
                                if (binFiles.Length > 0)
                                {
                                    assemblyPath = binFiles[0];
                                    _logger.LogInformation("Found assembly after build at: {Path}", assemblyPath);
                                }
                            }
                            else
                            {
                                _logger.LogError("Failed to build assembly manually: {Error}", error);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error trying to build assembly manually");
                        }
                    }
                
                    if (string.IsNullOrEmpty(assemblyPath))
                    {
                        _logger.LogError("Assembly not found in output directory");
                        return false;
                    }
                }
                
                _logger.LogInformation("Loading assembly from: {Path}", assemblyPath);
                _compiledAssembly = Assembly.LoadFrom(assemblyPath);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error building test project");
                return false;
            }
        }
        
        /// <summary>
        /// Creates helper classes required for compilation but not generated by the compiler
        /// </summary>
        private void CreateRedisHelperClasses()
        {
            // Create Services directory
            var servicesDir = Path.Combine(_testOutputPath, "Services");
            Directory.CreateDirectory(servicesDir);
            
            // Create RedisHealthCheck file
            var redisHealthCheckPath = Path.Combine(servicesDir, "RedisHealthCheck.cs");
            File.WriteAllText(redisHealthCheckPath, 
                "namespace Beacon.Runtime.Services { public class RedisHealthCheck {} }");
            
            // Create RedisMetrics file
            var redisMetricsPath = Path.Combine(servicesDir, "RedisMetrics.cs");
            File.WriteAllText(redisMetricsPath, 
                "namespace Beacon.Runtime.Services { public class RedisMetrics {} }");
        }
        
        /// <summary>
        /// Executes a method on the compiled assembly
        /// </summary>
        public object? ExecuteMethod(string typeName, string methodName, params object[] parameters)
        {
            if (_compiledAssembly == null)
            {
                throw new InvalidOperationException("Assembly has not been compiled");
            }
            
            var type = _compiledAssembly.GetType(typeName);
            if (type == null)
            {
                throw new InvalidOperationException($"Type {typeName} not found in compiled assembly");
            }
            
            var method = type.GetMethod(methodName);
            if (method == null)
            {
                throw new InvalidOperationException($"Method {methodName} not found in type {typeName}");
            }
            
            // Create an instance if the method is not static
            object? instance = null;
            if (!method.IsStatic)
            {
                instance = Activator.CreateInstance(type);
            }
            
            return method.Invoke(instance, parameters);
        }
        
        public async Task<(bool success, Dictionary<string, object>? outputs)> ExecuteRules(Dictionary<string, object> inputs)
        {
            try
            {
                if (Redis == null || _compiledAssembly == null)
                {
                    _logger.LogError("Redis or compiled assembly not available");
                    return (false, null);
                }
                
                // Clear Redis first
                await Redis.ExecuteAsync("FLUSHALL");
                
                // Load inputs into Redis
                foreach (var (key, value) in inputs)
                {
                    await Redis.StringSetAsync(key, value.ToString());
                }
                
                // Load the RuntimeOrchestrator
                var orchestratorType = _compiledAssembly.GetType("Beacon.Runtime.RuntimeOrchestrator");
                if (orchestratorType == null)
                {
                    _logger.LogError("RuntimeOrchestrator type not found in compiled assembly");
                    return (false, null);
                }
                
                // Create dependencies for the orchestrator
                var redisConfigType = _compiledAssembly.GetType("Beacon.Runtime.Services.RedisConfiguration");
                if (redisConfigType == null)
                {
                    _logger.LogError("RedisConfiguration type not found in compiled assembly");
                    return (false, null);
                }
                
                var redisConfig = Activator.CreateInstance(redisConfigType);
                if (redisConfig == null)
                {
                    _logger.LogError("Failed to create RedisConfiguration instance");
                    return (false, null);
                }
                
                // Set the Redis endpoints property
                var endpointsProperty = redisConfigType.GetProperty("Endpoints");
                if (endpointsProperty == null)
                {
                    _logger.LogError("Endpoints property not found in RedisConfiguration");
                    return (false, null);
                }
                
                endpointsProperty.SetValue(redisConfig, new[] { "localhost:6379" });
                
                // Create and run the orchestrator
                var orchestrator = Activator.CreateInstance(orchestratorType, 
                    new object[] { redisConfig, _logger });
                
                if (orchestrator == null)
                {
                    _logger.LogError("Failed to create RuntimeOrchestrator instance");
                    return (false, null);
                }
                
                // Get the RunCycleAsync method
                var runCycleMethod = orchestratorType.GetMethod("RunCycleAsync");
                if (runCycleMethod == null)
                {
                    _logger.LogError("RunCycleAsync method not found in RuntimeOrchestrator");
                    return (false, null);
                }
                
                await (Task)runCycleMethod.Invoke(orchestrator, null)!;
                
                // Read outputs from Redis
                var outputs = new Dictionary<string, object>();
                var keys = (string[])await Redis.ExecuteAsync("KEYS", "output:*");
                foreach (var key in keys)
                {
                    var value = await Redis.StringGetAsync(key);
                    outputs[key] = value.ToString();
                }
                
                return (true, outputs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing rules");
                return (false, null);
            }
        }
        
        public async Task MonitorMemoryUsage(TimeSpan duration, int cycleCount, Action<long> memoryCallback)
        {
            var process = Process.GetCurrentProcess();
            var startMemory = process.WorkingSet64;
            var stopwatch = Stopwatch.StartNew();
            
            // Create test data
            var inputs = new Dictionary<string, object>
            {
                { "input:a", 10 },
                { "input:b", 20 },
                { "input:c", 30 }
            };
            
            _logger.LogInformation("Starting memory usage test - initial memory: {StartMemory}MB", 
                startMemory / (1024 * 1024));
            
            for (int i = 0; i < cycleCount && stopwatch.Elapsed < duration; i++)
            {
                // Run the rules
                await ExecuteRules(inputs);
                
                // Check memory usage every 10 cycles
                if (i % 10 == 0)
                {
                    process.Refresh();
                    var currentMemory = process.WorkingSet64;
                    memoryCallback(currentMemory);
                    
                    _logger.LogInformation("Cycle {Cycle} - Memory: {Memory}MB", 
                        i, currentMemory / (1024 * 1024));
                }
                
                // Small delay to prevent overwhelming the system
                await Task.Delay(100);
            }
            
            stopwatch.Stop();
            process.Refresh();
            var finalMemory = process.WorkingSet64;
            
            _logger.LogInformation("Memory test completed - final memory: {FinalMemory}MB, change: {Change}MB", 
                finalMemory / (1024 * 1024), (finalMemory - startMemory) / (1024 * 1024));
        }

        public async Task DisposeAsync()
        {
            if (!_disposed)
            {
                if (_redisContainer != null)
                {
                    await _redisContainer.DisposeAsync();
                }
                _redisConnection?.Dispose();
                _disposed = true;
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _redisConnection?.Dispose();
                _redisContainer?.DisposeAsync().AsTask().Wait();
                _disposed = true;
            }
        }
    }
}