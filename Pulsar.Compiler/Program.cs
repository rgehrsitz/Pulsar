// File: Pulsar.Compiler/Program.cs

using Pulsar.Compiler.Config;
using Pulsar.Compiler.Core;
using Pulsar.Compiler.Models;
using Pulsar.Compiler.Parsers;
using Serilog;

namespace Pulsar.Compiler;

public class Program
{
    private static readonly ILogger _logger = LoggingConfig.GetLogger();

    public static async Task<int> Main(string[] args)
    {
        try
        {
            var options = ParseArguments(args);

            if (options.Count == 0 || !options.ContainsKey("command"))
            {
                PrintUsage(_logger);
                return 1;
            }

            var command = options["command"];

            try
            {
                ValidateRequiredOptions(options);
            }
            catch (ArgumentException ex)
            {
                _logger.Error(ex.Message);

                // Print specific usage based on the command
                switch (command)
                {
                    case "beacon":
                        PrintBeaconUsage(_logger);
                        break;
                    default:
                        PrintUsage(_logger);
                        break;
                }

                return 1;
            }

            switch (command)
            {
                case "test":
                    var testCommand = new Commands.TestCommand(_logger);
                    return await testCommand.RunAsync(options) ? 0 : 1;
                case "compile":
                    return await CompileRules(options, _logger);
                case "validate":
                    return await ValidateRules(options, _logger);
                case "init":
                    return await InitializeProject(options, _logger) ? 0 : 1;
                case "generate":
                    return await GenerateBuildableProject(options, _logger) ? 0 : 1;
                case "beacon":
                    return await GenerateBeaconSolution(options, _logger) ? 0 : 1;
                default:
                    _logger.Error("Unknown command: {Command}", command);
                    PrintUsage(_logger);
                    return 1;
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Unhandled exception occurred.");
            return 1;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    public static async Task<bool> GenerateBuildableProject(
        Dictionary<string, string> options,
        ILogger logger
    )
    {
        logger.Information("Generating buildable project...");

        try
        {
            var buildConfig = CreateBuildConfig(options);
            var systemConfig = await LoadSystemConfig(
                options.GetValueOrDefault("config", "system_config.yaml")
            );
            logger.Information(
                "System configuration loaded. Valid sensors: {ValidSensors}",
                string.Join(", ", systemConfig.ValidSensors)
            );

            // Use the new CompilationPipeline instead of BuildTimeOrchestrator.
            var compilerOptions = new Models.CompilerOptions
            {
                BuildConfig = buildConfig,
                ValidSensors = systemConfig.ValidSensors.ToList(),
            };
            var pipeline = new Core.CompilationPipeline(
                new Core.AOTRuleCompiler(),
                new Parsers.DslParser()
            );
            var result = pipeline.ProcessRules(options["rules"], compilerOptions);
            if (!result.Success)
            {
                foreach (var error in result.Errors)
                {
                    logger.Error(error);
                }
                return false;
            }

            // Generate project files
            var templateManager = new TemplateManager();
            templateManager.GenerateProjectFiles(buildConfig.OutputPath, buildConfig);

            return true;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to generate buildable project");
            return false;
        }
    }

    public static async Task<bool> InitializeProject(
        Dictionary<string, string> options,
        ILogger logger
    )
    {
        var outputPath = options.GetValueOrDefault("output", ".");

        try
        {
            // Create directory structure
            Directory.CreateDirectory(outputPath);
            Directory.CreateDirectory(Path.Combine(outputPath, "rules"));
            Directory.CreateDirectory(Path.Combine(outputPath, "config"));

            // Create example rule file
            var exampleRulePath = Path.Combine(outputPath, "rules", "example.yaml");
            await File.WriteAllTextAsync(
                exampleRulePath,
                @"rules:
  - name: 'TemperatureConversion'
    description: 'Converts temperature from Fahrenheit to Celsius'
    conditions:
      all:
        - condition:
            type: comparison
            sensor: 'temperature_f'
            operator: '>'
            value: -459.67  # Absolute zero check
    actions:
      - set_value:
          key: 'temperature_c'
          value_expression: '(temperature_f - 32) * 5/9'

  - name: 'HighTemperatureAlert'
    description: 'Alerts when temperature exceeds threshold for duration'
    conditions:
      all:
        - condition:
            type: threshold_over_time
            sensor: 'temperature_c'
            threshold: 30
            duration: 300  # 300ms
    actions:
      - set_value:
          key: 'high_temp_alert'
          value: 1"
            );

            // Create system config file
            var configPath = Path.Combine(outputPath, "config", "system_config.yaml");
            await File.WriteAllTextAsync(
                configPath,
                @"version: 1
validSensors:
  - temperature_f
  - temperature_c
  - high_temp_alert
cycleTime: 100  # ms
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
bufferCapacity: 100"
            );

            // Create build configuration file
            var buildConfigPath = Path.Combine(outputPath, "config", "build_config.yaml");
            await File.WriteAllTextAsync(
                buildConfigPath,
                @"maxRulesPerFile: 100
namespace: Pulsar.Runtime.Rules
generateDebugInfo: true
optimizeOutput: true
complexityThreshold: 100
groupParallelRules: true"
            );

            // Create a README file
            var readmePath = Path.Combine(outputPath, "README.md");
            await File.WriteAllTextAsync(
                readmePath,
                @"# Pulsar Rules Project

This is a newly initialized Pulsar rules project. The directory structure is:

- `rules/` - Contains your YAML rule definitions
- `config/` - Contains system and build configuration
  - `system_config.yaml` - System-wide configuration
  - `build_config.yaml` - Build process configuration

## Getting Started

1. Edit the rules in `rules/example.yaml` or create new rule files
2. Adjust configurations in the `config/` directory
3. Compile your rules:
   ```bash
   pulsar compile --rules ./rules --config ./config/system_config.yaml --output ./output
   ```

4. Build the runtime:
   ```bash
   cd output
   dotnet publish -c Release -r linux-x64 --self-contained true
   ```

## Rule Files

Each rule file should contain one or more rules defined in YAML format.
See `rules/example.yaml` for an example of the rule format.

## Configuration

- `system_config.yaml` defines valid sensors and system-wide settings
- `build_config.yaml` controls the build process and output format

## Additional Information

For more detailed documentation, visit:
https://github.com/yourusername/pulsar/docs"
            );

            logger.Information("Initialized new Pulsar project at {Path}", outputPath);
            logger.Information("Created example rule in rules/example.yaml");
            logger.Information("Created system configuration in config/system_config.yaml");
            logger.Information("See README.md for next steps");

            return true;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to initialize project");
            return false;
        }
    }

    private static void PrintUsage(ILogger logger)
    {
        logger.Information("Usage: Pulsar.Compiler <command> [options]");
        logger.Information("Commands:");
        logger.Information("  compile  - Compile rules into a deployable project");
        logger.Information("  validate - Validate rules without generating code");
        logger.Information("  test     - Test Pulsar rules and configuration files");
        logger.Information("  init     - Initialize a new project");
        logger.Information("  generate - Generate a buildable project");
        logger.Information("  beacon   - Generate an AOT-compatible Beacon solution");
        logger.Information("Options:");
        logger.Information("  --output=<path>  Output directory (required for compile/generate)");
        logger.Information("  --config=<path>  System configuration file (optional)");
        logger.Information(
            "  --target=<id>    Target runtime identifier for AOT (e.g., win-x64, linux-x64)"
        );
        logger.Information("  --verbose        Enable verbose logging");
    }

    private static Dictionary<string, string> ParseArguments(string[] args)
    {
        var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (args.Length == 0)
        {
            return options;
        }

        // First argument is the command
        options["command"] = args[0];

        for (int i = 1; i < args.Length; i++)
        {
            string arg = args[i];

            // Handle different argument formats (--key=value, --key value, or --flag)
            if (arg.StartsWith("--"))
            {
                string key = arg.Substring(2);

                // Handle --key=value format
                if (key.Contains("="))
                {
                    var parts = key.Split('=', 2);
                    options[parts[0]] = parts[1];
                    continue;
                }

                // Handle flags without values
                if (IsFlagOption(key))
                {
                    options[key] = "true";
                    continue;
                }

                // Handle --key value format
                if (i + 1 < args.Length && !args[i + 1].StartsWith("--"))
                {
                    options[key] = args[++i];
                }
                else
                {
                    // If no value is provided and it's not a flag, use an empty string
                    options[key] = "";
                }
            }
            else
            {
                // Non-option arguments (not starting with --)
                if (i == 0)
                {
                    options["command"] = arg;
                }
                else
                {
                    // Handle positional arguments if needed
                    _logger.Warning("Ignoring unexpected positional argument: {Arg}", arg);
                }
            }
        }

        return options;
    }

    private static bool IsFlagOption(string option)
    {
        return option switch
        {
            "aot" or "debug" or "parallel" or "emit-sourcemap" or "verbose" or "clean" => true,
            _ => false,
        };
    }

    private static void ValidateRequiredOptions(Dictionary<string, string> options)
    {
        // Validate based on command context
        var command = options.GetValueOrDefault("command", "compile");

        switch (command)
        {
            case "compile":
                if (!options.ContainsKey("rules"))
                {
                    throw new ArgumentException("--rules argument is required for compilation");
                }
                ValidateCompileOptions(options);
                break;

            case "validate":
                if (!options.ContainsKey("rules"))
                {
                    throw new ArgumentException("--rules argument is required for validation");
                }
                break;

            case "test":
                if (!options.ContainsKey("rules"))
                {
                    throw new ArgumentException("--rules argument is required for testing");
                }
                break;

            case "beacon":
                if (!options.ContainsKey("rules"))
                {
                    throw new ArgumentException(
                        "--rules argument is required for Beacon solution generation"
                    );
                }
                // Validate target runtime if specified
                if (options.TryGetValue("target", out var target))
                {
                    if (!IsValidTarget(target))
                    {
                        throw new ArgumentException($"Invalid target runtime: {target}");
                    }
                }
                break;
        }
    }

    private static void ValidateCompileOptions(Dictionary<string, string> options)
    {
        // Validate target runtime if specified
        if (options.TryGetValue("target", out var target))
        {
            if (!IsValidTarget(target))
            {
                throw new ArgumentException($"Invalid target runtime: {target}");
            }
        }

        // Validate validation level if specified
        if (options.TryGetValue("validation-level", out var level))
        {
            if (!IsValidValidationLevel(level))
            {
                throw new ArgumentException($"Invalid validation level: {level}");
            }
        }

        // Validate numeric options
        if (options.TryGetValue("max-rules", out var maxRules))
        {
            if (!int.TryParse(maxRules, out var value) || value <= 0)
            {
                throw new ArgumentException("max-rules must be a positive integer");
            }
        }
    }

    private static bool IsValidTarget(string target)
    {
        return new[] { "linux-x64", "win-x64", "osx-x64" }.Contains(target);
    }

    private static bool IsValidValidationLevel(string level)
    {
        return new[] { "strict", "normal", "relaxed" }.Contains(level);
    }

    private static async Task<int> CompileRules(Dictionary<string, string> options, ILogger logger)
    {
        logger.Information("Starting rule compilation...");

        var buildConfig = CreateBuildConfig(options);
        var systemConfig = await LoadSystemConfig(
            options.GetValueOrDefault("config", "system_config.yaml")
        );

        var compilerOptions = new Models.CompilerOptions
        {
            BuildConfig = buildConfig,
            ValidSensors = systemConfig.ValidSensors.ToList(),
        };
        var pipeline = new CompilationPipeline(new AOTRuleCompiler(), new DslParser());
        var result = pipeline.ProcessRules(options["rules"], compilerOptions);

        // Generate detailed output based on compilation result
        if (result.Success)
        {
            logger.Information(
                $"Successfully generated {result.GeneratedFiles.Length} files from rules"
            );

            // Log any optimizations or special handling
            if (options.GetValueOrDefault("aot") == "true")
            {
                logger.Information("Generated AOT-compatible code");
            }

            if (options.GetValueOrDefault("debug") == "true")
            {
                logger.Information("Included debug symbols and enhanced logging");
            }

            return 0;
        }
        else
        {
            foreach (var error in result.Errors)
            {
                logger.Error(error);
            }
            return 1;
        }
    }

    private static BuildConfig CreateBuildConfig(Dictionary<string, string> options)
    {
        return new BuildConfig
        {
            OutputPath = options.GetValueOrDefault("output", "Generated"),
            Target = options.GetValueOrDefault("target", "win-x64"),
            ProjectName = "Generated",
            TargetFramework = options.GetValueOrDefault("targetframework", "net9.0"),
            RulesPath = options.GetValueOrDefault("rules", "Rules"),
            MaxRulesPerFile = int.Parse(options.GetValueOrDefault("max-rules", "100")),
            GenerateDebugInfo = options.GetValueOrDefault("debug") == "true",
            StandaloneExecutable = true,
            Namespace = "Generated",
            GroupParallelRules = options.GetValueOrDefault("parallel") == "true",
            OptimizeOutput = options.GetValueOrDefault("aot") == "true",
            ComplexityThreshold = int.Parse(
                options.GetValueOrDefault("complexity-threshold", "100")
            ),
        };
    }

    private static async Task<SystemConfig> LoadSystemConfig(string configPath)
    {
        if (!File.Exists(configPath))
        {
            // Try looking in the parent directory
            string parentPath = Path.Combine(
                Directory.GetParent(Directory.GetCurrentDirectory())?.FullName
                    ?? throw new InvalidOperationException("Parent directory not found"),
                configPath
            );
            if (File.Exists(parentPath))
            {
                configPath = parentPath;
            }
            else
            {
                throw new FileNotFoundException(
                    $"System configuration file not found: {configPath}"
                );
            }
        }

        var yaml = await File.ReadAllTextAsync(configPath);
        var deserializer = new YamlDotNet.Serialization.DeserializerBuilder().Build();
        return deserializer.Deserialize<SystemConfig>(yaml);
    }

    private static async Task GenerateSourceMap(
        BuildResult result,
        string outputPath,
        ILogger logger
    )
    {
        var sourceMapPath = Path.Combine(outputPath, "sourcemap.json");
        var sourceMap = new
        {
            Rules = result.Manifest.Rules,
            Files = result.GeneratedFiles,
            CompilationTime = DateTime.UtcNow,
            SourceFiles = result.Manifest.Rules.Values.Select(r => r.SourceFile).Distinct(),
        };

        await File.WriteAllTextAsync(
            sourceMapPath,
            System.Text.Json.JsonSerializer.Serialize(
                sourceMap,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true }
            )
        );

        logger.Information("Generated source map at {Path}", sourceMapPath);
    }

    public static async Task<int> ValidateRules(Dictionary<string, string> options, ILogger logger)
    {
        logger.Information("Validating rules...");

        var systemConfig = await LoadSystemConfig(
            options.GetValueOrDefault("config", "system_config.yaml")
        );
        var parser = new DslParser();
        var validationLevel = options.GetValueOrDefault("validation-level", "normal");

        try
        {
            var rules = await ParseAndValidateRules(
                options["rules"],
                systemConfig.ValidSensors,
                parser,
                validationLevel,
                logger
            );

            logger.Information("Successfully validated {RuleCount} rules", rules.Count);
            return 0;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Rule validation failed");
            return 1;
        }
    }

    private static async Task<List<RuleDefinition>> ParseAndValidateRules(
        string rulesPath,
        List<string> validSensors,
        DslParser parser,
        string validationLevel,
        ILogger logger
    )
    {
        var rules = new List<RuleDefinition>();
        var ruleFiles = GetRuleFiles(rulesPath);

        foreach (var file in ruleFiles)
        {
            var content = await File.ReadAllTextAsync(file);
            var fileRules = parser.ParseRules(content, validSensors, file);

            // Apply validation based on level
            switch (validationLevel.ToLower())
            {
                case "strict":
                    ValidateRulesStrict(fileRules, logger);
                    break;
                case "relaxed":
                    ValidateRulesRelaxed(fileRules, logger);
                    break;
                default:
                    ValidateRulesNormal(fileRules, logger);
                    break;
            }

            rules.AddRange(fileRules);
        }

        return rules;
    }

    private static List<string> GetRuleFiles(string rulesPath)
    {
        if (File.Exists(rulesPath))
        {
            return new List<string> { rulesPath };
        }

        if (Directory.Exists(rulesPath))
        {
            return Directory
                .GetFiles(rulesPath, "*.yaml", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles(rulesPath, "*.yml", SearchOption.AllDirectories))
                .ToList();
        }

        throw new ArgumentException($"Rules path not found: {rulesPath}");
    }

    private static void ValidateRulesStrict(List<RuleDefinition> rules, ILogger logger)
    {
        foreach (var rule in rules)
        {
            // Require description
            if (string.IsNullOrWhiteSpace(rule.Description))
            {
                throw new ArgumentException(
                    $"Rule {rule.Name} missing description (required in strict mode)"
                );
            }

            // Require at least one condition
            if (rule.Conditions?.All?.Count == 0 && rule.Conditions?.Any?.Count == 0)
            {
                throw new ArgumentException($"Rule {rule.Name} must have at least one condition");
            }

            // Validate action complexity
            if (rule.Actions.Count > 5)
            {
                throw new ArgumentException(
                    $"Rule {rule.Name} has too many actions (max 5 in strict mode)"
                );
            }
        }
    }

    private static void ValidateRulesNormal(List<RuleDefinition> rules, ILogger logger)
    {
        foreach (var rule in rules)
        {
            // Warning for missing description
            if (string.IsNullOrWhiteSpace(rule.Description))
            {
                logger.Warning("Rule {RuleName} missing description", rule.Name);
            }

            // Warning for high action count
            if (rule.Actions.Count > 10)
            {
                logger.Warning(
                    "Rule {RuleName} has a high number of actions ({Count})",
                    rule.Name,
                    rule.Actions.Count
                );
            }
        }
    }

    private static void ValidateRulesRelaxed(List<RuleDefinition> rules, ILogger logger)
    {
        foreach (var rule in rules)
        {
            // Minimal validation, just log warnings for potential issues
            if (string.IsNullOrWhiteSpace(rule.Description))
            {
                logger.Information("Rule {RuleName} missing description", rule.Name);
            }

            if (rule.Actions.Count > 15)
            {
                logger.Information(
                    "Rule {RuleName} has a very high number of actions ({Count})",
                    rule.Name,
                    rule.Actions.Count
                );
            }
        }
    }

    public static async Task<bool> GenerateBeaconSolution(
        Dictionary<string, string> options,
        Serilog.ILogger logger
    )
    {
        logger.Information("Generating AOT-compatible Beacon solution...");

        try
        {
            var rulesPath = options.GetValueOrDefault("rules", null);
            var configPath = options.GetValueOrDefault("config", "system_config.yaml");
            var outputPath = options.GetValueOrDefault("output", ".");
            var target = options.GetValueOrDefault("target", "win-x64");
            var verbose = options.ContainsKey("verbose");

            if (string.IsNullOrEmpty(rulesPath))
            {
                logger.Error("Rules path not specified");
                PrintBeaconUsage(logger);
                return false;
            }

            // Parse system config
            if (!File.Exists(configPath))
            {
                logger.Error("System configuration file not found: {Path}", configPath);
                return false;
            }

            // Create output directory if it doesn't exist
            if (!Directory.Exists(outputPath))
            {
                logger.Information("Creating output directory: {Path}", outputPath);
                Directory.CreateDirectory(outputPath);
            }
            else
            {
                logger.Information("Output directory already exists: {Path}", outputPath);
                // Clean the output directory if it's not empty
                var files = Directory.GetFiles(outputPath);
                if (files.Length > 0)
                {
                    logger.Information("Cleaning output directory...");
                    foreach (var file in files)
                    {
                        File.Delete(file);
                    }
                }
            }

            // Load system config using proper method
            logger.Information("Loading system config from {Path}", configPath);
            if (verbose)
            {
                var configContent = await File.ReadAllTextAsync(configPath);
                logger.Debug("Config file content:\n{Content}", configContent);
            }

            var systemConfig = SystemConfig.Load(configPath);

            // Debug validSensors
            logger.Information(
                "System configuration loaded with {SensorCount} valid sensors: {Sensors}",
                systemConfig.ValidSensors?.Count ?? 0,
                string.Join(", ", systemConfig.ValidSensors ?? new List<string>())
            );

            // Ensure validSensors is not null and contains required sensors
            if (systemConfig.ValidSensors == null)
            {
                systemConfig.ValidSensors = new List<string>();
                logger.Warning("ValidSensors was null, creating new empty list");
            }

            // Manually add required sensors if they're not already in the list
            var requiredSensors = new List<string>
            {
                "temperature_f",
                "temperature_c",
                "humidity",
                "pressure",
            };
            foreach (var sensor in requiredSensors)
            {
                if (!systemConfig.ValidSensors.Contains(sensor))
                {
                    systemConfig.ValidSensors.Add(sensor);
                    logger.Warning("Added missing required sensor: {Sensor}", sensor);
                }
            }

            logger.Information(
                "Final valid sensors list: {Sensors}",
                string.Join(", ", systemConfig.ValidSensors)
            );

            // Parse rules
            var parser = new DslParser();
            var rules = new List<RuleDefinition>();

            // Create compiler options with validation disabled
            var compilerOptions = new CompilerOptions
            {
                ValidSensors = systemConfig.ValidSensors,
                AllowInvalidSensors = true, // Bypass sensor validation
                OutputDirectory = outputPath,
                TargetFramework = "net9.0",
                RuntimeIdentifier = target,
            };

            // Try to load rules directly first
            if (File.Exists(rulesPath))
            {
                var content = await File.ReadAllTextAsync(rulesPath);
                var parsedRules = parser.ParseRules(
                    content,
                    systemConfig.ValidSensors,
                    Path.GetFileName(rulesPath),
                    true
                );
                rules.AddRange(parsedRules);
            }
            else if (Directory.Exists(rulesPath))
            {
                foreach (
                    var file in Directory.GetFiles(rulesPath, "*.yaml", SearchOption.AllDirectories)
                )
                {
                    var content = await File.ReadAllTextAsync(file);
                    var parsedRules = parser.ParseRules(
                        content,
                        systemConfig.ValidSensors,
                        Path.GetFileName(file),
                        true
                    );
                    rules.AddRange(parsedRules);
                }
            }
            else
            {
                logger.Error("Rules path not found: {Path}", rulesPath);
                return false;
            }

            logger.Information("Parsed {Count} rules", rules.Count);

            if (rules.Count == 0)
            {
                // If no rules were parsed directly, try using the compilation pipeline
                logger.Warning(
                    "No rules were parsed directly, attempting to use compilation pipeline"
                );
                var pipeline = new CompilationPipeline(new AOTRuleCompiler(), new DslParser());
                var compilationResult = pipeline.ProcessRules(rulesPath, compilerOptions);

                if (!compilationResult.Success)
                {
                    logger.Error("No rules could be parsed from the specified path");
                    return false;
                }

                rules = compilationResult.Rules;
            }

            // Generate the Beacon solution
            var buildConfig = new BuildConfig
            {
                OutputPath = outputPath,
                Target = target,
                ProjectName = "Beacon.Runtime",
                AssemblyName = "Beacon.Runtime",
                TargetFramework = "net9.0",
                RulesPath = rulesPath,
                RuleDefinitions = rules,
                SystemConfig = systemConfig, // Ensure SystemConfig is set here
                StandaloneExecutable = true,
                GenerateDebugInfo = false,
                OptimizeOutput = true,
                Namespace = "Beacon.Runtime",
                RedisConnection = 
                    systemConfig.Redis != null && systemConfig.Redis.TryGetValue("endpoints", out var endpoints) && endpoints is List<string> endpointList && endpointList.Count > 0
                        ? endpointList[0].ToString()
                        : "localhost:6379",
                CycleTime = systemConfig.CycleTime,
                BufferCapacity = systemConfig.BufferCapacity,
                MaxRulesPerFile = 50,
                MaxLinesPerFile = 1000,
                ComplexityThreshold = 10,
                GroupParallelRules = true,
                GenerateTestProject = true,
                CreateSeparateDirectory = true,
                SolutionName = "Beacon",
            };

            // Double-check that system config is properly set
            if (buildConfig.SystemConfig == null)
            {
                logger.Warning("SystemConfig is null in BuildConfig, initializing it");
                buildConfig.SystemConfig = systemConfig;
            }

            // Ensure validSensors is populated
            if (
                buildConfig.SystemConfig.ValidSensors == null
                || buildConfig.SystemConfig.ValidSensors.Count == 0
            )
            {
                logger.Warning("ValidSensors is empty in SystemConfig, adding required sensors");
                buildConfig.SystemConfig.ValidSensors = new List<string>
                {
                    "temperature_f",
                    "temperature_c",
                    "humidity",
                    "pressure",
                };
            }

            var orchestrator = new BeaconBuildOrchestrator();
            var buildResult = await orchestrator.BuildBeaconAsync(buildConfig);

            if (buildResult.Success)
            {
                logger.Information("Beacon solution generated successfully");

                // Use the GeneratedFiles from the BuildResult
                if (buildResult.GeneratedFiles != null && buildResult.GeneratedFiles.Length > 0)
                {
                    logger.Information("Generated {Count} files:", buildResult.GeneratedFiles.Length);
                    foreach (var file in buildResult.GeneratedFiles)
                    {
                        var fileInfo = new FileInfo(file);
                        logger.Information(
                            "  {Name} ({Size} bytes)",
                            Path.GetFileName(file),
                            fileInfo.Length
                        );
                    }
                }
                else
                {
                    // Fallback to listing files in the output directory if GeneratedFiles is empty
                    var beaconRuntimeGenDir = Path.Combine(outputPath, "Beacon", "Beacon.Runtime", "Generated");
                    if (Directory.Exists(beaconRuntimeGenDir))
                    {
                        var generatedFiles = Directory.GetFiles(beaconRuntimeGenDir);
                        logger.Information("Generated {Count} files:", generatedFiles.Length);
                        foreach (var file in generatedFiles)
                        {
                            var fileInfo = new FileInfo(file);
                            logger.Information(
                                "  {Name} ({Size} bytes)",
                                Path.GetFileName(file),
                                fileInfo.Length
                            );
                        }
                    }
                }

                return true;
            }
            else
            {
                logger.Error("Failed to generate Beacon solution:");
                foreach (var error in buildResult.Errors)
                {
                    logger.Error("  {Error}", error);
                }
                return false;
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error generating Beacon solution");
            return false;
        }
    }

    private static void PrintBeaconUsage(ILogger logger)
    {
        logger.Information(
            "Usage: dotnet run --project Pulsar.Compiler.csproj beacon --rules <rules-path> --config <config-path> --output <output-path> [--target <runtime-id>] [--verbose]"
        );
        logger.Information("");
        logger.Information("Options:");
        logger.Information(
            "  --rules <path>      Path to YAML rule file or directory containing rule files (required)"
        );
        logger.Information(
            "  --config <path>     Path to system configuration YAML file (default: system_config.yaml)"
        );
        logger.Information(
            "  --output <path>     Output directory for the Beacon solution (default: current directory)"
        );
        logger.Information(
            "  --target <runtime>  Target runtime identifier for AOT compilation (default: win-x64)"
        );
        logger.Information("  --verbose          Enable verbose logging");
        logger.Information("");
        logger.Information("Example:");
        logger.Information(
            "  dotnet run --project Pulsar.Compiler.csproj beacon --rules ./rules.yaml --config ./system_config.yaml --output ./output --target win-x64"
        );
    }
}
