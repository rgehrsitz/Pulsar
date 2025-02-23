// File: Pulse.Compiler/Core/AOTRuleCompiler.cs

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Pulsar.Compiler.Models;
using Pulsar.Compiler.Core;
using Pulsar.Compiler.Generation;
using Pulsar.Compiler.Config;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Pulsar.Compiler.Core
{
    public class AOTRuleCompiler : IRuleCompiler
    {
        private readonly ILogger<AOTRuleCompiler> _logger;

        public AOTRuleCompiler(ILogger<AOTRuleCompiler>? logger = null)
        {
            _logger = logger ?? NullLogger<AOTRuleCompiler>.Instance;
        }

        public CompilationResult Compile(RuleDefinition[] rules, CompilerOptions options)
        {
            try
            {
                _logger.LogInformation("Starting AOT compilation of {Count} rules", rules.Length);

                // Log detailed rule information
                foreach (var rule in rules)
                {
                    _logger.LogDebug(
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
                        _logger.LogDebug("Rule {Name} validated successfully", rule.Name);
                    }
                    catch (ArgumentException ex)
                    {
                        _logger.LogError(ex, "Rule validation failed");
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

                _logger.LogInformation("Rule dependencies analyzed and sorted");

                using var generator = new CodeGenerator();
                var generatedFiles = new List<GeneratedFileInfo>();

                // Generate rule groups
                var layerMap = analyzer.GetDependencyMap(sortedRules);
                var ruleGroups = SplitRulesIntoGroups(sortedRules, layerMap);
                _logger.LogDebug("Generated {Count} rule groups", ruleGroups.Count);

                foreach (var group in ruleGroups)
                {
                    _logger.LogDebug("Generating code for rule group {GroupId}", group.Key);
                    var groupImplementation = CodeGenerator.GenerateGroupImplementation(group.Key, group.Value, layerMap);
                    generatedFiles.Add(groupImplementation);
                    _logger.LogDebug("Generated rule group {GroupId}", group.Key);
                }

                // Generate rule coordinator
                var coordinator = CodeGenerator.GenerateRuleCoordinator(ruleGroups, layerMap);
                generatedFiles.Add(coordinator);
                _logger.LogDebug("Generated rule coordinator");

                // Generate metadata file
                var metadata = CodeGenerator.GenerateMetadataFile(sortedRules, layerMap);
                generatedFiles.Add(metadata);
                _logger.LogDebug("Generated metadata file");

                // Generate embedded config
                var config = CodeGenerator.GenerateEmbeddedConfig(options.BuildConfig);
                generatedFiles.Add(config);
                _logger.LogDebug("Generated embedded config");

                // Copy runtime templates
                var templatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "Templates", "Runtime");
                var templateFiles = Directory.GetFiles(templatePath, "*.cs", SearchOption.AllDirectories);
                foreach (var templateFile in templateFiles)
                {
                    var relativePath = Path.GetRelativePath(templatePath, templateFile);
                    var content = File.ReadAllText(templateFile);
                    generatedFiles.Add(new GeneratedFileInfo
                    {
                        FileName = relativePath,
                        Content = content,
                        Namespace = "Pulsar.Runtime",
                        LayerRange = null
                    });
                    _logger.LogDebug("Copied template file {FileName}", relativePath);
                }

                // Copy interface templates
                var interfacePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "Templates", "Interfaces");
                var interfaceFiles = Directory.GetFiles(interfacePath, "*.cs", SearchOption.AllDirectories);
                foreach (var interfaceFile in interfaceFiles)
                {
                    var relativePath = Path.GetRelativePath(interfacePath, interfaceFile);
                    var content = File.ReadAllText(interfaceFile);
                    generatedFiles.Add(new GeneratedFileInfo
                    {
                        FileName = Path.Combine("Interfaces", relativePath),
                        Content = content,
                        Namespace = "Pulsar.Runtime.Interfaces",
                        LayerRange = null
                    });
                    _logger.LogDebug("Copied interface file {FileName}", relativePath);
                }

                // Use the output path as is, assuming it's already properly configured
                var outputPath = options.BuildConfig.OutputPath;

                // Ensure the output path is external to the Pulsar.Compiler directory
                var compilerBasePath = Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                var configuredOutputPath = Path.GetFullPath(outputPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (configuredOutputPath.StartsWith(compilerBasePath, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogError("Configured output path {OutputPath} is inside the Pulsar.Compiler directory. Please specify an external directory for generated projects.", configuredOutputPath);
                    return new CompilationResult
                    {
                        Success = false,
                        Errors = new List<string> { $"Configured output path {configuredOutputPath} must be external to the Pulsar.Compiler directory." },
                        GeneratedFiles = Array.Empty<GeneratedFileInfo>()
                    };
                }

                if (!Directory.Exists(outputPath))
                {
                    Directory.CreateDirectory(outputPath);
                }

                // Write all files
                foreach (var file in generatedFiles)
                {
                    var targetPath = Path.Combine(outputPath, file.FileName);
                    var targetDir = Path.GetDirectoryName(targetPath);
                    if (targetDir != null && !Directory.Exists(targetDir))
                    {
                        Directory.CreateDirectory(targetDir);
                    }
                    File.WriteAllText(targetPath, file.Content);
                    _logger.LogDebug("Written file to {Path}", targetPath);
                }

                // Copy template files from source directory
                var templatePath2 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "Templates");
                _logger.LogInformation("Template path: {Path}", templatePath2);

                if (!Directory.Exists(templatePath2))
                {
                    _logger.LogError("Template directory not found at {Path}", templatePath2);
                    return new CompilationResult
                    {
                        Success = false,
                        Errors = new List<string> { $"Template directory not found at {templatePath2}" },
                        GeneratedFiles = Array.Empty<GeneratedFileInfo>()
                    };
                }

                // Create output directories
                var runtimeDir = Path.Combine(outputPath, "Runtime");
                var interfacesDir = Path.Combine(outputPath, "Interfaces");
                var servicesDir = Path.Combine(runtimeDir, "Services");
                var buffersDir = Path.Combine(runtimeDir, "Buffers");
                Directory.CreateDirectory(runtimeDir);
                Directory.CreateDirectory(interfacesDir);
                Directory.CreateDirectory(servicesDir);
                Directory.CreateDirectory(buffersDir);

                // Copy solution template
                var solutionTemplate = Path.Combine(templatePath2, "Project", "Generated.sln");
                if (!File.Exists(solutionTemplate))
                {
                    _logger.LogError("Solution template not found at {Path}", solutionTemplate);
                    return new CompilationResult
                    {
                        Success = false,
                        Errors = new List<string> { $"Solution template not found at {solutionTemplate}" },
                        GeneratedFiles = Array.Empty<GeneratedFileInfo>()
                    };
                }
                File.Copy(solutionTemplate, Path.Combine(outputPath, "Generated.sln"), true);

                // Copy project template and replace placeholders
                var projectTemplate = Path.Combine(templatePath2, "Project", "Runtime.csproj");
                if (!File.Exists(projectTemplate))
                {
                    _logger.LogError("Project template not found at {Path}", projectTemplate);
                    return new CompilationResult
                    {
                        Success = false,
                        Errors = new List<string> { $"Project template not found at {projectTemplate}" },
                        GeneratedFiles = Array.Empty<GeneratedFileInfo>()
                    };
                }

                var projectContent = File.ReadAllText(projectTemplate)
                    .Replace("{{TargetFramework}}", "net8.0")
                    .Replace("{{Target}}", "win-x64");
                File.WriteAllText(Path.Combine(outputPath, "Generated.csproj"), projectContent);

                // Copy trimming config
                var trimmingTemplate = Path.Combine(templatePath2, "Project", "trimming.xml");
                if (File.Exists(trimmingTemplate))
                {
                    File.Copy(trimmingTemplate, Path.Combine(outputPath, "trimming.xml"), true);
                }

                // Copy runtime files
                var runtimeFiles = new Dictionary<string, string>
                {
                    { "Program.cs", outputPath },
                    { "RuntimeConfig.cs", outputPath },
                    { "ConfigurationLoader.cs", outputPath },
                    { "Runtime/RuntimeOrchestrator.cs", runtimeDir },
                    { "Runtime/TemplateRuleCoordinator.cs", runtimeDir },
                    { "Runtime/Services/RedisConfiguration.cs", servicesDir },
                    { "Runtime/Services/RedisService.cs", servicesDir },
                    { "Runtime/Services/RedisLoggingConfiguration.cs", servicesDir },
                    { "Runtime/Services/RedisMonitoring.cs", servicesDir },
                    { "Runtime/Buffers/CircularBuffer.cs", buffersDir },
                    { "Runtime/Buffers/IDateTimeProvider.cs", buffersDir },
                    { "Runtime/Buffers/SystemDateTimeProvider.cs", buffersDir },
                    { "Interfaces/ICompiledRules.cs", interfacesDir },
                    { "Interfaces/IRuleCoordinator.cs", interfacesDir },
                    { "Interfaces/IRuleGroup.cs", interfacesDir }
                };

                foreach (var file in runtimeFiles)
                {
                    var sourcePath = Path.Combine(templatePath2, file.Key);
                    var targetPath = Path.Combine(file.Value, Path.GetFileName(file.Key));

                    if (!File.Exists(sourcePath))
                    {
                        _logger.LogWarning("Template file not found: {Path}", sourcePath);
                        continue;
                    }

                    File.Copy(sourcePath, targetPath, true);
                    _logger.LogDebug("Generated file written to {Path}", targetPath);
                }

                _logger.LogInformation("Successfully generated solution and project files");

                _logger.LogInformation("Successfully generated source files in {Path}", outputPath);

                return new CompilationResult
                {
                    Success = true,
                    GeneratedFiles = generatedFiles.ToArray(),
                    OutputPath = outputPath
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AOT compilation failed");
                return new CompilationResult
                {
                    Success = false,
                    Errors = new List<string> { ex.Message },
                    GeneratedFiles = Array.Empty<GeneratedFileInfo>()
                };
            }
        }

        private Dictionary<int, List<RuleDefinition>> SplitRulesIntoGroups(List<RuleDefinition> rules, Dictionary<string, string> layerMap)
        {
            var ruleGroups = new Dictionary<int, List<RuleDefinition>>();
            var config = new Pulsar.Compiler.Config.RuleGroupingConfig();
            var currentGroup = 0;
            var currentGroupRules = new List<RuleDefinition>();

            foreach (var rule in rules)
            {
                if (currentGroupRules.Count >= config.MaxRulesPerGroup ||
                    (currentGroupRules.Any() && 
                     (GetTotalConditions(currentGroupRules) + GetConditionCount(rule) > config.MaxConditionsPerGroup ||
                      GetTotalActions(currentGroupRules) + GetActionCount(rule) > config.MaxActionsPerGroup)))
                {
                    ruleGroups[currentGroup] = currentGroupRules;
                    currentGroup++;
                    currentGroupRules = new List<RuleDefinition>();
                }

                currentGroupRules.Add(rule);
            }

            if (currentGroupRules.Count > 0)
            {
                ruleGroups[currentGroup] = currentGroupRules;
            }

            return ruleGroups;
        }

        private static int GetConditionCount(RuleDefinition rule)
        {
            if (rule.Conditions == null)
            {
                return 0;
            }

            return (rule.Conditions.All?.Count ?? 0) + (rule.Conditions.Any?.Count ?? 0);
        }

        private static int GetTotalConditions(List<RuleDefinition> rules)
        {
            return rules.Sum(GetConditionCount);
        }

        private static int GetActionCount(RuleDefinition rule)
        {
            return rule.Actions?.Count ?? 0;
        }

        private static int GetTotalActions(List<RuleDefinition> rules)
        {
            return rules.Sum(GetActionCount);
        }
    }
}
