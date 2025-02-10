// File: Pulse.Compiler/Core/CompilationPipeline.cs

using System;
using System.Collections.Generic;
using System.IO;
using Pulsar.Compiler.Core;
using Pulsar.Compiler.Models;
using Pulsar.Compiler.Parsers;

namespace Pulsar.Compiler.Core
{
    public class CompilationPipeline
    {
        private readonly IRuleCompiler _compiler;
        private readonly DslParser _parser;

        public CompilationPipeline(IRuleCompiler compiler, DslParser parser)
        {
            _compiler = compiler;
            _parser = parser;
        }

        /// <summary>
        /// Process rules from a given YAML file path using provided compiler options.
        /// </summary>
        /// <param name="rulesFilePath">Path to the YAML file that contains the rules.</param>
        /// <param name="options">Compiler options.</param>
        /// <returns>A CompilationResult that summarizes the outcome.</returns>
        public CompilationResult ProcessRules(string rulesFilePath, CompilerOptions options)
        {
            if (!File.Exists(rulesFilePath))
            {
                throw new FileNotFoundException($"Rules file not found at {rulesFilePath}");
            }

            // Read the YAML content
            string yamlContent = File.ReadAllText(rulesFilePath);

            // Parse the YAML to obtain rule definitions
            // Pass the valid sensors from options
            List<RuleDefinition> rules = _parser.ParseRules(
                yamlContent,
                options.ValidSensors.ToList(),
                Path.GetFileName(rulesFilePath)
            );

            // Compile the rules using the provided compiler
            return _compiler.Compile(rules.ToArray(), options);
        }
    }
}
