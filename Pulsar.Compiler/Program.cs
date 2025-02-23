// File: Pulsar.Compiler/Program.cs

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Pulsar.Compiler.Config;
using Pulsar.Compiler.Core;
using Pulsar.Compiler.Generation;
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
            var command = options.GetValueOrDefault("command", "compile");

            ValidateRequiredOptions(options);

            switch (command)
            {
                case "compile":
                    return await CompileRules(options, _logger);
                case "validate":
                    return await ValidateRules(options, _logger);
                case "init":
                    return await InitializeProject(options, _logger) ? 0 : 1;
                case "generate":
                    return await GenerateBuildableProject(options, _logger) ? 0 : 1;
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

    public static async Task<bool> GenerateBuildableProject(Dictionary<string, string> options, ILogger logger)
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

            return true;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to generate buildable project");
            return false;
        }
    }

    public static async Task<bool> InitializeProject(Dictionary<string, string> options, ILogger logger)
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
        logger.Information("Pulsar Rule Compiler");
        logger.Information("");
        logger.Information("Usage:");
        logger.Information("  Step 1: Generate buildable project");
        logger.Information(
            "    pulsar generate --rules <path> --config <config.yaml> --output <dir>"
        );
        logger.Information("");
        logger.Information("  Step 2: Build AOT runtime (run from output directory)");
        logger.Information(
            "    dotnet publish -c Release -r <linux-x64|win-x64|osx-x64> --self-contained true"
        );
        logger.Information("");
        logger.Information("Options:");
        logger.Information("  --rules <path>       Path to YAML rule file(s)");
        logger.Information("  --config <path>      Path to system configuration file");
        logger.Information("  --output <dir>       Output directory for generated project");
        logger.Information("  --debug              Include debug symbols and enhanced logging");
        logger.Information("");
        logger.Information("Examples:");
        logger.Information(
            "  pulsar generate --rules ./rules/myrules.yaml --config system_config.yaml --output ./runtime"
        );
        logger.Information(
            "  cd runtime && dotnet publish -c Release -r linux-x64 --self-contained true"
        );
    }

    private static Dictionary<string, string> ParseArguments(string[] args)
    {
        var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 1; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--"))
            {
                throw new ArgumentException($"Invalid argument format: {args[i]}");
            }

            var key = args[i].Substring(2);

            // Handle flags without values
            if (IsFlagOption(key))
            {
                options[key] = "true";
                continue;
            }

            // Handle options with values
            if (i + 1 >= args.Length)
            {
                throw new ArgumentException($"Missing value for argument: {args[i]}");
            }

            options[key] = args[++i];
        }

        ValidateRequiredOptions(options);
        return options;
    }

    private static bool IsFlagOption(string option)
    {
        return option switch
        {
            "aot" or "debug" or "parallel" or "emit-sourcemap" => true,
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
            ValidSensors = new List<string>(), // Optionally set valid sensors if available
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
            ProjectName = options.GetValueOrDefault("project", "Pulsar.Compiler"),
            TargetFramework = options.GetValueOrDefault("targetframework", "net9.0"),
            RulesPath = options.GetValueOrDefault("rules", "Rules"),
            MaxRulesPerFile = int.Parse(options.GetValueOrDefault("max-rules", "100")),
            GenerateDebugInfo = options.GetValueOrDefault("debug") == "true",
            StandaloneExecutable = true,
            Namespace = "Pulsar.Runtime.Rules",
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
            result.Manifest.Rules,
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

    private static async Task<int> ValidateRules(Dictionary<string, string> options, ILogger logger)
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
}
