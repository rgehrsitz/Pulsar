// File: Pulsar.Compiler/Config/BuildOrchestrator.cs

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Pulsar.Compiler.Core;
using Pulsar.Compiler.Generation;
using Pulsar.Compiler.Models;
using Serilog;

namespace Pulsar.Compiler.Config
{
    public class BuildOrchestrator
    {
        private readonly ILogger _logger;
        private readonly CompilationPipeline _pipeline;

        public BuildOrchestrator()
        {
            _logger = LoggingConfig.GetLogger();
            _pipeline = new CompilationPipeline(new AOTRuleCompiler(), new Parsers.DslParser());
        }

        public BuildResult BuildProject(BuildConfig config)
        {
            try
            {
                _logger.Information("Starting build for project: {ProjectName}", config.ProjectName);

                var result = new BuildResult
                {
                    Success = true,
                    OutputPath = config.OutputPath,
                    Metrics = new RuleMetrics()
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
                    Errors = new List<string> { ex.Message }.ToArray()
                };
            }
        }
    }
}
