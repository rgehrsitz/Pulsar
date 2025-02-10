// File: Pulsar.Compiler/Config/Templates.cs

using System.Reflection;
using System.Text;

namespace Pulsar.Compiler.Config;

internal static class Templates
{
    public static string GetTemplate(string templateName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = $"Pulsar.Compiler.Config.Templates.{templateName}";

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            throw new InvalidOperationException(
                $"Template '{templateName}' not found in assembly {assembly.FullName}. Available resources: {string.Join(", ", assembly.GetManifestResourceNames())}"
            );
        }

        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
