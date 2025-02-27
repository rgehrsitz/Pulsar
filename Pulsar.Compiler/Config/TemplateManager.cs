using System;
using System.IO;
using System.Text;

namespace Pulsar.Compiler.Config
{
    public class TemplateManager
    {
        public void GenerateSolutionFile(string path)
        {
            var content =
                @"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio Version 17
VisualStudioVersion = 17.0.31903.59
MinimumVisualStudioVersion = 10.0.40219.1
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""Generated"", ""Generated.csproj"", ""{"
                + Guid.NewGuid().ToString().ToUpper()
                + @"}""
EndProject
Global
    GlobalSection(SolutionConfigurationPlatforms) = preSolution
        Debug|Any CPU = Debug|Any CPU
        Release|Any CPU = Release|Any CPU
    EndGlobalSection
    GlobalSection(ProjectConfigurationPlatforms) = postSolution
        {"
                + Guid.NewGuid().ToString().ToUpper()
                + @"}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
        {"
                + Guid.NewGuid().ToString().ToUpper()
                + @"}.Debug|Any CPU.Build.0 = Debug|Any CPU
        {"
                + Guid.NewGuid().ToString().ToUpper()
                + @"}.Release|Any CPU.ActiveCfg = Release|Any CPU
        {"
                + Guid.NewGuid().ToString().ToUpper()
                + @"}.Release|Any CPU.Build.0 = Release|Any CPU
    EndGlobalSection
EndGlobal";

            File.WriteAllText(path, content.TrimStart());
        }

        public void GenerateProjectFile(string path, BuildConfig buildConfig)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<Project Sdk=\"Microsoft.NET.Sdk\">");
            sb.AppendLine("  <PropertyGroup>");
            sb.AppendLine($"    <TargetFramework>{buildConfig.TargetFramework}</TargetFramework>");
            sb.AppendLine("    <ImplicitUsings>enable</ImplicitUsings>");
            sb.AppendLine("    <Nullable>enable</Nullable>");
            sb.AppendLine("    <OutputType>Exe</OutputType>");
            if (buildConfig.StandaloneExecutable)
            {
                sb.AppendLine("    <PublishSingleFile>true</PublishSingleFile>");
                sb.AppendLine("    <SelfContained>true</SelfContained>");
                sb.AppendLine($"    <RuntimeIdentifier>{buildConfig.Target}</RuntimeIdentifier>");
            }
            if (buildConfig.OptimizeOutput)
            {
                sb.AppendLine("    <PublishReadyToRun>true</PublishReadyToRun>");
                sb.AppendLine("    <PublishTrimmed>true</PublishTrimmed>");
                sb.AppendLine("    <TrimMode>link</TrimMode>");
            }
            sb.AppendLine("  </PropertyGroup>");
            sb.AppendLine();
            sb.AppendLine("  <ItemGroup>");
            sb.AppendLine(
                "    <PackageReference Include=\"Microsoft.Extensions.Logging\" Version=\"8.0.0\" />"
            );
            sb.AppendLine("    <PackageReference Include=\"NRedisStack\" Version=\"0.13.1\" />");
            sb.AppendLine("    <PackageReference Include=\"Polly\" Version=\"8.3.0\" />");
            sb.AppendLine("    <PackageReference Include=\"prometheus-net\" Version=\"8.2.1\" />");
            sb.AppendLine("    <PackageReference Include=\"Serilog\" Version=\"4.2.0\" />");
            sb.AppendLine("    <PackageReference Include=\"Serilog.Enrichers.Thread\" Version=\"3.1.0\" />");
            sb.AppendLine("    <PackageReference Include=\"Serilog.Formatting.Compact\" Version=\"2.0.0\" />");
            sb.AppendLine("    <PackageReference Include=\"Serilog.Sinks.Console\" Version=\"5.0.1\" />");
            sb.AppendLine("    <PackageReference Include=\"Serilog.Sinks.File\" Version=\"5.0.0\" />");
            sb.AppendLine(
                "    <PackageReference Include=\"StackExchange.Redis\" Version=\"2.8.16\" />"
            );
            sb.AppendLine("    <PackageReference Include=\"YamlDotNet\" Version=\"16.3.0\" />");
            sb.AppendLine("  </ItemGroup>");
            sb.AppendLine("</Project>");

            File.WriteAllText(path, sb.ToString());
        }

        public void GenerateProjectFiles(string outputPath, BuildConfig buildConfig)
        {
            // Create solution file
            GenerateSolutionFile(Path.Combine(outputPath, "Generated.sln"));

            // Create project file
            GenerateProjectFile(Path.Combine(outputPath, "Generated.csproj"), buildConfig);

            // Copy Program.cs template
            CopyTemplateFile("Program.cs", outputPath);

            // Copy RuntimeOrchestrator.cs template
            CopyTemplateFile(
                "Runtime/RuntimeOrchestrator.cs",
                outputPath,
                "RuntimeOrchestrator.cs"
            );

            // Copy other necessary template files
            CopyTemplateFile("RuntimeConfig.cs", outputPath);

            // Create directories for additional files
            Directory.CreateDirectory(Path.Combine(outputPath, "Services"));
            Directory.CreateDirectory(Path.Combine(outputPath, "Buffers"));
            Directory.CreateDirectory(Path.Combine(outputPath, "Interfaces"));

            // Copy interface templates
            CopyTemplateFile(
                "Interfaces/IRuleCoordinator.cs",
                outputPath,
                "Interfaces/IRuleCoordinator.cs"
            );
            CopyTemplateFile(
                "Interfaces/IRuleGroup.cs",
                outputPath,
                "Interfaces/IRuleGroup.cs"
            );
            CopyTemplateFile(
                "Interfaces/ICompiledRules.cs",
                outputPath,
                "Interfaces/ICompiledRules.cs"
            );

            // Copy service templates
            CopyTemplateFile(
                "Runtime/Services/RedisConfiguration.cs",
                outputPath,
                "Services/RedisConfiguration.cs"
            );
            CopyTemplateFile(
                "Runtime/Services/RedisService.cs",
                outputPath,
                "Services/RedisService.cs"
            );
            CopyTemplateFile(
                "Runtime/Services/RedisMonitoring.cs",
                outputPath,
                "Services/RedisMonitoring.cs"
            );
            CopyTemplateFile(
                "Runtime/Services/RedisLoggingConfiguration.cs", 
                outputPath,
                "Services/RedisLoggingConfiguration.cs"
            );
            
            // Copy buffer templates
            CopyTemplateFile(
                "Runtime/Buffers/CircularBuffer.cs",
                outputPath,
                "Buffers/CircularBuffer.cs"
            );
            CopyTemplateFile(
                "Runtime/Buffers/IDateTimeProvider.cs",
                outputPath,
                "Buffers/IDateTimeProvider.cs"
            );
            CopyTemplateFile(
                "Runtime/Buffers/SystemDateTimeProvider.cs",
                outputPath,
                "Buffers/SystemDateTimeProvider.cs"
            );
        }

        private void CopyTemplateFile(
            string templateRelativePath,
            string outputPath,
            string? outputRelativePath = null
        )
        {
            try
            {
                var templatePath = GetTemplatePath(templateRelativePath);
                var destinationPath = Path.Combine(
                    outputPath,
                    outputRelativePath ?? Path.GetFileName(templateRelativePath)
                );

                // Create directory if it doesn't exist
                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? outputPath);

                // Copy the template file
                var templateContent = File.ReadAllText(templatePath);
                File.WriteAllText(destinationPath, templateContent);
            }
            catch (FileNotFoundException ex)
            {
                // Log the error but continue - some templates might be optional
                Console.Error.WriteLine(
                    $"Warning: Could not find template file: {templateRelativePath}. {ex.Message}"
                );
            }
        }

        private string GetTemplatePath(string templateFileName)
        {
            // Try multiple possible locations for the template files
            var possiblePaths = new[]
            {
                // Direct path from working directory
                Path.Combine("Pulsar.Compiler", "Config", "Templates", templateFileName),
                // Path relative to assembly location
                Path.Combine(
                    Path.GetDirectoryName(typeof(TemplateManager).Assembly.Location) ?? "",
                    "Pulsar.Compiler",
                    "Config",
                    "Templates",
                    templateFileName
                ),
                // Path relative to project root (go up from bin directory)
                Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "..",
                    "..",
                    "..",
                    "Pulsar.Compiler",
                    "Config",
                    "Templates",
                    templateFileName
                ),
                // Absolute path based on solution directory structure
                Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "..",
                    "..",
                    "..",
                    "..",
                    "Pulsar.Compiler",
                    "Config",
                    "Templates",
                    templateFileName
                ),
            };

            foreach (var path in possiblePaths)
            {
                var normalizedPath = Path.GetFullPath(path);
                if (File.Exists(normalizedPath))
                {
                    return normalizedPath;
                }
            }

            throw new FileNotFoundException(
                $"Template file not found: {templateFileName}. Searched in: {string.Join(", ", possiblePaths)}"
            );
        }
    }
}