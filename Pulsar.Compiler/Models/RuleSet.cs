using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Pulsar.Compiler.Models
{
    /// <summary>
    /// Represents a set of rule definitions loaded from a YAML file
    /// </summary>
    public class RuleSet
    {
        /// <summary>
        /// The collection of rules in this rule set
        /// </summary>
        public List<RuleDefinition> Rules { get; set; } = new List<RuleDefinition>();

        /// <summary>
        /// Loads a RuleSet from a YAML file
        /// </summary>
        /// <param name="path">Path to the YAML file</param>
        /// <returns>The loaded RuleSet instance</returns>
        public static RuleSet Load(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException($"Rule file not found: {path}");

            string yaml = File.ReadAllText(path);
            Console.WriteLine($"Loading rule file content:\n{yaml}");

            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            try
            {
                var ruleset = deserializer.Deserialize<RuleSetContainer>(yaml);
                return new RuleSet { Rules = ruleset?.Rules ?? new List<RuleDefinition>() };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing YAML: {ex}");
                throw new FormatException($"Failed to parse rule file: {ex.Message}", ex);
            }
        }
    }

    /// <summary>
    /// Internal container for deserializing YAML with a "rules" property
    /// </summary>
    internal class RuleSetContainer
    {
        public List<RuleDefinition> Rules { get; set; } = new List<RuleDefinition>();
    }
}
