// File: Pulsar.Compiler/Core/CompilationPipeline.cs

using Pulsar.Compiler.Exceptions;
using Pulsar.Compiler.Models;
using Pulsar.Compiler.Parsers;
using Serilog;

namespace Pulsar.Compiler.Core
{
    public class CompilationPipeline(IRuleCompiler compiler, DslParser parser)
    {
        private readonly ILogger _logger = LoggingConfig.GetLogger();

        public CompilationResult ProcessRules(string rulesPath, CompilerOptions options)
        {
            try
            {
                _logger.Information("Starting rule compilation pipeline for {Path}", rulesPath);

                var rules = LoadRulesFromPaths(
                    rulesPath,
                    options.ValidSensors,
                    options.AllowInvalidSensors
                );
                _logger.Information("Loaded {Count} rules from {Path}", rules.Count, rulesPath);

                var result = compiler.Compile(rules.ToArray(), options);
                if (result.Success)
                {
                    _logger.Information("Successfully compiled {Count} rules", rules.Count);
                    result.Rules = rules;
                }
                else
                {
                    _logger.Error(
                        "Rule compilation failed with {Count} errors",
                        result.Errors.Count
                    );
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error in compilation pipeline");
                return new CompilationResult
                {
                    Success = false,
                    Errors = new List<string> { ex.Message },
                };
            }
        }

        public CompilationResult ProcessRules(List<RuleDefinition> rules, CompilerOptions options)
        {
            try
            {
                _logger.Information(
                    "Starting rule compilation pipeline for {Count} predefined rules",
                    rules.Count
                );

                var result = compiler.Compile(rules.ToArray(), options);
                if (result.Success)
                {
                    _logger.Information("Successfully compiled {Count} rules", rules.Count);
                }
                else
                {
                    _logger.Error(
                        "Rule compilation failed with {Count} errors",
                        result.Errors.Count
                    );
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error in compilation pipeline");
                return new CompilationResult
                {
                    Success = false,
                    Errors = new List<string> { ex.Message },
                };
            }
        }

        private List<RuleDefinition> LoadRulesFromPaths(
            string rulesPath,
            List<string> validSensors,
            bool allowInvalidSensors
        )
        {
            try
            {
                var rules = new List<RuleDefinition>();

                // Debug validSensors
                _logger.Information(
                    "LoadRulesFromPaths received {Count} valid sensors: {Sensors}",
                    validSensors?.Count ?? 0,
                    string.Join(", ", validSensors ?? new List<string>())
                );

                // Ensure validSensors is not null
                if (validSensors == null)
                {
                    validSensors = new List<string>();
                    _logger.Warning(
                        "ValidSensors was null in LoadRulesFromPaths, creating new empty list"
                    );
                }

                // Manually add required sensors if not already in the list
                var requiredSensors = new List<string>
                {
                    "temperature_f",
                    "temperature_c",
                    "humidity",
                    "pressure",
                };
                foreach (var sensor in requiredSensors)
                {
                    if (!validSensors.Contains(sensor))
                    {
                        validSensors.Add(sensor);
                        _logger.Warning(
                            "Added missing required sensor in LoadRulesFromPaths: {Sensor}",
                            sensor
                        );
                    }
                }

                _logger.Information(
                    "Final valid sensors list in LoadRulesFromPaths: {Sensors}",
                    string.Join(", ", validSensors)
                );

                if (Directory.Exists(rulesPath))
                {
                    _logger.Debug("Loading rules from directory: {Path}", rulesPath);
                    var files = Directory.GetFiles(
                        rulesPath,
                        "*.yaml",
                        SearchOption.AllDirectories
                    );
                    foreach (var file in files)
                    {
                        try
                        {
                            var content = System.IO.File.ReadAllText(file);
                            var parsedRules = parser.ParseRules(
                                content,
                                validSensors,
                                System.IO.Path.GetFileName(file),
                                allowInvalidSensors
                            );
                            rules.AddRange(parsedRules);
                        }
                        catch (Exception ex) when (ex is not ValidationException)
                        {
                            _logger.Error(ex, "Error parsing rule file: {Path}", file);
                        }
                    }
                }
                else if (File.Exists(rulesPath))
                {
                    _logger.Debug("Loading rules from file: {Path}", rulesPath);
                    var content = System.IO.File.ReadAllText(rulesPath);
                    var parsedRules = parser.ParseRules(
                        content,
                        validSensors,
                        System.IO.Path.GetFileName(rulesPath),
                        allowInvalidSensors
                    );
                    rules.AddRange(parsedRules);
                }
                else
                {
                    _logger.Error("Rules path not found: {Path}", rulesPath);
                }

                return rules;
            }
            catch (ValidationException ex)
            {
                _logger.Error(ex, "Error loading rules from {Path}", rulesPath);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error loading rules from {Path}", rulesPath);
                return new List<RuleDefinition>();
            }
        }
    }
}
