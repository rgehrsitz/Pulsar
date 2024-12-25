using CommandLine;

namespace Pulsar.CompilerCLI;

public class Options
{
    [Option('c', "config", Required = true, HelpText = "Path to the system configuration YAML file")]
    public string ConfigFile { get; set; } = string.Empty;

    [Option('r', "rules", Required = true, HelpText = "Path to the rules YAML file")]
    public string RulesFile { get; set; } = string.Empty;

    [Option('o', "output", Required = true, HelpText = "Output directory for compiled rules")]
    public string OutputDirectory { get; set; } = string.Empty;

    [Option('n', "namespace", Required = false, Default = "Pulsar.CompiledRules", HelpText = "Namespace for generated code")]
    public string Namespace { get; set; } = "Pulsar.CompiledRules";

    [Option('v', "verbose", Required = false, HelpText = "Set output to verbose")]
    public bool Verbose { get; set; }
}
