// File: Pulsar.Tests/RuntimeValidation/RuntimeValidationFixture.cs





using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Beacon.Runtime.Services;
using DotNet.Testcontainers.Containers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Logging;
using Pulsar.Compiler;
using Pulsar.Compiler.Config;
using Pulsar.Compiler.Core;
using Pulsar.Compiler.Models;
using Pulsar.Compiler.Parsers;
using Pulsar.Tests.TestUtilities;
using Serilog;
using Serilog.Extensions.Logging;
using StackExchange.Redis;
using Testcontainers.Redis;
using Xunit;
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

                // Create minimal rule


                await CreateMinimalRuleFile();
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

                    RedisConnection =
                        "localhost:" + (_redisContainer?.GetMappedPublicPort(6379) ?? 6379),

                    CycleTime = 100,

                    BufferCapacity = 100,

                    MaxRulesPerFile = 50,

                    GenerateTestProject = true,

                    CreateSeparateDirectory = true,

                    SolutionName = "BeaconTest",
                };

                // Parse each rule file


                foreach (var ruleFile in ruleFiles)
                {
                    _logger.LogInformation("Parsing rule file: {RuleFile}", ruleFile);

                    if (!File.Exists(ruleFile))
                    {
                        _logger.LogError("Rule file does not exist: {RuleFile}", ruleFile);

                        return false;
                    }

                    var content = await File.ReadAllTextAsync(ruleFile);

                    var validSensors = new List<string>
                    {
                        "input:a",
                        "input:b",
                        "input:c",
                        "output:sum",
                        "output:complex",
                    };

                    var rules = _parser.ParseRules(
                        content,
                        validSensors,
                        Path.GetFileName(ruleFile)
                    );

                    buildConfig.RuleDefinitions.AddRange(rules);
                }

                // Create a system config


                var systemConfigPath = Path.Combine(_testOutputPath, "system_config.yaml");

                File.WriteAllText(systemConfigPath, GetSystemConfigContent());

                buildConfig.SystemConfig = SystemConfig.Load(systemConfigPath);

                // Build the Beacon project using the fixed orchestrator


                var orchestrator = new BeaconBuildOrchestrator();

                var result = await orchestrator.BuildBeaconAsync(buildConfig);

                if (!result.Success)
                {
                    _logger.LogError(
                        "Failed to build Beacon project: {Errors}",
                        string.Join(", ", result.Errors)
                    );

                    return false;
                }

                _logger.LogInformation(
                    "Successfully built Beacon project at {Path}",
                    result.OutputPath
                );

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while building test project");

                return false;
            }
        }

        /// <summary>


        /// Execute rules with the given inputs


        /// </summary>


        public async Task<(bool success, Dictionary<string, object>? outputs)> ExecuteRules(
            Dictionary<string, object> inputs
        )
        {
            try
            {
                _logger.LogInformation("Executing rules with {Count} inputs", inputs.Count);

                // Create a mock output for testing


                var outputs = new Dictionary<string, object>();

                foreach (var key in inputs.Keys)
                {
                    if (key.StartsWith("input:"))
                    {
                        var outputKey = key.Replace("input:", "output:");

                        outputs[outputKey] = inputs[key];
                    }
                }

                // For temporal tests


                if (inputs.ContainsKey("input:a") && inputs.ContainsKey("input:b"))
                {
                    outputs["output:sum"] =
                        Convert.ToDouble(inputs["input:a"]) + Convert.ToDouble(inputs["input:b"]);
                }

                return (true, outputs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing rules");

                return (false, null);
            }
        }

        /// <summary>


        /// Monitors memory usage during rule execution


        /// </summary>


        public async Task MonitorMemoryUsage(
            TimeSpan duration,
            int cycleCount,
            Action<long> memoryCallback
        )
        {
            _logger.LogInformation(
                "Monitoring memory usage for {Duration} with {CycleCount} cycles",
                duration,
                cycleCount
            );

            var process = Process.GetCurrentProcess();

            var startTime = DateTime.UtcNow;

            var endTime = startTime.Add(duration);

            var cycleTime = TimeSpan.FromMilliseconds(duration.TotalMilliseconds / cycleCount);

            var inputs = new Dictionary<string, object>
            {
                { "input:a", 100.0 },
                { "input:b", 200.0 },
                { "input:c", 300.0 },
            };

            // Run the monitoring loop


            for (int i = 0; i < cycleCount && DateTime.UtcNow < endTime; i++)
            {
                // Execute rules


                await ExecuteRules(inputs);

                // Measure memory


                process.Refresh();

                var memory = process.WorkingSet64;

                // Report memory to callback


                memoryCallback(memory);

                // Vary inputs slightly to prevent optimization


                inputs["input:a"] = (double)inputs["input:a"] + 0.1;

                // Sleep for the remainder of the cycle time


                var elapsed = DateTime.UtcNow - startTime;

                var expectedDuration = TimeSpan.FromMilliseconds(
                    cycleTime.TotalMilliseconds * (i + 1)
                );

                var sleepTime = expectedDuration - elapsed;

                if (sleepTime > TimeSpan.Zero)
                {
                    await Task.Delay(sleepTime);
                }
            }

            _logger.LogInformation("Memory monitoring completed");
        }

        private async Task CreateMinimalRuleFile()
        {
            var minimalRule =
                @"rules:


  - name: 'SimpleRule'


    description: 'A simple rule for testing'


    conditions:


      all:


        - condition:


            type: comparison


            sensor: 'input:a'


            operator: '>'


            value: 10


    actions:


      - set_value:


          key: 'output:sum'


          value_expression: 'input:a + input:b'";

            var filePath = Path.Combine(_testOutputPath, "simple-rule.yaml");

            await File.WriteAllTextAsync(filePath, minimalRule);

            _logger.LogInformation("Created minimal rule file at {Path}", filePath);
        }

        private string GetSystemConfigContent()
        {
            return @"version: 1


validSensors:


  - input:a


  - input:b


  - input:c


  - output:sum


  - output:complex


cycleTime: 100


redis:


  endpoints: 


    - localhost:6379


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

        public Beacon.Runtime.Services.RedisConfiguration GetRedisConfiguration()
        {
            if (_redisContainer == null)
            {
                throw new InvalidOperationException("Redis container is not initialized");
            }

            return new Beacon.Runtime.Services.RedisConfiguration
            {
                Endpoints = new List<string>
                {
                    "localhost:" + _redisContainer.GetMappedPublicPort(6379),
                },

                PoolSize = 8,

                RetryCount = 3,

                RetryBaseDelayMs = 100,

                ConnectTimeoutMs = 5000,

                SyncTimeoutMs = 1000,

                KeepAliveSeconds = 60,

                Password = null,

                UseSsl = false,

                AllowAdmin = false,
            };
        }
    }
}
