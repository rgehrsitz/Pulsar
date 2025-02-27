// File: Pulsar.Compiler/Core/CompilationPipeline.cs

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Pulsar.Compiler.Models;
using Pulsar.Compiler.Core;
using Pulsar.Compiler.Generation;
using Pulsar.Compiler.Config;
using Pulsar.Compiler.Parsers;
using Serilog;

namespace Pulsar.Compiler.Core
{
    public class CompilationPipeline
    {
        private readonly IRuleCompiler _compiler;
        private readonly DslParser _parser;
        private readonly ILogger _logger;

        public CompilationPipeline(IRuleCompiler compiler, DslParser parser)
        {
            _compiler = compiler;
            _parser = parser;
            _logger = LoggingConfig.GetLogger();
        }

        public CompilationResult ProcessRules(string rulesPath, CompilerOptions options)
        {
            try
            {
                _logger.Information("Starting rule compilation pipeline for {Path}", rulesPath);

                var rules = LoadRulesFromPaths(rulesPath, options.ValidSensors);
                _logger.Information("Loaded {Count} rules from {Path}", rules.Count, rulesPath);

                var result = _compiler.Compile(rules.ToArray(), options);
                if (result.Success)
                {
                    _logger.Information("Successfully compiled {Count} rules", rules.Count);
                }
                else
                {
                    _logger.Error("Rule compilation failed with {Count} errors", result.Errors.Count);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error in compilation pipeline");
                return new CompilationResult { Success = false, Errors = new List<string> { ex.Message } };
            }
        }
        
        public CompilationResult ProcessRules(List<RuleDefinition> rules, CompilerOptions options)
        {
            try
            {
                _logger.Information("Starting rule compilation pipeline for {Count} predefined rules", rules.Count);

                var result = _compiler.Compile(rules.ToArray(), options);
                if (result.Success)
                {
                    _logger.Information("Successfully compiled {Count} rules", rules.Count);
                }
                else
                {
                    _logger.Error("Rule compilation failed with {Count} errors", result.Errors.Count);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error in compilation pipeline");
                return new CompilationResult { Success = false, Errors = new List<string> { ex.Message } };
            }
        }

        private List<RuleDefinition> LoadRulesFromPaths(string rulesPath, List<string> validSensors)
        {
            try
            {
                var rules = new List<RuleDefinition>();

                if (System.IO.Directory.Exists(rulesPath))
                {
                    _logger.Debug("Loading rules from directory: {Path}", rulesPath);
                    var files = System.IO.Directory.GetFiles(rulesPath, "*.yaml", System.IO.SearchOption.AllDirectories);
                    foreach (var file in files)
                    {
                        _logger.Debug("Processing rule file: {File}", file);
                        var content = System.IO.File.ReadAllText(file);
                        rules.AddRange(_parser.ParseRules(content, validSensors, file));
                    }
                }
                else if (System.IO.File.Exists(rulesPath))
                {
                    _logger.Debug("Loading rules from file: {Path}", rulesPath);
                    var content = System.IO.File.ReadAllText(rulesPath);
                    rules.AddRange(_parser.ParseRules(content, validSensors, rulesPath));
                }
                else
                {
                    throw new System.IO.FileNotFoundException($"Rules path not found: {rulesPath}");
                }

                if (!rules.Any())
                {
                    throw new InvalidOperationException("No rules found in the specified path(s)");
                }

                return rules;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error loading rules from {Path}", rulesPath);
                throw;
            }
        }
    }
}
