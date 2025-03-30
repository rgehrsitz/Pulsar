// File: Pulsar.Compiler/Config/BeaconBuildOrchestrator.cs
// NOTE: This implementation includes AOT compatibility fixes.

using Pulsar.Compiler.Generation; // Using CodeGenerator now
using Pulsar.Compiler.Models;
using Serilog;

namespace Pulsar.Compiler.Config
{
    /// <summary>
    /// Beacon build orchestrator with AOT compatibility support
    /// </summary>
    public class BeaconBuildOrchestrator
    {
        private readonly ILogger _logger = LoggingConfig.GetLogger().ForContext<BeaconBuildOrchestrator>();
        private readonly BeaconTemplateManager _templateManager = new();

        /// <summary>
        /// Builds a complete Beacon solution from the given configuration and rules
        /// </summary>
        public Task<BuildResult> BuildBeaconAsync(BuildConfig config)
        {
            try
            {
                _logger.Information(
                    "Starting Beacon build for project: {ProjectName}",
                    config.ProjectName
                );

                return Task.Run(() =>
                {
                    var result = new BuildResult
                    {
                        Success = true,
                        OutputPath = config.OutputPath,
                        Metrics = new RuleMetrics(),
                    };

                    // Ensure output directory exists
                    var outputDir = config.OutputDirectory;
                    if (string.IsNullOrEmpty(outputDir))
                    {
                        throw new ArgumentException(
                            "Output directory is not specified in the configuration"
                        );
                    }

                    // Clean existing files if they exist to avoid conflicts
                    if (Directory.Exists(outputDir))
                    {
                        _logger.Information(
                            "Cleaning existing output directory: {Path}",
                            outputDir
                        );
                        try
                        {
                            Directory.Delete(outputDir, true);
                            Directory.CreateDirectory(outputDir);
                        }
                        catch (Exception ex)
                        {
                            _logger.Warning(
                                "Could not clean output directory: {Error}",
                                ex.Message
                            );
                        }
                    }

                    // Create a beacon directory inside the output directory
                    string beaconOutputDir = outputDir;
                    if (config.CreateSeparateDirectory)
                    {
                        beaconOutputDir = Path.Combine(outputDir, config.SolutionName);
                        Directory.CreateDirectory(beaconOutputDir);
                    }

                    // Create the Beacon solution structure
                    _templateManager.CreateBeaconSolution(beaconOutputDir, config);

                    // Use the code generator to generate rule files
                    var codeGenerator = new CodeGenerator();
                    var generatedFiles = codeGenerator.GenerateCSharp(
                        config.RuleDefinitions,
                        config
                    );

                    // Write the generated files to the output directory
                    var generatedDir = Path.Combine(beaconOutputDir, "Beacon.Runtime", "Generated");
                    if (!Directory.Exists(generatedDir))
                    {
                        Directory.CreateDirectory(generatedDir);
                    }
                    else
                    {
                        // Clean existing generated files to avoid conflicts
                        foreach (var file in Directory.GetFiles(generatedDir))
                        {
                            try
                            {
                                File.Delete(file);
                                _logger.Debug("Deleted existing generated file: {Path}", file);
                            }
                            catch (Exception ex)
                            {
                                _logger.Warning(
                                    "Could not delete generated file {Path}: {Error}",
                                    file,
                                    ex.Message
                                );
                            }
                        }
                    }

                    // Track written file paths for the result
                    var writtenFilePaths = new List<string>();

                    foreach (var file in generatedFiles)
                    {
                        // Skip Program.cs in Generated directory to avoid conflicts
                        if (file.FileName == "Program.cs")
                        {
                            _logger.Information(
                                "Skipping Program.cs in Generated directory to avoid conflicts"
                            );
                            continue;
                        }

                        var filePath = Path.Combine(generatedDir, file.FileName);
                        File.WriteAllText(filePath, file.Content);
                        _logger.Debug("Wrote generated file: {Path}", filePath);
                        writtenFilePaths.Add(filePath);
                    }

                    // Generate rule manifest file
                    var ruleManifest = new RuleManifest { GeneratedAt = DateTime.UtcNow };

                    // Set build metrics
                    ruleManifest.BuildMetrics.TotalRules = config.RuleDefinitions.Count;

                    // Add basic rule metadata
                    foreach (var rule in config.RuleDefinitions)
                    {
                        ruleManifest.Rules[rule.Name] = new RuleMetadata
                        {
                            SourceFile = rule.SourceFile,
                            SourceLineNumber = rule.LineNumber,
                            Description = rule.Description,
                        };
                    }

                    var manifestPath = Path.Combine(generatedDir, "rules.manifest.json");
                    var manifestJson = System.Text.Json.JsonSerializer.Serialize(
                        ruleManifest,
                        new System.Text.Json.JsonSerializerOptions { WriteIndented = true }
                    );
                    File.WriteAllText(manifestPath, manifestJson);
                    _logger.Information("Created rule manifest at {Path}", manifestPath);
                    writtenFilePaths.Add(manifestPath);

                    // Add generated files to the result
                    result.GeneratedFiles = writtenFilePaths.ToArray();
                    _logger.Information("Generated {Count} files:", writtenFilePaths.Count);
                    foreach (var file in writtenFilePaths)
                    {
                        _logger.Information("  - {File}", Path.GetFileName(file));
                    }

                    // Return success result
                    return result;
                });
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Beacon build failed with exception");
                return Task.FromResult(
                    new BuildResult { Success = false, Errors = new[] { ex.Message } }
                );
            }
        }
    }
}
