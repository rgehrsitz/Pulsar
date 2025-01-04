using System.CommandLine;
using Pulsar.Compiler;
using Pulsar.RuleDefinition.Models;
using Serilog;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        var logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .CreateLogger();

        var inputOption = new Option<FileInfo>(
            name: "--input",
            description: "The input YAML file containing rule definitions"
        )
        {
            IsRequired = true,
        };
        inputOption.AddAlias("-i");

        var outputOption = new Option<FileInfo>(
            name: "--output",
            description: "The output file path for generated C# code"
        )
        {
            IsRequired = true,
        };
        outputOption.AddAlias("-o");

        var compileCommand = new Command("compile", "Compile rules from YAML to C#")
        {
            inputOption,
            outputOption,
        };

        compileCommand.SetHandler(CompileRulesAsync, inputOption, outputOption);

        var rootCommand = new RootCommand("Pulsar Rule Compiler") { compileCommand };
        return await rootCommand.InvokeAsync(args);
    }

    private static async Task CompileRulesAsync(FileInfo input, FileInfo output)
    {
        var logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .CreateLogger();

        try
        {
            // Read and parse YAML
            var yaml = await File.ReadAllTextAsync(input.FullName);
            logger.Debug("Read YAML file:\n{Yaml}", yaml);

            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

            // Parse according to DSL spec format with version
            VersionedRuleSet? versionedRuleSet;
            try
            {
                versionedRuleSet = deserializer.Deserialize<VersionedRuleSet>(yaml);
                logger.Debug("Parsed YAML structure: {@RuleSet}", versionedRuleSet);
            }
            catch (YamlDotNet.Core.YamlException ex)
            {
                logger.Error(
                    ex,
                    "Failed to parse YAML file. Please check the format matches the specification."
                );
                logger.Error("YAML parsing error details: {Message}", ex.Message);
                Environment.Exit(1);
                return;
            }

            if (versionedRuleSet == null)
            {
                logger.Error("Failed to deserialize YAML - result was null");
                Environment.Exit(1);
                return;
            }

            if (versionedRuleSet.Version != 1)
            {
                logger.Error("Unsupported rule set version: {Version}", versionedRuleSet.Version);
                Environment.Exit(1);
            }

            if (versionedRuleSet.Rules == null || versionedRuleSet.Rules.Count == 0)
            {
                logger.Error("No rules found in YAML file");
                Environment.Exit(1);
            }

            // Compile rules
            var compiler = new RuleCompiler(logger);
            var (compiledRuleSet, generatedCode) = compiler.CompileRules(versionedRuleSet.Rules);

            // Write output
            await File.WriteAllTextAsync(output.FullName, generatedCode);
            logger.Information(
                "Successfully compiled {RuleCount} rules to {OutputFile}",
                versionedRuleSet.Rules.Count,
                output.FullName
            );
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error compiling rules");
            Environment.Exit(1);
        }
    }
}

public class VersionedRuleSet
{
    public int Version { get; set; }
    public List<Rule> Rules { get; set; } = new();
}
