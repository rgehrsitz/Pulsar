using System.Text;
using Microsoft.Extensions.Logging;

namespace Pulsar.Tests.TestUtilities
{
    /// <summary>
    /// Tool for repairing or recreating broken solution files
    /// </summary>
    public static class SolutionRepairTool
    {
        private const string ProjectTypeGuid = "{9A19103F-16F7-4668-BE54-9A1E7A4F7556}"; // Default for C# projects
        private const string SolutionGuid = "{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"; // Default for VS solutions

        public static bool RegenerateSolution(string solutionDir, ILogger logger)
        {
            try
            {
                if (!Directory.Exists(solutionDir))
                {
                    logger.LogError("Solution directory not found: {Path}", solutionDir);
                    return false;
                }

                // Also check for a csproj file directly in the solution directory
                // Sometimes the solution structure is flat with just a single project
                var directProjectFile = Directory
                    .GetFiles(solutionDir, "*.csproj")
                    .FirstOrDefault();
                if (directProjectFile != null)
                {
                    logger.LogInformation(
                        "Found project file directly in solution directory: {Path}",
                        directProjectFile
                    );
                }

                string solutionPath = Path.Combine(
                    solutionDir,
                    Path.GetFileName(solutionDir) + ".sln"
                );
                string solutionName = Path.GetFileNameWithoutExtension(solutionPath);

                // Find all .csproj files in the solution directory
                var projectFiles = Directory.GetFiles(
                    solutionDir,
                    "*.csproj",
                    SearchOption.AllDirectories
                );

                // If no projects found, create at least an empty project
                if (projectFiles.Length == 0 && directProjectFile == null)
                {
                    logger.LogWarning(
                        "No project files found. Creating a minimal project structure."
                    );

                    // Create a minimal project file for Beacon.Runtime
                    var minimalProjectDir = Path.Combine(solutionDir, "Beacon.Runtime");
                    if (!Directory.Exists(minimalProjectDir))
                    {
                        Directory.CreateDirectory(minimalProjectDir);
                    }

                    var minimalProjectPath = Path.Combine(
                        minimalProjectDir,
                        "Beacon.Runtime.csproj"
                    );
                    var minimalProjectContent =
                        @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
  </PropertyGroup>
</Project>";

                    File.WriteAllText(minimalProjectPath, minimalProjectContent);
                    logger.LogInformation(
                        "Created minimal project file at: {Path}",
                        minimalProjectPath
                    );

                    // Create a minimal Program.cs
                    var minimalProgramPath = Path.Combine(minimalProjectDir, "Program.cs");
                    var minimalProgramContent =
                        @"using System;

namespace Beacon.Runtime
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine(""Beacon runtime started"");
            Console.WriteLine(""Using Redis connection: "" + Environment.GetEnvironmentVariable(""REDIS_CONNECTION"") ?? ""default"");
            
            // Keep the program running
            while (true)
            {
                System.Threading.Thread.Sleep(1000);
            }
        }
    }
}";
                    File.WriteAllText(minimalProgramPath, minimalProgramContent);
                    logger.LogInformation(
                        "Created minimal Program.cs at: {Path}",
                        minimalProgramPath
                    );

                    // Add the new project to the list
                    projectFiles = new[] { minimalProjectPath };
                }

                if (projectFiles.Length == 0)
                {
                    logger.LogError("No project files found in solution directory");
                    return false;
                }

                logger.LogInformation("Found {Count} project files", projectFiles.Length);

                var projectInfos = new List<(string Name, string Path, Guid ProjectGuid)>();

                // Generate info for each project
                foreach (var projectPath in projectFiles)
                {
                    string projectName = Path.GetFileNameWithoutExtension(projectPath);
                    string relativePath = Path.GetRelativePath(solutionDir, projectPath);
                    Guid projectGuid = Guid.NewGuid();

                    projectInfos.Add((projectName, relativePath, projectGuid));
                    logger.LogInformation(
                        "Project: {Name}, Path: {Path}",
                        projectName,
                        relativePath
                    );
                }

                // Generate new solution file
                var sb = new StringBuilder();

                // Header
                sb.AppendLine("Microsoft Visual Studio Solution File, Format Version 12.00");
                sb.AppendLine("# Visual Studio Version 17");
                sb.AppendLine("VisualStudioVersion = 17.0.31903.59");
                sb.AppendLine("MinimumVisualStudioVersion = 10.0.40219.1");
                sb.AppendLine();

                // Projects
                foreach (var (name, path, projectGuid) in projectInfos)
                {
                    sb.AppendLine(
                        $"Project(\"{ProjectTypeGuid}\") = \"{name}\", \"{path}\", \"{{{projectGuid}}}\""
                    );
                    sb.AppendLine("EndProject");
                }

                sb.AppendLine();

                // Global sections
                sb.AppendLine("Global");

                // Solution configurations
                sb.AppendLine("\tGlobalSection(SolutionConfigurationPlatforms) = preSolution");
                sb.AppendLine("\t\tDebug|Any CPU = Debug|Any CPU");
                sb.AppendLine("\t\tRelease|Any CPU = Release|Any CPU");
                sb.AppendLine("\tEndGlobalSection");

                // Project configurations
                sb.AppendLine("\tGlobalSection(ProjectConfigurationPlatforms) = postSolution");
                foreach (var (_, _, projectGuid) in projectInfos)
                {
                    sb.AppendLine($"\t\t{{{projectGuid}}}.Debug|Any CPU.ActiveCfg = Debug|Any CPU");
                    sb.AppendLine($"\t\t{{{projectGuid}}}.Debug|Any CPU.Build.0 = Debug|Any CPU");
                    sb.AppendLine(
                        $"\t\t{{{projectGuid}}}.Release|Any CPU.ActiveCfg = Release|Any CPU"
                    );
                    sb.AppendLine(
                        $"\t\t{{{projectGuid}}}.Release|Any CPU.Build.0 = Release|Any CPU"
                    );
                }
                sb.AppendLine("\tEndGlobalSection");

                // Solution properties
                sb.AppendLine("\tGlobalSection(SolutionProperties) = preSolution");
                sb.AppendLine("\t\tHideSolutionNode = FALSE");
                sb.AppendLine("\tEndGlobalSection");

                sb.AppendLine("EndGlobal");

                // Backup existing solution if it exists
                if (File.Exists(solutionPath))
                {
                    string backupPath =
                        solutionPath + ".bak." + DateTime.Now.ToString("yyyyMMddHHmmss");
                    File.Copy(solutionPath, backupPath);
                    logger.LogInformation("Backed up existing solution file to {Path}", backupPath);
                }

                // Write new solution file
                File.WriteAllText(solutionPath, sb.ToString());
                logger.LogInformation("Generated new solution file at {Path}", solutionPath);

                return true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error regenerating solution file");
                return false;
            }
        }
    }
}
