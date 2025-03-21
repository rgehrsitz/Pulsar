// File: Pulsar.Tests/RuntimeValidation/RuntimeValidationFixture.cs


using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Pulsar.Compiler.Config;
using Pulsar.Compiler.Core;
using Pulsar.Compiler.Models;
using Pulsar.Compiler.Parsers;
using Pulsar.Tests.TestUtilities;
using StackExchange.Redis;
using Testcontainers.Redis;
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
        // We no longer need to keep a reference to the compiled assembly
        private bool _disposed;

        public RuntimeValidationFixture()
        {
            // Use Microsoft.Extensions.Logging.ILogger directly
            _logger = Pulsar.Tests.TestUtilities.LoggingConfig.GetLogger();
            _parser = new DslParser();
            _testOutputPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "RuntimeValidation",
                "test-output"
            );
            // Ensure parent directories exist
            Directory.CreateDirectory(_testOutputPath);
        }

        public ILogger Logger => _logger;
        public string OutputPath => _testOutputPath;

        public async Task InitializeAsync()
        {
            _logger.LogInformation("Initializing RuntimeValidationFixture");
            try
            {
                // Start Redis container
                _redisContainer = new RedisBuilder().WithPortBinding(6379, true).Build();
                await _redisContainer.StartAsync();
                _logger.LogInformation(
                    "Redis container started on port {Port}",
                    _redisContainer.GetMappedPublicPort(6379)
                );
                // Connect to Redis
                _redisConnection = await ConnectionMultiplexer.ConnectAsync(
                    "localhost:" + _redisContainer.GetMappedPublicPort(6379)
                );
                _logger.LogInformation("Connected to Redis");
                // Create test rule files
                await CreateMinimalRuleFile();
                await CreateSimpleRuleFile();
                await CreateComplexRuleFile();
                await CreateSystemConfigFile();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize RuntimeValidationFixture");
                throw;
            }
        }

        public async Task DisposeAsync()
        {
            if (_redisConnection != null)
            {
                _redisConnection.Dispose();
                _redisConnection = null;
            }
            if (_redisContainer != null)
            {
                await _redisContainer.DisposeAsync();
                _redisContainer = null;
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            if (_redisConnection != null)
            {
                _redisConnection.Dispose();
                _redisConnection = null;
            }
            _disposed = true;
        }

        /// <summary>
        /// Builds a test project with the given rule files
        /// </summary>
        public async Task<bool> BuildTestProject(string[] ruleFiles)
        {
            try
            {
                _logger.LogInformation(
                    "Building test project with {Count} rule files",
                    ruleFiles.Length
                );
                
                // Create system config file if it doesn't exist
                var systemConfigPath = Path.Combine(_testOutputPath, "system_config.yaml");
                if (!File.Exists(systemConfigPath))
                {
                    await CreateSystemConfigFile();
                }
                
                // Create BuildConfig
                var buildConfig = new BuildConfig
                {
                    OutputPath = _testOutputPath,
                    Target = "linux-x64",
                    ProjectName = "Beacon.Runtime.Test",
                    AssemblyName = "Beacon.Runtime.Test",
                    TargetFramework = "net9.0",
                    RuleDefinitions = new List<RuleDefinition>(),
                    RulesPath = string.Join(",", ruleFiles), // Add required RulesPath
                    Namespace = "Beacon.Runtime",
                    StandaloneExecutable = true,
                    GenerateDebugInfo = true,
                    OptimizeOutput = false,
                    CycleTime = 1000,
                    BufferCapacity = 100,
                    MaxRulesPerFile = 50,
                    MaxLinesPerFile = 1000,
                    ComplexityThreshold = 10,
                    GroupParallelRules = true,
                    GenerateTestProject = true,
                    CreateSeparateDirectory = true,
                    RedisConnection = "localhost:" + _redisContainer?.GetMappedPublicPort(6379),
                };
                
                // Create a SystemConfig object and assign it to the BuildConfig
                var systemConfig = new SystemConfig
                {
                    Version = 1,
                    CycleTime = 1000,
                    BufferCapacity = 100,
                    LogLevel = "Debug",
                    ValidSensors = new List<string> { 
                        "input:a", "input:b", "input:c", 
                        "output:sum", "output:complex", "output:log",
                        "test"
                    },
                    Redis = new Dictionary<string, object>
                    {
                        { "connection", "localhost:" + _redisContainer?.GetMappedPublicPort(6379) },
                        { "keyPrefix", "test:" }
                    }
                };
                buildConfig.SystemConfig = systemConfig;
                
                // Parse and validate rules
                var ruleDefinitions = new List<RuleDefinition>();
                foreach (var ruleFile in ruleFiles)
                {
                    var ruleContent = await File.ReadAllTextAsync(ruleFile);
                    try {
                        // Use the same valid sensors as in the system config
                        var parsedRules = _parser.ParseRules(ruleContent, systemConfig.ValidSensors, Path.GetFileName(ruleFile), true);
                        ruleDefinitions.AddRange(parsedRules);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(
                            "Failed to parse rule file {RuleFile}: {Error}",
                            Path.GetFileName(ruleFile),
                            ex.Message
                        );
                        return false;
                    }
                }
                buildConfig.RuleDefinitions = ruleDefinitions;
                
                // Generate source code
                var compiler = new AOTRuleCompiler();
                var options = new CompilerOptions
                {
                    OutputDirectory = _testOutputPath,
                    ValidSensors = systemConfig.ValidSensors,
                    AllowInvalidSensors = true,
                    BuildConfig = buildConfig
                };
                
                // Use BeaconBuildOrchestrator for AOT-compatible builds
                var orchestrator = new BeaconBuildOrchestrator();
                var buildResult = await orchestrator.BuildBeaconAsync(buildConfig);
                
                if (!buildResult.Success)
                {
                    _logger.LogError(
                        "Failed to build Beacon project: {Errors}",
                        string.Join(", ", buildResult.Errors)
                    );
                    return false;
                }
                
                _logger.LogInformation("Successfully built Beacon project with AOT compatibility");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to build test project");
                return false;
            }
        }

        /// <summary>
        /// Executes the rules in the compiled assembly
        /// </summary>
        /// <param name="inputs">Optional dictionary of inputs to pass to the rules</param>
        /// <returns>A tuple containing (success, outputs)</returns>
        public async Task<(bool success, Dictionary<string, object>? outputs)> ExecuteRules(Dictionary<string, object>? inputs = null)
        {
            try
            {
                _logger.LogInformation("Executing rules");
                // Start a process to monitor memory usage
                _ = MonitorMemoryUsage(TimeSpan.FromMinutes(5), 10);
                // Execute the rules
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = $"{Path.Combine(_testOutputPath, "Beacon.Runtime.Test.dll")}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                var process = new Process
                {
                    StartInfo = processStartInfo,
                    EnableRaisingEvents = true,
                };
                process.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        _logger.LogInformation("[Beacon] {Output}", e.Data);
                    }
                };
                process.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        _logger.LogError("[Beacon] {Error}", e.Data);
                    }
                };
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                // Wait for the process to exit
                await process.WaitForExitAsync();
                // Memory monitoring will stop automatically after the specified duration
                return (process.ExitCode == 0, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute rules");
                return (false, null);
            }
        }

        /// <summary>
        /// Monitors memory usage of the process
        /// </summary>
        /// <param name="duration">Duration to monitor memory usage</param>
        /// <param name="cycleCount">Number of cycles to monitor</param>
        /// <param name="memoryCallback">Callback to receive memory usage updates</param>
        public async Task MonitorMemoryUsage(TimeSpan duration, int cycleCount = 1, Action<long>? memoryCallback = null)
        {
            var cts = new CancellationTokenSource(duration);
            var process = Process.GetCurrentProcess();
            var intervalMs = (int)(duration.TotalMilliseconds / cycleCount);
            
            try
            {
                for (int i = 0; i < cycleCount && !cts.Token.IsCancellationRequested; i++)
                {
                    process.Refresh();
                    var memoryBytes = process.WorkingSet64;
                    var memoryMB = memoryBytes / (1024 * 1024);
                    _logger.LogInformation("Memory usage: {MemoryMB} MB", memoryMB);
                    
                    // Call the callback if provided
                    memoryCallback?.Invoke(memoryBytes);
                    
                    if (i < cycleCount - 1) // Don't delay on the last iteration
                    {
                        await Task.Delay(intervalMs, cts.Token);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error monitoring memory usage");
            }
        }

        /// <summary>
        /// Creates a minimal rule file for testing
        /// </summary>
        public async Task CreateMinimalRuleFile()
        {
            var ruleContent = @"rules:
  - name: TestRule
    description: A test rule
    conditions:
      all:
        - condition:
            type: comparison
            sensor: test
            operator: '>'
            value: 0
    actions:
      - set_value:
          key: output:log
          value_expression: 'Test rule triggered'
";
            var ruleFilePath = Path.Combine(_testOutputPath, "test-rule.yaml");
            await File.WriteAllTextAsync(ruleFilePath, ruleContent);
            _logger.LogInformation("Created minimal rule file at {RuleFilePath}", ruleFilePath);
            _logger.LogInformation("Rule content: {RuleContent}", ruleContent);
        }

        /// <summary>
        /// Creates a simple rule file for testing
        /// </summary>
        public async Task CreateSimpleRuleFile()
        {
            var ruleContent = @"rules:
  - name: SimpleRule
    description: A simple test rule
    conditions:
      all:
        - condition:
            type: comparison
            sensor: input:a
            operator: '>'
            value: 0
    actions:
      - set_value:
          key: output:sum
          value_expression: 'input:a + input:b'
";
            var ruleFilePath = Path.Combine(_testOutputPath, "simple-rule.yaml");
            await File.WriteAllTextAsync(ruleFilePath, ruleContent);
            _logger.LogInformation("Created simple rule file at {RuleFilePath}", ruleFilePath);
            _logger.LogInformation("Rule content: {RuleContent}", ruleContent);
        }

        /// <summary>
        /// Creates a complex rule file for testing
        /// </summary>
        public async Task CreateComplexRuleFile()
        {
            var ruleContent = @"rules:
  - name: ComplexRule
    description: A complex test rule with nested conditions
    conditions:
      all:
        - condition:
            type: comparison
            sensor: input:a
            operator: '>'
            value: 0
        - condition:
            type: comparison
            sensor: input:b
            operator: '>'
            value: 0
        - or:
            - condition:
                type: comparison
                sensor: input:c
                operator: '>'
                value: 10
            - and:
                - condition:
                    type: comparison
                    sensor: input:c
                    operator: '>'
                    value: 0
                - condition:
                    type: comparison
                    sensor: input:c
                    operator: '<'
                    value: 5
    actions:
      - set_value:
          key: output:complex
          value_expression: 'input:a * input:b + input:c'
";
            var ruleFilePath = Path.Combine(_testOutputPath, "complex-rule.yaml");
            await File.WriteAllTextAsync(ruleFilePath, ruleContent);
            _logger.LogInformation("Created complex rule file at {RuleFilePath}", ruleFilePath);
            _logger.LogInformation("Rule content: {RuleContent}", ruleContent);
        }

        /// <summary>
        /// Creates a system config file for testing
        /// </summary>
        public async Task CreateSystemConfigFile()
        {
            var configContent = GetSystemConfigContent();
            var configFilePath = Path.Combine(_testOutputPath, "system_config.yaml");
            await File.WriteAllTextAsync(configFilePath, configContent);
            _logger.LogInformation("Created system config file at {ConfigFilePath}", configFilePath);
        }

        /// <summary>
        /// Gets the content for a system config file
        /// </summary>
        public string GetSystemConfigContent()
        {
            return $@"validSensors:
  - name: test
    type: number

redis:
  endpoints:
    - localhost:{_redisContainer?.GetMappedPublicPort(6379)}

  poolSize: 8

  retryCount: 3

  retryBaseDelayMs: 100

  connectTimeout: 5000

  syncTimeout: 1000

  keepAlive: 60

  password: null


  ssl: false


  allowAdmin: false


bufferCapacity: 100";
        }

        public Dictionary<string, object> GetRedisConfiguration()
        {
            if (_redisContainer == null)
            {
                throw new InvalidOperationException("Redis container is not initialized");
            }

            return RedisConfigHelper.CreateRedisConfig(_redisContainer);
        }
    }
}
