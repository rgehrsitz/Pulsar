// File: Pulsar.Compiler/Config/TemplateManager.cs

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Serilog;
using Pulsar.Compiler.Models;

namespace Pulsar.Compiler.Config
{
    public class TemplateManager
    {
        private readonly Microsoft.Extensions.Logging.ILogger _logger;
        private const string TemplateDirectory = "Config/Templates";
        private readonly string[] _templateExtensions = { ".cs", ".xml", ".csproj", ".json" };

        public TemplateManager(Microsoft.Extensions.Logging.ILogger logger)
        {
            _logger = logger;
        }

        public List<GeneratedFileInfo> CopyTemplateFiles(string outputPath)
        {
            var files = new List<GeneratedFileInfo>();
            var templatePath = FindTemplatePath();

            try
            {
                // Create output directory if it doesn't exist
                Directory.CreateDirectory(outputPath);

                // Get all template files
                var templateFiles = Directory
                    .GetFiles(templatePath, "*.*", SearchOption.AllDirectories)
                    .Where(f =>
                        _templateExtensions.Contains(
                            Path.GetExtension(f),
                            StringComparer.OrdinalIgnoreCase
                        )
                    );

                foreach (var templateFile in templateFiles)
                {
                    var relativePath = Path.GetRelativePath(templatePath, templateFile);
                    var outputFilePath = Path.Combine(outputPath, relativePath);
                    var content = File.ReadAllText(templateFile);

                    // Ensure directory exists for output file
                    var dirPath = Path.GetDirectoryName(outputFilePath);
                    if (dirPath != null)
                    {
                        Directory.CreateDirectory(dirPath);
                    }

                    files.Add(
                        new GeneratedFileInfo
                        {
                            FileName = Path.GetFileName(templateFile),
                            FilePath = outputFilePath,
                            Content = content,
                            Namespace = "Pulsar.Runtime.Rules"
                        }
                    );

                    _logger.LogDebug("Added template file: {FileName}", relativePath);
                }

                return files;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error copying template files");
                throw;
            }
        }

        private string FindTemplatePath()
        {
            var assembly = typeof(TemplateManager).Assembly;
            var assemblyLocation = Path.GetDirectoryName(assembly.Location);

            if (assemblyLocation == null)
            {
                throw new InvalidOperationException("Could not determine assembly location");
            }

            var templatePath = Path.Combine(assemblyLocation, TemplateDirectory);

            if (!Directory.Exists(templatePath))
            {
                // Try looking in project directory
                templatePath = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "Pulsar.Compiler",
                    TemplateDirectory
                );
            }

            if (!Directory.Exists(templatePath))
            {
                throw new DirectoryNotFoundException(
                    $"Template directory not found at: {templatePath}"
                );
            }

            return templatePath;
        }
    }
}