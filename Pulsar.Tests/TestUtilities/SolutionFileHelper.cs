using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Pulsar.Tests.TestUtilities
{
    /// <summary>
    /// Helper class for working with solution files
    /// </summary>
    public static class SolutionFileHelper
    {
        private const string DefaultProjectTypeGuid = "{9A19103F-16F7-4668-BE54-9A1E7A4F7556}"; // Default for C# projects
        private const string DefaultSolutionGuid = "{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"; // Default for VS solutions

        /// <summary>
        /// Fixes common issues in Visual Studio solution files
        /// </summary>
        public static bool TryFixSolutionFile(string solutionPath, ILogger logger)
        {
            try
            {
                if (!File.Exists(solutionPath))
                {
                    logger.LogError("Solution file not found: {Path}", solutionPath);
                    return false;
                }

                string content = File.ReadAllText(solutionPath);
                bool modified = false;

                // Make backup of original file
                string backupPath = solutionPath + ".bak";
                File.WriteAllText(backupPath, content);
                logger.LogInformation("Created backup of solution file: {Path}", backupPath);

                // Fix Project Type GUIDs
                var projectLineRegex = new Regex(@"Project\s*\(\s*""(?<typeGuid>[^""]*)""\s*\)");
                var matches = projectLineRegex.Matches(content);

                if (matches.Count > 0)
                {
                    StringBuilder newContent = new StringBuilder(content);

                    foreach (Match match in matches)
                    {
                        string typeGuid = match.Groups["typeGuid"].Value;
                        if (
                            string.IsNullOrWhiteSpace(typeGuid)
                            || !typeGuid.StartsWith("{")
                            || !typeGuid.EndsWith("}")
                        )
                        {
                            // Replace with default C# project type GUID
                            int startIndex = match.Groups["typeGuid"].Index;
                            int length = match.Groups["typeGuid"].Length;

                            newContent.Remove(startIndex, length);
                            newContent.Insert(startIndex, DefaultProjectTypeGuid);
                            modified = true;

                            logger.LogInformation(
                                "Fixed project type GUID: '{Original}' -> '{New}'",
                                typeGuid,
                                DefaultProjectTypeGuid
                            );
                        }
                    }

                    if (modified)
                    {
                        content = newContent.ToString();
                    }
                }

                // Check for other common solution file issues
                // 1. Check for required format version
                if (!content.Contains("Microsoft Visual Studio Solution File"))
                {
                    string header =
                        "Microsoft Visual Studio Solution File, Format Version 12.00\r\n# Visual Studio Version 17\r\n";
                    content = header + content;
                    modified = true;
                    logger.LogInformation("Added missing solution file header");
                }

                // 2. Fix missing EndProject blocks
                var projectStartPattern = new Regex(
                    @"Project\s*\([^\)]+\)\s*=\s*[^,]+,\s*[^,]+,\s*[^)]+\)"
                );
                var endProjectPattern = new Regex(@"EndProject");

                var projectStarts = projectStartPattern.Matches(content);
                var endProjects = endProjectPattern.Matches(content);

                if (projectStarts.Count > endProjects.Count)
                {
                    StringBuilder newContent = new StringBuilder(content);
                    for (int i = 0; i < projectStarts.Count - endProjects.Count; i++)
                    {
                        newContent.AppendLine("EndProject");
                    }
                    content = newContent.ToString();
                    modified = true;
                    logger.LogInformation(
                        "Added {Count} missing EndProject tags",
                        projectStarts.Count - endProjects.Count
                    );
                }

                // 3. Check for required Global section
                if (!content.Contains("GlobalSection"))
                {
                    StringBuilder newContent = new StringBuilder(content);
                    newContent.AppendLine("Global");
                    newContent.AppendLine(
                        "\tGlobalSection(SolutionConfigurationPlatforms) = preSolution"
                    );
                    newContent.AppendLine("\t\tDebug|Any CPU = Debug|Any CPU");
                    newContent.AppendLine("\t\tRelease|Any CPU = Release|Any CPU");
                    newContent.AppendLine("\tEndGlobalSection");
                    newContent.AppendLine("EndGlobal");
                    content = newContent.ToString();
                    modified = true;
                    logger.LogInformation("Added missing Global section");
                }
                else if (!content.Contains("EndGlobal"))
                {
                    content += "\r\nEndGlobal\r\n";
                    modified = true;
                    logger.LogInformation("Added missing EndGlobal tag");
                }

                // Write fixed content if modified
                if (modified)
                {
                    File.WriteAllText(solutionPath, content);
                    logger.LogInformation("Fixed solution file and saved to: {Path}", solutionPath);
                }
                else
                {
                    logger.LogInformation("No issues found in the solution file");
                }

                return modified;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error trying to fix solution file: {Path}", solutionPath);
                return false;
            }
        }
    }
}
