// File: Pulsar.Compiler/Core/AOTRuleCompiler.cs

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Pulsar.Compiler.Config;
using Pulsar.Compiler.Generation;
using Pulsar.Compiler.Generation.Generators; // Using RuleGroupGeneratorFixed now
using Pulsar.Compiler.Models;

namespace Pulsar.Compiler.Core
{
    public class AOTRuleCompiler : IRuleCompiler
    {
        private readonly ILogger<AOTRuleCompiler> _logger;
        private readonly CodeGenerator _codeGenerator;
        private readonly RuleGroupGenerator _ruleGroupGenerator;
        private readonly RuleCoordinatorGenerator _ruleCoordinatorGenerator;
        private readonly MetadataGenerator _metadataGenerator;

        public AOTRuleCompiler(ILogger<AOTRuleCompiler>? logger = null)
        {
            _logger = logger ?? NullLogger<AOTRuleCompiler>.Instance;
            _codeGenerator = new CodeGenerator();
            _ruleGroupGenerator = new RuleGroupGenerator(_logger);
            _ruleCoordinatorGenerator = new RuleCoordinatorGenerator();
            _metadataGenerator = new MetadataGenerator();
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
                var validationResult = RuleValidator.ValidateRules(rules);
                if (!validationResult.IsValid)
                {
                    _logger.LogError("Rule validation failed");
                    return new CompilationResult
                    {
                        Success = false,
                        Errors = validationResult.Errors.ToList(),
                        GeneratedFiles = Array.Empty<GeneratedFileInfo>(),
                    };
                }

                var analyzer = new DependencyAnalyzer();
                var sortedRules = analyzer.AnalyzeDependencies(rules.ToList());

                _logger.LogInformation("Rule dependencies analyzed and sorted");

                var generatedFiles = new List<GeneratedFileInfo>();

                // Generate rule groups
                var layerMap = _codeGenerator.AssignLayers(sortedRules);
                var stringLayerMap = layerMap.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.ToString()
                );
                var ruleGroups = _codeGenerator.SplitRulesIntoGroups(sortedRules, stringLayerMap);
                _logger.LogDebug("Generated {Count} rule groups", ruleGroups.Count);

                // Generate rule group files
                for (int i = 0; i < ruleGroups.Count; i++)
                {
                    _logger.LogDebug("Generating code for rule group {GroupId}", i);
                    var groupImplementation = _ruleGroupGenerator.GenerateGroupImplementation(
                        i,
                        ruleGroups[i],
                        stringLayerMap,
                        options.BuildConfig
                    );
                    generatedFiles.Add(groupImplementation);
                    _logger.LogDebug("Generated rule group {GroupId}", i);
                }

                // Generate rule coordinator
                var coordinator = _ruleCoordinatorGenerator.GenerateRuleCoordinator(
                    ruleGroups,
                    stringLayerMap,
                    options.BuildConfig
                );
                generatedFiles.Add(coordinator);
                _logger.LogDebug("Generated rule coordinator");

                // Generate metadata file
                var metadata = _metadataGenerator.GenerateMetadataFile(
                    sortedRules,
                    stringLayerMap,
                    options.BuildConfig
                );
                generatedFiles.Add(metadata);
                _logger.LogDebug("Generated metadata file");

                // Generate embedded config
                var config = _codeGenerator.GenerateEmbeddedConfig(options.BuildConfig);
                generatedFiles.Add(config);
                _logger.LogDebug("Generated embedded config");

                // Post-generation fixups no longer needed

                // Write files to disk
                foreach (var file in generatedFiles)
                {
                    var filePath = Path.Combine(options.BuildConfig.OutputPath, file.FileName);
                    var directory = Path.GetDirectoryName(filePath);
                    if (!string.IsNullOrEmpty(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }
                    File.WriteAllText(filePath, file.Content);
                }

                _logger.LogInformation("AOT compilation completed successfully");

                return new CompilationResult
                {
                    Success = true,
                    GeneratedFiles = generatedFiles.ToArray(),
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AOT compilation failed");
                return new CompilationResult
                {
                    Success = false,
                    Errors = new List<string> { ex.Message },
                    GeneratedFiles = Array.Empty<GeneratedFileInfo>(),
                };
            }
        }

        private async Task<CompilationResult> CompileRulesAsync(
            List<RuleDefinition> rules,
            BuildConfig buildConfig
        )
        {
            var result = new CompilationResult();

            try
            {
                // Validate rules
                var validationResult = RuleValidator.ValidateRules(rules);
                if (!validationResult.IsValid)
                {
                    result.Success = false;
                    result.Errors.AddRange(validationResult.Errors);
                    return result;
                }

                // Assign layers to rules
                var layerMap = _codeGenerator.AssignLayers(rules);
                var stringLayerMap = layerMap.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.ToString()
                );

                // Split rules into groups
                var ruleGroups = await SplitRulesIntoGroupsAsync(rules, stringLayerMap);

                // Generate code
                var generatedFiles = _codeGenerator.GenerateCSharp(rules, buildConfig);

                // Write files to disk
                foreach (var file in generatedFiles)
                {
                    var filePath = Path.Combine(buildConfig.OutputPath, file.FileName);
                    var directory = Path.GetDirectoryName(filePath);
                    if (!string.IsNullOrEmpty(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }
                    await File.WriteAllTextAsync(filePath, file.Content);
                }

                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Errors.Add(ex.Message);
            }

            return result;
        }

        private async Task<Dictionary<int, List<RuleDefinition>>> SplitRulesIntoGroupsAsync(
            List<RuleDefinition> rules,
            Dictionary<string, string> layerMap
        )
        {
            var ruleGroups = new Dictionary<int, List<RuleDefinition>>();
            var config = new Pulsar.Compiler.Config.RuleGroupingConfig();
            var currentGroup = 0;
            var currentGroupRules = new List<RuleDefinition>();

            foreach (var rule in rules)
            {
                if (
                    currentGroupRules.Count >= config.MaxRulesPerGroup
                    || (
                        currentGroupRules.Any()
                        && (
                            GetTotalConditions(currentGroupRules) + GetConditionCount(rule)
                                > config.MaxConditionsPerGroup
                            || GetTotalActions(currentGroupRules) + GetActionCount(rule)
                                > config.MaxActionsPerGroup
                        )
                    )
                )
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
