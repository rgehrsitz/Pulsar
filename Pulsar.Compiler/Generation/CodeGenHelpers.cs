// File: Pulsar.Compiler.Generation.CodeGenHelpers.cs

using System;

namespace Pulsar.Compiler.Generation
{
    /// <summary>
    /// Consolidated helper methods for code generation routines such as file header generation, namespace wrapping, common usings, and embedding source tracking comments.
    /// </summary>
    public static class CodeGenHelpers
    {
        /// <summary>
        /// Generates a standard file header comment including the file name and UTC generation timestamp.
        /// </summary>
        public static string GenerateFileHeader(string fileName)
        {
            return $"// Auto-generated file: {fileName}\n// Generated at: {DateTime.UtcNow.ToString("u")}\n";
        }

        /// <summary>
        /// Wraps the provided content in a namespace declaration.
        /// </summary>
        public static string WrapInNamespace(string namespaceName, string content)
        {
            return $"namespace {namespaceName}\n{{\n{content}\n}}";
        }

        /// <summary>
        /// Prepends common using statements to the content. Adjust as needed for your project.
        /// </summary>
        public static string PrependCommonUsings(string content)
        {
            string usings = "using System;\nusing System.Collections.Generic;\nusing System.IO;\n";
            return usings + "\n" + content;
        }

        /// <summary>
        /// Embeds source tracking comments that reference original rule sources into the generated content.
        /// </summary>
        public static string EmbedSourceTrackingComments(string content, string sourceReference)
        {
            string comment = $"// Source: {sourceReference}\n";
            return comment + content;
        }
    }
}
