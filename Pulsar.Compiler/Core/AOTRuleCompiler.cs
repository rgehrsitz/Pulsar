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

                // Log detailed rule information
                foreach (var rule in rules)
                {
                    _logger.Debug(
                        "Compiling rule: Name={Name}, HasConditions={HasConditions}, ActionCount={ActionCount}",
                        rule.Name,
                        rule.Conditions != null,
                        rule.Actions?.Count ?? 0
                    );
                }

                // Validate rules before compilation
                foreach (var rule in rules)
                {
                    try 
                    {
                        if (rule == null)
                        {
                            throw new ArgumentException("Rule cannot be null");
                        }
                        rule.Validate();
                        _logger.Debug("Rule {Name} validated successfully", rule.Name);
                    }
                    catch (ArgumentException ex)
                    {
                        _logger.Error(ex, "Rule validation failed");
                        return new CompilationResult
                        {
                            Success = false,
                            Errors = new List<string> { ex.Message },
                            GeneratedFiles = Array.Empty<GeneratedFileInfo>()
                        };
                    }
                }

                var analyzer = new DependencyAnalyzer();
                var sortedRules = analyzer.AnalyzeDependencies(rules.ToList());

                _logger.Information("Rule dependencies analyzed and sorted");

                using var generator = new CodeGenerator();
                var generatedFiles = generator.GenerateCSharp(sortedRules, options.BuildConfig);
                _logger.Information("Generated {Count} source files", generatedFiles.Count);

                // Convert relative path to absolute path
                var outputPath = Path.IsPathRooted(options.BuildConfig.OutputPath) 
                    ? options.BuildConfig.OutputPath 
                    : Path.GetFullPath(options.BuildConfig.OutputPath);

                if (!Directory.Exists(outputPath))
                {
                    Directory.CreateDirectory(outputPath);
                }

                // Copy generated files to output directory
                foreach (var file in generatedFiles)
                {
                    var targetPath = Path.Combine(outputPath, file.FileName);
                    File.WriteAllText(targetPath, file.Content);
                    _logger.Debug("Generated file written to {Path}", targetPath);
                }

                _logger.Information("Successfully generated source files in {Path}", outputPath);

                return new CompilationResult
                {
                    Success = true,
                    GeneratedFiles = generatedFiles.ToArray(),
                    OutputPath = outputPath
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
