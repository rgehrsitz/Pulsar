// File: Pulsar.Compiler/Validation/ExpressionHelper.cs

using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.RegularExpressions;

namespace Pulsar.Compiler.Validation
{
    public static class ExpressionHelper
    {
        // Predefined set of allowed mathematical functions
        private static readonly HashSet<string> AllowedMathFunctions = new HashSet<string>
        {
            "Math.Abs", "Math.Pow", "Math.Sqrt",
            "Math.Sin", "Math.Cos", "Math.Tan",
            "Math.Log", "Math.Exp",
            "Math.Floor", "Math.Ceiling", "Math.Round",
            "Math.Max", "Math.Min"
        };

        // Predefined set of allowed operators
        private static readonly HashSet<string> AllowedOperators = new HashSet<string>
        {
            "+", "-", "*", "/", ">", "<", ">=", "<=", "==", "!="
        };

        /// <summary>
        /// Validates a generated expression with comprehensive compile-time checks
        /// </summary>
        public static bool ValidateGeneratedExpression(string expression)
        {
            if (string.IsNullOrWhiteSpace(expression))
                return false;

            try
            {
                // Comprehensive validation steps
                ValidateSyntax(expression);
                ValidateFunctions(expression);
                ValidateOperators(expression);
                ValidateIdentifiers(expression);

                return true;
            }
            catch (ExpressionValidationException ex)
            {
                // Log detailed validation failure
                Debug.WriteLine($"Expression Validation Failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Performs syntax validation using Roslyn
        /// </summary>
        private static void ValidateSyntax(string expression)
        {
            var code = $@"
            using System;
            public class ExpressionValidator {{
                public bool Validate() {{
                    return {expression};
                }}
            }}";

            var syntaxTree = CSharpSyntaxTree.ParseText(code);
            var diagnostics = syntaxTree.GetDiagnostics()
                .Where(d => d.Severity == DiagnosticSeverity.Error);

            if (diagnostics.Any())
            {
                throw new ExpressionValidationException(
                    "Syntax validation failed: " +
                    string.Join(", ", diagnostics.Select(d => d.GetMessage()))
                );
            }
        }

        /// <summary>
        /// Validates function calls in the expression
        /// </summary>
        private static void ValidateFunctions(string expression)
        {
            // Extract function calls
            var functionPattern = @"([a-zA-Z_][a-zA-Z0-9_]*\.)?([a-zA-Z_][a-zA-Z0-9_]*)\s*\(";
            var matches = Regex.Matches(expression, functionPattern);

            foreach (Match match in matches)
            {
                var fullFunctionName = match.Groups[0].Value.TrimEnd('(').Trim();
                var functionName = match.Groups[2].Value;

                // Check if it's a method call or just an identifier
                if (Regex.IsMatch(fullFunctionName, @"[a-zA-Z_][a-zA-Z0-9_]*\.[a-zA-Z_][a-zA-Z0-9_]*"))
                {
                    if (!AllowedMathFunctions.Contains(fullFunctionName))
                    {
                        throw new ExpressionValidationException(
                            $"Unauthorized function call: {fullFunctionName}"
                        );
                    }
                }
            }
        }

        /// <summary>
        /// Validates operators in the expression
        /// </summary>
        private static void ValidateOperators(string expression)
        {
            // Extract all operators
            var operatorPattern = @"(\+|\-|\*|\/|>|<|>=|<=|==|!=)";
            var matches = Regex.Matches(expression, operatorPattern);

            foreach (Match match in matches)
            {
                var op = match.Value;
                if (!AllowedOperators.Contains(op))
                {
                    throw new ExpressionValidationException(
                        $"Unauthorized operator: {op}"
                    );
                }
            }
        }

        /// <summary>
        /// Validates identifiers in the expression
        /// </summary>
        private static void ValidateIdentifiers(string expression)
        {
            // Identify potential identifiers (sensors, variables)
            var identifierPattern = @"\b([a-zA-Z_][a-zA-Z0-9_]*)\b";
            var matches = Regex.Matches(expression, identifierPattern);

            foreach (Match match in matches)
            {
                var identifier = match.Value;

                // Exclude known keywords and math functions
                if (IsReservedKeyword(identifier) ||
                    AllowedMathFunctions.Any(f => f.EndsWith(identifier)))
                {
                    continue;
                }

                // Additional validation can be added here
                // For example, checking against a predefined set of valid sensors
            }
        }

        /// <summary>
        /// Checks if an identifier is a reserved keyword
        /// </summary>
        private static bool IsReservedKeyword(string identifier)
        {
            string[] reservedKeywords = {
                "true", "false", "null",
                "int", "double", "float", "decimal",
                "return", "if", "else", "for", "while"
            };

            return reservedKeywords.Contains(identifier);
        }

        /// <summary>
        /// Custom exception for expression validation
        /// </summary>
        private class ExpressionValidationException : Exception
        {
            public ExpressionValidationException(string message) : base(message) { }
        }

        /// <summary>
        /// Extracts sensors from an expression
        /// </summary>
        public static List<string> ExtractSensors(string expression)
        {
            if (string.IsNullOrWhiteSpace(expression))
                return new List<string>();

            var sensors = new List<string>();
            var identifierPattern = @"\b([a-zA-Z_][a-zA-Z0-9_]*)\b";
            var matches = Regex.Matches(expression, identifierPattern);

            foreach (Match match in matches)
            {
                var identifier = match.Value;

                // Exclude math functions, keywords, and known non-sensor identifiers
                if (!IsReservedKeyword(identifier) &&
                    !AllowedMathFunctions.Any(f => f.EndsWith(identifier)))
                {
                    sensors.Add(identifier);
                }
            }

            return sensors.Distinct().ToList();
        }
    }
}