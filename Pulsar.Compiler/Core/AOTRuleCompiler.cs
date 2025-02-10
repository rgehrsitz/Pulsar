using System;
using System.Collections.Generic;
using System.IO;
using Pulsar.Compiler.Analysis;
using Pulsar.Compiler.Config;
using Pulsar.Compiler.Generation;
using Pulsar.Compiler.Models;

namespace Pulsar.Compiler.Core
{
    public class AOTRuleCompiler : IRuleCompiler
    {
        public CompilationResult Compile(RuleDefinition[] rules, CompilerOptions options)
        {
            // Step 1: Dependency Analysis
            var analyzer = new DependencyAnalyzer();
            List<RuleDefinition> orderedRules = analyzer.AnalyzeDependencies(new List<RuleDefinition>(rules));

            Console.WriteLine($"[AOTRuleCompiler] Received {rules.Length} rules, ordered into {orderedRules.Count} rules after dependency analysis.");

            // Step 2: Source Generation
            // Use provided BuildConfig from options
            BuildConfig buildConfig = options.BuildConfig ?? new BuildConfig
            {
                OutputPath = "Generated",
                Target = "win-x64",
                ProjectName = "Pulsar.Compiler",
                TargetFramework = "net9.0"
            };
            List<GeneratedFileInfo> generatedFiles = CodeGenerator.GenerateCSharp(orderedRules, buildConfig);

            Console.WriteLine($"[AOTRuleCompiler] Generated {generatedFiles.Count} files.");

            // Write generated files to disk (simplified for demonstration)
            List<string> writtenFilePaths = new List<string>();
            Directory.CreateDirectory(buildConfig.OutputDirectory);
            foreach (var file in generatedFiles)
            {
                string filePath = Path.Combine(buildConfig.OutputDirectory, file.FileName);
                File.WriteAllText(filePath, file.Content);
                writtenFilePaths.Add(filePath);
            }

            // Step 3: Finalize generation result without compiling
            return new CompilationResult
            {
                Success = true,
                Errors = new string[0],
                GeneratedFiles = writtenFilePaths.ToArray(),
                Assembly = null
            };
        }
    }
}
