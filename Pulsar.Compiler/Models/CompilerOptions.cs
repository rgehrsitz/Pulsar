using Pulsar.Compiler.Config;

namespace Pulsar.Compiler.Models
{
    public class CompilerOptions
    {
        /// <summary>
        /// Build configuration to be used during compilation.
        /// </summary>
        public BuildConfig BuildConfig { get; set; } = new BuildConfig
        {
            OutputPath = "Generated",
            Target = "win-x64",
            ProjectName = "Pulsar.Compiler",
            TargetFramework = "net9.0"
        };

        /// <summary>
        /// Optional list of valid sensors for rule validation.
        /// </summary>
        public string[] ValidSensors { get; set; } = new string[0];
    }
}
