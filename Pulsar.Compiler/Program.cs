// File: Pulsar.Compiler/Program.cs

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Serilog;
using Pulsar.Compiler.Parsers;
using Pulsar.Compiler.Models;
using Pulsar.Compiler.Generation;
using Pulsar.Compiler.Build;

namespace Pulsar.Compiler;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        // Configure logging
        var logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .CreateLogger();

        if (args.Length == 0 || args[0] == "--help" || args[0] == "-h")
        {
            PrintUsage();
            return 0;
        }

        try
        {
            var command = args[0].ToLower();
            var options = ParseArguments(args);

            switch (command)
            {
                case "compile":
                    await CompileRules(options, logger);
                    break;
                default:
                    logger.Error("Unknown command: {Command}", command);
                    PrintUsage();
                    return 1;
            }

            return 0;
        }
        catch (ArgumentException ex)
        {
            logger.Error(ex, "Invalid arguments provided");
            PrintUsage();
            return 1;
        }
        catch (Exception ex)
        {
            logger.Fatal(ex, "Fatal error during compilation");
            return 1;
        }
    }

    private static async Task CompileRules(Dictionary<string, string> options, ILogger logger)
    {
        // Validate required options
        if (!options.TryGetValue("rules", out var rulesPath))
        {
            throw new ArgumentException("--rules argument is required");
        }

        // Load configuration options
        var configPath = options.GetValueOrDefault("config", "system_config.yaml");
        var outputPath = options.GetValueOrDefault("output", "Generated");

        // Load system configuration
        var systemConfig = await LoadSystemConfig(configPath);

        // Create build configuration
        var buildConfig = CreateBuildConfig(options);

        // Create and configure build-time orchestrator
        var orchestrator = new BuildTimeOrchestrator(
            logger,
            systemConfig,
            buildConfig
        );

        try
        {
            // Process rules and generate output
            var result = await orchestrator.ProcessRulesDirectory(rulesPath, outputPath);

            // Log build results
            LogBuildResults(logger, result);

            // Validate and log any warnings or high-complexity rules
            ValidateBuildResult(logger, result);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Rule compilation failed");
            throw;
        }
    }

    private static void LogBuildResults(ILogger logger, BuildResult result)
    {
        // Log basic compilation statistics
        logger.Information(
            "Compilation completed: {RuleCount} rules compiled to {FileCount} files",
            result.Manifest.Rules.Count,
            result.GeneratedFiles.Count
        );

        // Log generated file names
        foreach (var file in result.GeneratedFiles)
        {
            logger.Debug("Generated file: {FileName}", file.FileName);
        }
    }

    private static void ValidateBuildResult(ILogger logger, BuildResult result)
    {
        // Log any warnings
        foreach (var warning in result.Warnings)
        {
            logger.Warning(warning);
        }

        // Check and log high-complexity rules
        foreach (var ruleMetric in result.RuleMetrics)
        {
            if (ruleMetric.Value.EstimatedComplexity > 100) // Example threshold
            {
                logger.Warning(
                    "Rule {RuleName} has high complexity: {Complexity}",
                    ruleMetric.Key,
                    ruleMetric.Value.EstimatedComplexity
                );
            }
        }
    }

    private static async Task<SystemConfig> LoadSystemConfig(string configPath)
    {
        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException($"System configuration file not found: {configPath}");
        }

        var yaml = await File.ReadAllTextAsync(configPath);
        var deserializer = new YamlDotNet.Serialization.DeserializerBuilder()
            .Build();
        return deserializer.Deserialize<SystemConfig>(yaml);
    }

    private static BuildConfig CreateBuildConfig(Dictionary<string, string> options)
    {
        return new BuildConfig
        {
            // Parse options with defaults
            MaxRulesPerFile = int.Parse(options.GetValueOrDefault("max-rules", "100")),
            MaxLinesPerFile = int.Parse(options.GetValueOrDefault("max-lines", "1000")),
            GroupParallelRules = bool.Parse(options.GetValueOrDefault("group-parallel", "true")),
            NamespaceName = options.GetValueOrDefault("namespace", "Pulsar.Generated"),
            GenerateDebugInfo = bool.Parse(options.GetValueOrDefault("debug", "true")),
            ComplexityThreshold = int.Parse(options.GetValueOrDefault("complexity", "100"))
        };
    }

    private static Dictionary<string, string> ParseArguments(string[] args)
    {
        var options = new Dictionary<string, string>();
        for (int i = 1; i < args.Length; i += 2)
        {
            if (i + 1 < args.Length && args[i].StartsWith("--"))
            {
                options[args[i].Substring(2)] = args[i + 1];
            }
            else
            {
                throw new ArgumentException($"Invalid argument format: {args[i]}");
            }
        }
        return options;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Pulsar Rule Compiler");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  compile [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --rules <path>     Single YAML file or directory containing YAML files");
        Console.WriteLine("  --config <path>    Path to system configuration file (default: system_config.yaml)");
        Console.WriteLine("  --output <path>    Output directory for generated source files");
        Console.WriteLine("  --max-rules <n>    Maximum rules per generated file (default: 100)");
        Console.WriteLine("  --max-lines <n>    Maximum lines per generated file (default: 1000)");
        Console.WriteLine("  --group-parallel   Group parallel rules (default: true)");
        Console.WriteLine("  --namespace <ns>   Generated code namespace (default: Pulsar.Generated)");
        Console.WriteLine("  --debug <bool>     Generate debug information (default: true)");
        Console.WriteLine("  --complexity <n>   Rule complexity threshold (default: 100)");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  compile --rules ./rules/myrules.yaml");
        Console.WriteLine("  compile --rules ./rules/ --config system_config.yaml");
    }
}