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
            var programTemplate = File.ReadAllText("Pulsar.Compiler/Config/Templates/Program.cs");
            File.WriteAllText(Path.Combine(outputPath, "Program.cs"), programTemplate);
        }
    }
}
