using CommandLine;
using Pulsar.RuleDefinition.Parser;
using Pulsar.RuleDefinition.Validation;
using Pulsar.Compiler;
using Serilog;
using Serilog.Events;
using System.Text.Json;

namespace Pulsar.CompilerCLI;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        return await Parser.Default.ParseArguments<Options>(args)
            .MapResult(RunCompilerAsync, _ => Task.FromResult(1));
    }

    private static async Task<int> RunCompilerAsync(Options opts)
    {
        // Setup logging
        ConfigureLogging(opts.Verbose);
        var logger = Log.ForContext<Program>();

        try
        {
            // Validate input files exist
            if (!File.Exists(opts.ConfigFile))
            {
                logger.Error("Configuration file not found: {ConfigFile}", opts.ConfigFile);
                return 1;
            }

            if (!File.Exists(opts.RulesFile))
            {
                logger.Error("Rules file not found: {RulesFile}", opts.RulesFile);
                return 1;
            }

            // Create output directory if it doesn't exist
            Directory.CreateDirectory(opts.OutputDirectory);

            Log.Information("Parsing system configuration from {ConfigFile}", opts.ConfigFile);
            var configParser = new SystemConfigParser();
            var systemConfig = configParser.ParseFile(opts.ConfigFile);

            Log.Information("Parsing rules from {RulesFile}", opts.RulesFile);
            var ruleParser = new RuleParser(systemConfig);
            var ruleSet = ruleParser.ParseRulesFromFile(opts.RulesFile);

            // Validate rules
            logger.Information("Validating rules against system configuration");
            var validator = new RuleValidator(systemConfig);
            var validationResult = validator.ValidateRuleSet(ruleSet);

            if (!validationResult.IsValid)
            {
                logger.Error("Rule validation failed:");
                foreach (var error in validationResult.Errors)
                {
                    logger.Error("- {Error}", error);
                }
                return 1;
            }

            // Compile rules
            logger.Information("Compiling rules");
            var compiler = new RuleCompiler(logger, opts.Namespace);
            var (compiledRules, generatedCode) = compiler.CompileRules(ruleSet.Rules);

            // Write output files
            var outputPath = Path.Combine(opts.OutputDirectory, "CompiledRules.cs");
            logger.Information("Writing compiled rules to {OutputPath}", outputPath);
            await File.WriteAllTextAsync(outputPath, generatedCode);

            // Generate metadata
            var metadata = new
            {
                LayerCount = compiledRules.LayerCount,
                Rules = compiledRules.Rules.Select(r => new 
                {
                    r.Rule.Name,
                    r.Layer,
                    InputSensors = r.InputSensors.ToList(),
                    OutputSensors = r.OutputSensors.ToList(),
                    Dependencies = r.Dependencies.ToList()
                }).ToList(),
                InputSensors = compiledRules.AllInputSensors.ToList(),
                OutputSensors = compiledRules.AllOutputSensors.ToList()
            };

            var metadataPath = Path.Combine(opts.OutputDirectory, "RuleMetadata.json");
            logger.Information("Writing rule metadata to {MetadataPath}", metadataPath);
            await File.WriteAllTextAsync(metadataPath, 
                JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true }));

            logger.Information("Compilation completed successfully");
            return 0;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "An error occurred during compilation");
            return 1;
        }
    }

    private static void ConfigureLogging(bool verbose)
    {
        var config = new LoggerConfiguration()
            .WriteTo.Console()
            .MinimumLevel.Information();

        if (verbose)
        {
            config.MinimumLevel.Debug()
                  .MinimumLevel.Override("System", LogEventLevel.Debug)
                  .MinimumLevel.Override("Microsoft", LogEventLevel.Debug);
        }

        Log.Logger = config.CreateLogger();
    }
}
