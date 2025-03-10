// File: Pulsar.Compiler/Config/BeaconBuildOrchestrator.cs

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Pulsar.Compiler.Core;
using Pulsar.Compiler.Generation;
using Pulsar.Compiler.Models;
using Serilog;

namespace Pulsar.Compiler.Config
{
    /// <summary>
    /// Orchestrates the build process for the Beacon AOT-compatible solution
    /// </summary>
    public class BeaconBuildOrchestrator
    {
        private readonly ILogger _logger;
        private readonly CompilationPipeline _pipeline;
        private readonly BeaconTemplateManagerFixed _templateManager;

        public BeaconBuildOrchestrator(BuildConfig config = null)
        {
            _logger = LoggingConfig.GetLogger().ForContext<BeaconBuildOrchestrator>();
            _pipeline = new CompilationPipeline(new AOTRuleCompiler(), new Parsers.DslParser());
            _templateManager = new BeaconTemplateManagerFixed();
        }

        /// <summary>
        /// Builds a complete Beacon solution from the given configuration and rules
        /// </summary>
        public async Task<BuildResult> BuildBeaconAsync(BuildConfig config)
        {
            try
            {
                _logger.Information("Starting Beacon build for project: {ProjectName}", config.ProjectName);

                var result = new BuildResult
                {
                    Success = true,
                    OutputPath = config.OutputPath,
                    Metrics = new RuleMetrics()
                };

                // Ensure output directory exists
                var outputDir = config.OutputDirectory;
                if (string.IsNullOrEmpty(outputDir))
                {
                    throw new ArgumentException("Output directory is not specified in the configuration");
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

                // Get the path to the Beacon.Runtime project directory
                string runtimeOutputDir = Path.Combine(beaconOutputDir, "Beacon.Runtime");
                string generatedOutputDir = Path.Combine(runtimeOutputDir, "Generated");

                // Generate runtime files
                _logger.Information("Generating rule files...");
                var compilerOptions = new CompilerOptions { BuildConfig = config };
                var compilationResult = _pipeline.ProcessRules(config.RuleDefinitions, compilerOptions);

                if (!compilationResult.Success)
                {
                    _logger.Error("Rule compilation failed: {@Errors}", compilationResult.Errors);
                    result.Success = false;
                    result.Errors = compilationResult.Errors.ToArray();
                    return result;
                }

                // Copy the generated files to the appropriate locations
                foreach (var generatedFile in compilationResult.GeneratedFiles)
                {
                    string destPath = Path.Combine(generatedOutputDir, Path.GetFileName(generatedFile.FileName));
                    Directory.CreateDirectory(Path.GetDirectoryName(destPath));
                    File.WriteAllText(destPath, generatedFile.Content);
                    _logger.Debug("Wrote generated file: {FileName}", destPath);
                }

                // Create rule manifest file
                string manifestPath = Path.Combine(generatedOutputDir, "rules.manifest.json");
                var manifestContent = JsonSerializer.Serialize(
                    new { Rules = compilationResult.GeneratedFiles.Select(f => f.FileName).ToList() },
                    new JsonSerializerOptions { WriteIndented = true }
                );
                File.WriteAllText(manifestPath, manifestContent);
                _logger.Information("Created rule manifest at {Path}", manifestPath);

                // Build the solution using dotnet CLI
                _logger.Information("Building Beacon solution at {OutputDir}", beaconOutputDir);
                var solutionPath = Path.Combine(beaconOutputDir, config.SolutionName + ".sln");

                if (!File.Exists(solutionPath))
                {
                    _logger.Error("Solution file not found: {SolutionPath}", solutionPath);
                    result.Success = false;
                    result.Errors = new[] { $"Solution file not found: {solutionPath}" };
                    return result;
                }

                // Run dotnet build on the solution
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "dotnet",
                        Arguments = $"build {solutionPath} -c Release -v detailed",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false
                    }
                };

                _logger.Information("Executing: dotnet build {SolutionPath} -c Release", solutionPath);
                process.Start();
                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                // Log the output and error
                _logger.Debug("Build output: {Output}", output);
                if (!string.IsNullOrWhiteSpace(error))
                {
                    _logger.Debug("Build errors: {Error}", error);
                }

                if (process.ExitCode != 0)
                {
                    _logger.Error("Build process failed with exit code {ExitCode}: {Error}", process.ExitCode, error);
                    result.Success = false;
                    result.Errors = new[] { $"Build process failed: {error}" };
                    return result;
                }

                // If standalone executable is requested, run dotnet publish with the appropriate flags
                if (config.StandaloneExecutable)
                {
                    _logger.Information("Building standalone executable...");
                    var runtimeProject = Path.Combine(runtimeOutputDir, "Beacon.Runtime.csproj");
                    
                    var publishProcess = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "dotnet",
                            Arguments = $"publish {runtimeProject} -c Release -r {config.Target} --self-contained true -v detailed",
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false
                        }
                    };

                    _logger.Information("Executing: dotnet publish {RuntimeProject} -c Release -r {Target} --self-contained true", runtimeProject, config.Target);
                    publishProcess.Start();
                    var publishOutput = await publishProcess.StandardOutput.ReadToEndAsync();
                    var publishError = await publishProcess.StandardError.ReadToEndAsync();
                    await publishProcess.WaitForExitAsync();

                    // Log the output and error
                    _logger.Debug("Publish output: {Output}", publishOutput);
                    if (!string.IsNullOrWhiteSpace(publishError))
                    {
                        _logger.Debug("Publish errors: {Error}", publishError);
                    }

                    if (publishProcess.ExitCode != 0)
                    {
                        _logger.Error("Publish process failed with exit code {ExitCode}: {Error}", publishProcess.ExitCode, publishError);
                        result.Success = false;
                        result.Errors = new[] { $"Publish process failed: {publishError}" };
                        return result;
                    }
                }

                _logger.Information("Beacon build completed successfully");
                
                // Update build result with generated file information
                result.GeneratedFiles = new []
                {
                    solutionPath,
                    Path.Combine(runtimeOutputDir, "Beacon.Runtime.csproj"),
                    Path.Combine(runtimeOutputDir, "Program.cs"),
                    manifestPath
                };
                
                // Add test project path if generated
                if (config.GenerateTestProject)
                {
                    var testFiles = result.GeneratedFiles.ToList();
                    testFiles.Add(Path.Combine(beaconOutputDir, "Beacon.Tests", "Beacon.Tests.csproj"));
                    result.GeneratedFiles = testFiles.ToArray();
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Beacon build failed with exception");
                return new BuildResult
                {
                    Success = false,
                    Errors = new[] { ex.Message }
                };
            }
        }
    }
}