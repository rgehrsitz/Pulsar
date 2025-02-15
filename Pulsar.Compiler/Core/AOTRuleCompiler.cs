// File: Pulse.Compiler/Core/AOTRuleCompiler.cs

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Pulsar.Compiler.Analysis;
using Pulsar.Compiler.Config;
using Pulsar.Compiler.Generation;
using Pulsar.Compiler.Models;
using Serilog;

namespace Pulsar.Compiler.Core
{
    public class AOTRuleCompiler : IRuleCompiler
    {
        private readonly ILogger _logger;

        public AOTRuleCompiler()
        {
            _logger = LoggingConfig.GetLogger();
        }

        public CompilationResult Compile(RuleDefinition[] rules, CompilerOptions options)
        {
            try
            {
                _logger.Information("Starting AOT compilation of {Count} rules", rules.Length);

                var analyzer = new DependencyAnalyzer();
                var sortedRules = analyzer.AnalyzeDependencies(rules.ToList());

                _logger.Information("Rule dependencies analyzed and sorted");

                var generatedFiles = CodeGenerator.GenerateCSharp(sortedRules, options.BuildConfig);
                _logger.Information("Generated {Count} source files", generatedFiles.Count);

                // Compile the generated files using Roslyn
                RoslynCompiler.CompileSource(generatedFiles, options.BuildConfig.OutputDllPath, options.BuildConfig.Debug);
                _logger.Information("Successfully compiled rules to {Path}", options.BuildConfig.OutputDllPath);

                return new CompilationResult
                {
                    Success = true,
                    GeneratedFiles = generatedFiles.ToArray(),
                    OutputPath = options.BuildConfig.OutputDllPath
                };
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "AOT compilation failed");
                return new CompilationResult
                {
                    Success = false,
                    Errors = new List<string> { ex.Message },
                    GeneratedFiles = Array.Empty<GeneratedFileInfo>()
                };
            }
        }
    }
}
