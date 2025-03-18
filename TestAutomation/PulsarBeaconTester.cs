using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Xunit;
using Xunit.Abstractions;

namespace Pulsar.TestAutomation
{
    /// <summary>
    /// Test scenario definition
    /// </summary>
    public class TestScenario
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public Dictionary<string, object> Inputs { get; set; }
        public List<Dictionary<string, object>> InputSequence { get; set; }
        public Dictionary<string, object> PreSetOutputs { get; set; }
        public Dictionary<string, object> ExpectedOutputs { get; set; }
        public double Tolerance { get; set; } = 0.0001;
    }

    /// <summary>
    /// Root structure for test scenarios file
    /// </summary>
    public class TestScenarios
    {
        public List<TestScenario> Scenarios { get; set; }
    }

    /// <summary>
    /// Automated tester for Pulsar/Beacon end-to-end validation
    /// </summary>
    public class PulsarBeaconTester : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly ILogger _logger;
        private readonly string _workingDirectory;
        private readonly ConnectionMultiplexer _redis;
        private Process _beaconProcess;
        private bool _disposed;

        public PulsarBeaconTester(ITestOutputHelper output, ILogger logger, string workingDirectory)
        {
            _output = output;
            _logger = logger;
            _workingDirectory = workingDirectory;

            // Connect to Redis
            _redis = ConnectionMultiplexer.Connect("localhost:6379");
            _logger.LogInformation("Connected to Redis");
        }

        /// <summary>
        /// Run the complete test pipeline - compile, run, and validate
        /// </summary>
        public async Task<bool> RunEndToEndTests(
            string rulesPath,
            string configPath,
            string scenariosPath
        )
        {
            try
            {
                // 1. Compile the rule files using Pulsar
                if (!await CompileRules(rulesPath, configPath))
                {
                    return false;
                }

                // 2. Start the Beacon process
                if (!await StartBeacon())
                {
                    return false;
                }

                // Allow Beacon time to initialize
                await Task.Delay(2000);

                // 3. Load test scenarios
                var scenarios = LoadTestScenarios(scenariosPath);
                if (scenarios == null || !scenarios.Any())
                {
                    _logger.LogError("No scenarios found in {Path}", scenariosPath);
                    return false;
                }

                // 4. Run each scenario and collect results
                var allPassed = true;
                foreach (var scenario in scenarios)
                {
                    var result = await RunScenario(scenario);
                    allPassed = allPassed && result;

                    // Clear Redis for next scenario
                    await ClearRedisData();
                }

                _logger.LogInformation("End-to-end testing complete. Result: {Result}", allPassed);
                return allPassed;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running end-to-end tests");
                return false;
            }
            finally
            {
                // Clean up
                if (_beaconProcess != null && !_beaconProcess.HasExited)
                {
                    try
                    {
                        _beaconProcess.Kill();
                        _beaconProcess.Dispose();
                        _logger.LogInformation("Beacon process terminated");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error terminating Beacon process");
                    }
                }
            }
        }

        /// <summary>
        /// Compiles rules using Pulsar compiler
        /// </summary>
        private async Task<bool> CompileRules(string rulesPath, string configPath)
        {
            _logger.LogInformation("Compiling rules using Pulsar");

            // Find Pulsar.Compiler.dll
            var compilerPath = FindCompilerPath();
            if (string.IsNullOrEmpty(compilerPath))
            {
                _logger.LogError("Unable to find Pulsar.Compiler.dll");
                return false;
            }

            // Ensure paths are absolute
            rulesPath = Path.GetFullPath(rulesPath);
            configPath = Path.GetFullPath(configPath);

            // Create output directory
            var outputDir = Path.Combine(_workingDirectory, "BeaconOutput");
            Directory.CreateDirectory(outputDir);

            // Run the compiler
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments =
                        $"\"{compilerPath}\" beacon --rules=\"{rulesPath}\" --config=\"{configPath}\" --output=\"{outputDir}\" --verbose",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                },
            };

            _logger.LogInformation(
                "Running Pulsar compiler: {Command} {Args}",
                process.StartInfo.FileName,
                process.StartInfo.Arguments
            );

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            _logger.LogInformation("Pulsar compiler output: {Output}", output);
            if (!string.IsNullOrEmpty(error))
            {
                _logger.LogError("Pulsar compiler error: {Error}", error);
            }

            if (process.ExitCode != 0)
            {
                _logger.LogError(
                    "Pulsar compilation failed with exit code: {ExitCode}",
                    process.ExitCode
                );
                return false;
            }

            _logger.LogInformation(
                "Pulsar compilation completed successfully. Output directory: {Dir}",
                outputDir
            );
            return true;
        }

        /// <summary>
        /// Starts the Beacon process
        /// </summary>
        private async Task<bool> StartBeacon()
        {
            _logger.LogInformation("Starting Beacon process");

            // Look for Beacon.Runtime.dll
            var beaconDir = Path.Combine(_workingDirectory, "BeaconOutput", "Beacon");
            var beaconPath = FindBeaconRuntimePath(beaconDir);

            if (string.IsNullOrEmpty(beaconPath))
            {
                _logger.LogError("Unable to find Beacon.Runtime.dll");
                return false;
            }

            // Create a modified appsettings.json with correct Redis settings
            await CreateAppSettings(Path.GetDirectoryName(beaconPath));

            // Start the Beacon process
            _beaconProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = $"\"{beaconPath}\" --verbose",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    WorkingDirectory = Path.GetDirectoryName(beaconPath),
                },
                EnableRaisingEvents = true,
            };

            _beaconProcess.OutputDataReceived += (sender, args) =>
            {
                if (args.Data != null)
                {
                    _output.WriteLine($"Beacon: {args.Data}");
                }
            };

            _beaconProcess.ErrorDataReceived += (sender, args) =>
            {
                if (args.Data != null)
                {
                    _output.WriteLine($"Beacon Error: {args.Data}");
                }
            };

            _beaconProcess.Start();
            _beaconProcess.BeginOutputReadLine();
            _beaconProcess.BeginErrorReadLine();

            // Give it time to start
            await Task.Delay(1000);

            if (_beaconProcess.HasExited)
            {
                _logger.LogError(
                    "Beacon process exited prematurely with code: {Code}",
                    _beaconProcess.ExitCode
                );
                return false;
            }

            _logger.LogInformation("Beacon process started successfully");
            return true;
        }

        /// <summary>
        /// Load test scenarios from JSON file
        /// </summary>
        private List<TestScenario> LoadTestScenarios(string scenariosPath)
        {
            try
            {
                _logger.LogInformation("Loading test scenarios from {Path}", scenariosPath);

                var json = File.ReadAllText(scenariosPath);
                var scenarios = JsonSerializer.Deserialize<TestScenarios>(
                    json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                );

                _logger.LogInformation("Loaded {Count} test scenarios", scenarios.Scenarios.Count);
                return scenarios.Scenarios;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading test scenarios");
                return new List<TestScenario>();
            }
        }

        /// <summary>
        /// Run a single test scenario
        /// </summary>
        private async Task<bool> RunScenario(TestScenario scenario)
        {
            _logger.LogInformation(
                "Running scenario: {Name} - {Description}",
                scenario.Name,
                scenario.Description
            );

            try
            {
                var db = _redis.GetDatabase();

                // Apply pre-set outputs if specified (useful for testing rule dependencies)
                if (scenario.PreSetOutputs != null && scenario.PreSetOutputs.Count > 0)
                {
                    _logger.LogInformation("Setting pre-defined outputs for dependency testing");
                    foreach (var output in scenario.PreSetOutputs)
                    {
                        var key = output.Key;
                        var value = output.Value;

                        // Set both string and hash representations for compatibility
                        await db.StringSetAsync(key, value.ToString());
                        await db.HashSetAsync(
                            key,
                            new HashEntry[]
                            {
                                new HashEntry("value", value.ToString()),
                                new HashEntry("timestamp", DateTime.UtcNow.Ticks.ToString()),
                            }
                        );

                        _logger.LogInformation("Pre-set {Key} = {Value}", key, value);
                    }
                }

                // Handle single input case
                if (scenario.Inputs != null && scenario.Inputs.Count > 0)
                {
                    foreach (var input in scenario.Inputs)
                    {
                        var key = input.Key;
                        var value = input.Value;

                        // Set both string and hash representations for compatibility
                        await db.StringSetAsync(key, value.ToString());
                        await db.HashSetAsync(
                            key,
                            new HashEntry[]
                            {
                                new HashEntry("value", value.ToString()),
                                new HashEntry("timestamp", DateTime.UtcNow.Ticks.ToString()),
                            }
                        );

                        _logger.LogInformation("Set {Key} = {Value}", key, value);
                    }

                    // Give Beacon time to process the input
                    await Task.Delay(500);
                }

                // Handle input sequence case
                if (scenario.InputSequence != null && scenario.InputSequence.Count > 0)
                {
                    foreach (var step in scenario.InputSequence)
                    {
                        // Get the delay duration for this step, if specified
                        int delayMs = 100; // Default delay
                        if (step.TryGetValue("delayMs", out var delayObj))
                        {
                            delayMs = Convert.ToInt32(delayObj);
                            step.Remove("delayMs"); // Remove so it's not treated as an input
                        }

                        // Set each input value
                        foreach (var input in step)
                        {
                            var key = input.Key;
                            var value = input.Value;

                            // Set both string and hash representations for compatibility
                            await db.StringSetAsync(key, value.ToString());
                            await db.HashSetAsync(
                                key,
                                new HashEntry[]
                                {
                                    new HashEntry("value", value.ToString()),
                                    new HashEntry("timestamp", DateTime.UtcNow.Ticks.ToString()),
                                }
                            );

                            _logger.LogInformation("Set {Key} = {Value}", key, value);
                        }

                        // Wait for the specified delay before continuing to next step
                        await Task.Delay(delayMs);
                    }

                    // Additional time for Beacon to process the final inputs
                    await Task.Delay(500);
                }

                // Check expected outputs
                var passed = true;
                foreach (var expected in scenario.ExpectedOutputs)
                {
                    var key = expected.Key;
                    var expectedValue = expected.Value;

                    // Try to get the value as a hash first (primary format)
                    var hashValue = await db.HashGetAsync(key, "value");
                    string actualValueStr = null;
                    if (!hashValue.IsNull)
                    {
                        actualValueStr = hashValue.ToString();
                    }
                    else
                    {
                        // Fall back to string value
                        var stringValue = await db.StringGetAsync(key);
                        if (!stringValue.IsNull)
                        {
                            actualValueStr = stringValue.ToString();
                        }
                    }

                    if (actualValueStr == null)
                    {
                        _logger.LogError("Output {Key} not found in Redis", key);
                        passed = false;
                        continue;
                    }

                    // Handle different types of expected values
                    bool comparisonResult = false;

                    if (expectedValue is bool expectedBool)
                    {
                        // Boolean comparison
                        if (bool.TryParse(actualValueStr, out var actualBool))
                        {
                            comparisonResult = (actualBool == expectedBool);
                        }
                        else
                        {
                            // Handle common string representations
                            var lowerValue = actualValueStr.ToLower();
                            var actualBoolValue =
                                lowerValue == "true" || lowerValue == "1" || lowerValue == "yes";
                            comparisonResult = (actualBoolValue == expectedBool);
                        }
                    }
                    else if (
                        expectedValue is double
                        || expectedValue is int
                        || expectedValue is float
                        || expectedValue is decimal
                    )
                    {
                        // Numeric comparison with tolerance
                        var expectedNum = Convert.ToDouble(expectedValue);
                        if (double.TryParse(actualValueStr, out var actualNum))
                        {
                            comparisonResult =
                                Math.Abs(actualNum - expectedNum) <= scenario.Tolerance;
                        }
                    }
                    else
                    {
                        // String or other comparison
                        comparisonResult = (actualValueStr == expectedValue.ToString());
                    }

                    if (comparisonResult)
                    {
                        _logger.LogInformation(
                            "Output {Key} = {Value} matches expected {Expected}",
                            key,
                            actualValueStr,
                            expectedValue
                        );
                    }
                    else
                    {
                        _logger.LogError(
                            "Output {Key} = {Value} does not match expected {Expected}",
                            key,
                            actualValueStr,
                            expectedValue
                        );
                        passed = false;
                    }
                }

                // Log final result
                if (passed)
                {
                    _logger.LogInformation("Scenario PASSED: {Name}", scenario.Name);
                }
                else
                {
                    _logger.LogError("Scenario FAILED: {Name}", scenario.Name);
                }

                return passed;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running scenario {Name}", scenario.Name);
                return false;
            }
        }

        /// <summary>
        /// Clears Redis data between tests
        /// </summary>
        private async Task ClearRedisData()
        {
            try
            {
                var db = _redis.GetDatabase();
                var server = _redis.GetServer(_redis.GetEndPoints().First());

                // Get all keys with our prefixes
                var keys = server
                    .Keys(pattern: "input:*")
                    .Union(server.Keys(pattern: "output:*"))
                    .Union(server.Keys(pattern: "buffer:*"))
                    .ToArray();

                if (keys.Any())
                {
                    await db.KeyDeleteAsync(keys);
                    _logger.LogInformation("Cleared {Count} Redis keys", keys.Length);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing Redis data");
            }
        }

        /// <summary>
        /// Creates appsettings.json for Beacon
        /// </summary>
        private async Task CreateAppSettings(string directory)
        {
            var appSettings =
                @"{
  ""Redis"": {
    ""Endpoints"": [ ""localhost:6379"" ],
    ""PoolSize"": 4,
    ""RetryCount"": 3,
    ""RetryBaseDelayMs"": 100,
    ""ConnectTimeout"": 5000,
    ""SyncTimeout"": 1000,
    ""KeepAlive"": 60,
    ""Password"": null
  },
  ""Logging"": {
    ""LogLevel"": {
      ""Default"": ""Information"",
      ""Microsoft"": ""Warning"",
      ""Microsoft.Hosting.Lifetime"": ""Information""
    }
  },
  ""BufferCapacity"": 100,
  ""CycleTimeMs"": 100
}";
            var appSettingsPath = Path.Combine(directory, "appsettings.json");
            await File.WriteAllTextAsync(appSettingsPath, appSettings);
            _logger.LogInformation("Created appsettings.json at {Path}", appSettingsPath);
        }

        /// <summary>
        /// Find the Pulsar.Compiler.dll path
        /// </summary>
        private string FindCompilerPath()
        {
            var searchPaths = new[]
            {
                // Direct path
                Path.Combine(Directory.GetCurrentDirectory(), "Pulsar.Compiler.dll"),
                // Typical project output path
                Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "..",
                    "Pulsar.Compiler",
                    "bin",
                    "Debug",
                    "net9.0",
                    "Pulsar.Compiler.dll"
                ),
                // Solution relative path
                Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "Pulsar.Compiler",
                    "bin",
                    "Debug",
                    "net9.0",
                    "Pulsar.Compiler.dll"
                ),
                // Published path
                Path.Combine(Directory.GetCurrentDirectory(), "publish", "Pulsar.Compiler.dll"),
            };

            foreach (var path in searchPaths)
            {
                if (File.Exists(path))
                {
                    return Path.GetFullPath(path);
                }
            }

            // If not found in expected places, search recursively
            string rootDir = Directory.GetCurrentDirectory();
            while (rootDir != null && Directory.Exists(rootDir))
            {
                var found = Directory.GetFiles(
                    rootDir,
                    "Pulsar.Compiler.dll",
                    SearchOption.AllDirectories
                );
                if (found.Length > 0)
                {
                    return found[0];
                }

                rootDir = Path.GetDirectoryName(rootDir);
            }

            return null;
        }

        /// <summary>
        /// Find the Beacon.Runtime.dll path
        /// </summary>
        private string FindBeaconRuntimePath(string beaconDir)
        {
            if (!Directory.Exists(beaconDir))
            {
                _logger.LogError("Beacon directory not found: {Dir}", beaconDir);
                return null;
            }

            // Try to build the solution first
            TryBuildSolution(beaconDir);

            // Search for the DLL
            var searchPaths = new[]
            {
                // Main project binary
                Path.Combine(
                    beaconDir,
                    "Beacon.Runtime",
                    "bin",
                    "Debug",
                    "net9.0",
                    "Beacon.Runtime.dll"
                ),
                Path.Combine(
                    beaconDir,
                    "Beacon.Runtime",
                    "bin",
                    "Release",
                    "net9.0",
                    "Beacon.Runtime.dll"
                ),
                // Published binary
                Path.Combine(
                    beaconDir,
                    "Beacon.Runtime",
                    "bin",
                    "Debug",
                    "net9.0",
                    "publish",
                    "Beacon.Runtime.dll"
                ),
                Path.Combine(
                    beaconDir,
                    "Beacon.Runtime",
                    "bin",
                    "Release",
                    "net9.0",
                    "publish",
                    "Beacon.Runtime.dll"
                ),
            };

            foreach (var path in searchPaths)
            {
                if (File.Exists(path))
                {
                    return Path.GetFullPath(path);
                }
            }

            // If not found in expected places, search recursively
            var found = Directory.GetFiles(
                beaconDir,
                "Beacon.Runtime.dll",
                SearchOption.AllDirectories
            );
            if (found.Length > 0)
            {
                return found[0];
            }

            return null;
        }

        /// <summary>
        /// Try to build the Beacon solution
        /// </summary>
        private void TryBuildSolution(string solutionDir)
        {
            try
            {
                _logger.LogInformation("Attempting to build Beacon solution at {Dir}", solutionDir);

                // Find the solution file
                string solutionFile = null;
                var solutionFiles = Directory.GetFiles(solutionDir, "*.sln");
                if (solutionFiles.Length > 0)
                {
                    solutionFile = solutionFiles[0];
                }
                else
                {
                    _logger.LogWarning("No solution file found in {Dir}", solutionDir);
                    return;
                }

                // Build the solution
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "dotnet",
                        Arguments = $"build \"{solutionFile}\" -v minimal",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                    },
                };

                process.Start();
                var output = process.StandardOutput.ReadToEndAsync();
                var error = process.StandardError.ReadToEndAsync();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    _logger.LogError("Beacon build failed: {Error}", error.Result);
                }
                else
                {
                    _logger.LogInformation("Beacon build completed successfully");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error building Beacon solution");
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            if (_beaconProcess != null && !_beaconProcess.HasExited)
            {
                try
                {
                    _beaconProcess.Kill();
                    _beaconProcess.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error terminating Beacon process");
                }
            }

            _redis?.Dispose();

            _disposed = true;
        }
    }

    /// <summary>
    /// Example test class that would use the PulsarBeaconTester
    /// </summary>
    public class PulsarBeaconEndToEndTests
    {
        private readonly ITestOutputHelper _output;
        private readonly ILogger _logger;

        public PulsarBeaconEndToEndTests(ITestOutputHelper output)
        {
            _output = output;
            // Initialize logger - in a real implementation you would use the configured test logger
            _logger = new Logger<PulsarBeaconEndToEndTests>(new LoggerFactory());
        }

        [Fact]
        public async Task RunBasicRuleTests()
        {
            var workingDir = Path.GetFullPath(
                Path.Combine(Directory.GetCurrentDirectory(), "TestAutomation")
            );

            using var tester = new PulsarBeaconTester(_output, _logger, workingDir);

            var rulesPath = Path.Combine(workingDir, "test-rules.yaml");
            var configPath = Path.Combine(workingDir, "system_config.yaml");
            var scenariosPath = Path.Combine(workingDir, "test-scenarios.json");

            var result = await tester.RunEndToEndTests(rulesPath, configPath, scenariosPath);

            Assert.True(result, "End-to-end tests should pass");
        }
    }
}
