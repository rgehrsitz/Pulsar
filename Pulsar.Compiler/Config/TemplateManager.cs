using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace Pulsar.Compiler.Config
{
    public class TemplateManager
    {
        private readonly ILogger<TemplateManager> _logger;
        private const string TemplateDirectory = "Build/Templates/ProjectTemplate";
        private readonly string[] _templateExtensions = { ".cs", ".xml", ".csproj", ".json" };

        public TemplateManager(ILogger<TemplateManager> logger)
        {
            _logger = logger;
        }

        public string FindTemplatePath()
        {
            // Try multiple possible locations for the template directory
            string[] possibleTemplatePaths = new[]
            {
                // Direct path from executable
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, TemplateDirectory),

                // One level up (bin/Debug/net9.0)
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", TemplateDirectory),

                // Two levels up (bin/Debug)
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", TemplateDirectory),

                // Three levels up (bin)
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", TemplateDirectory),

                // Four levels up (project root)
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", TemplateDirectory),

                // Relative to current directory
                Path.Combine(Directory.GetCurrentDirectory(), TemplateDirectory)
            };

            foreach (var path in possibleTemplatePaths)
            {
                var normalizedPath = Path.GetFullPath(path);
                _logger.LogDebug("Checking template path: {Path}", normalizedPath);
                if (Directory.Exists(normalizedPath))
                {
                    _logger.LogInformation("Found template directory at: {Path}", normalizedPath);
                    return normalizedPath;
                }
            }

            var errorMessage = $"Template directory not found in any of the expected locations. Searched paths:\n{string.Join("\n", possibleTemplatePaths.Select(p => Path.GetFullPath(p)))}";
            _logger.LogError(errorMessage);
            throw new DirectoryNotFoundException(errorMessage);
        }

        public void CopyTemplates(string outputPath, bool overwrite = true)
        {
            var templatePath = FindTemplatePath();
            var templateFiles = Directory.GetFiles(templatePath)
                .Where(f => _templateExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase));

            foreach (string file in templateFiles)
            {
                string fileName = Path.GetFileName(file);
                string destFile = Path.Combine(outputPath, fileName);

                try
                {
                    File.Copy(file, destFile, overwrite);
                    _logger.LogDebug("Copied template file: {FileName}", fileName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to copy template file {FileName}", fileName);
                    throw;
                }
            }
        }

        public void CleanOutputDirectory(string outputPath)
        {
            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
                _logger.LogDebug("Created output directory: {Path}", outputPath);
                return;
            }

            try
            {
                var filesToDelete = Directory.GetFiles(outputPath)
                    .Where(f => _templateExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase));

                foreach (var file in filesToDelete)
                {
                    File.Delete(file);
                    _logger.LogDebug("Deleted file: {File}", file);
                }
            }
            catch (Exception ex)
            {
                var errorMessage = $"Failed to clean output directory: {ex.Message}";
                _logger.LogError(ex, errorMessage);
                throw new InvalidOperationException(errorMessage, ex);
            }
        }

        public bool ValidateTemplates(string templatePath)
        {
            var requiredTemplates = new[]
            {
                "Program.cs",
                "RuntimeConfig.cs",
                "trimming.xml"
            };

            foreach (var template in requiredTemplates)
            {
                var templateFile = Path.Combine(templatePath, template);
                if (!File.Exists(templateFile))
                {
                    _logger.LogError("Required template file not found: {Template}", template);
                    return false;
                }
            }

            return true;
        }
    }
}
