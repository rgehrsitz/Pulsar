// File: Pulsar.Compiler/Config/BuildOrchestrator.cs

using System.Diagnostics;
using Pulsar.Compiler.Core;
using Pulsar.Compiler.Models;
using Serilog;

namespace Pulsar.Compiler.Config
{
    public class BuildOrchestrator
    {
        private readonly ILogger _logger;
        private readonly CompilationPipeline _pipeline;
        private readonly TemplateManager _templateManager;

        public BuildOrchestrator()
        {
            _logger = LoggingConfig.GetLogger();
            _pipeline = new CompilationPipeline(new AOTRuleCompiler(), new Parsers.DslParser());
            _templateManager = new TemplateManager();
        }

        public BuildOrchestrator(BuildConfig config)
        {
            _logger = LoggingConfig.GetLogger();
            _pipeline = new CompilationPipeline(new AOTRuleCompiler(), new Parsers.DslParser());
            _templateManager = new TemplateManager();
        }

        public BuildResult BuildProject(BuildConfig config)
        {
            try
            {
                _logger.Information(
                    "Starting build for project: {ProjectName}",
                    config.ProjectName
                );

                var result = new BuildResult
                {
                    Success = true,
                    OutputPath = config.OutputPath,
                    Metrics = new RuleMetrics(),
                };

                var compilerOptions = new CompilerOptions { BuildConfig = config };
                var compilationResult = _pipeline.ProcessRules(config.RulesPath, compilerOptions);

                if (!compilationResult.Success)
                {
                    _logger.Error("Build failed with errors: {@Errors}", compilationResult.Errors);
                    result.Success = false;
                    var errorsList = new List<string>(result.Errors);
                    errorsList.AddRange(compilationResult.Errors);
                    result.Errors = errorsList.ToArray();
                    return result;
                }

                _logger.Information("Build completed successfully");
                return result;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Build failed with exception");
                return new BuildResult
                {
                    Success = false,
                    Errors = new List<string> { ex.Message }.ToArray(),
                };
            }
        }

        public async Task<BuildResult> BuildAsync(BuildConfig config)
        {
            try
            {
                _logger.Information(
                    "Starting async build for project: {ProjectName}",
                    config.ProjectName
                );

                // Ensure output directory exists
                var outputDir = config.OutputDirectory ?? config.OutputPath;
                if (string.IsNullOrEmpty(outputDir))
                {
                    throw new ArgumentException(
                        "Output directory is not specified in the configuration"
                    );
                }

                Directory.CreateDirectory(outputDir);

                var result = new BuildResult
                {
                    Success = true,
                    OutputPath = outputDir,
                    Metrics = new RuleMetrics(),
                };

                // Copy templates to output directory
                _logger.Information(
                    "Copying templates to output directory: {OutputDir}",
                    outputDir
                );
                _templateManager.CopyTemplates(outputDir);

                // Generate runtime files
                var compilerOptions = new CompilerOptions { BuildConfig = config };
                var compilationResult = _pipeline.ProcessRules(
                    config.RuleDefinitions,
                    compilerOptions
                );

                if (!compilationResult.Success)
                {
                    _logger.Error("Rule compilation failed: {@Errors}", compilationResult.Errors);
                    result.Success = false;
                    result.Errors = compilationResult.Errors.ToArray();
                    return result;
                }

                // Build the project using dotnet CLI
                _logger.Information("Building project at {OutputDir}", outputDir);
                var projectPath = Path.Combine(outputDir, $"{config.AssemblyName}.csproj");

                if (!File.Exists(projectPath))
                {
                    _logger.Error("Project file not found: {ProjectPath}", projectPath);
                    result.Success = false;
                    result.Errors = new[] { $"Project file not found: {projectPath}" };
                    return result;
                }

                // Run dotnet build
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "dotnet",
                        Arguments = $"build {projectPath} -c Release -v detailed",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                    },
                };

                process.Start();
                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    _logger.Error(
                        "Build process failed with exit code {ExitCode}: {Error}",
                        process.ExitCode,
                        error
                    );
                    result.Success = false;
                    result.Errors = new[] { $"Build process failed: {error}" };
                    return result;
                }

                _logger.Information("Build completed successfully");
                return result;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Build failed with exception");
                return new BuildResult { Success = false, Errors = new[] { ex.Message } };
            }
        }
    }
}
