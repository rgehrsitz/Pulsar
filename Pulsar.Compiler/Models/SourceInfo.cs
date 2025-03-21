// File: Pulsar.Compiler/Models/SourceInfo.cs

using Serilog;

namespace Pulsar.Compiler.Models
{
    public class SourceInfo
    {
        private static readonly ILogger _logger = LoggingConfig.GetLogger();

        public required string FilePath { get; set; }
        public required string Content { get; set; }
        public DateTime LastModified { get; set; }
        public required string Hash { get; set; }

        public static SourceInfo FromFile(string path)
        {
            try
            {
                _logger.Debug("Loading source info from {Path}", path);

                var fileInfo = new FileInfo(path);
                var content = File.ReadAllText(path);
                var hash = CalculateHash(content);

                _logger.Debug("Source file loaded successfully. Hash: {Hash}", hash);

                return new SourceInfo
                {
                    FilePath = path,
                    Content = content,
                    LastModified = fileInfo.LastWriteTimeUtc,
                    Hash = hash,
                };
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to load source info from {Path}", path);
                throw;
            }
        }

        private static string CalculateHash(string content)
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var bytes = System.Text.Encoding.UTF8.GetBytes(content);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }
    }
}
