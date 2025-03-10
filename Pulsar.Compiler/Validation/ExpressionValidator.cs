// File: Pulsar.Compiler/Validation/ExpressionValidator.cs

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Serilog;
using Pulsar.Compiler;

namespace Pulsar.Compiler.Validation
{
    public class ExpressionValidator
    {
        private static readonly ILogger _logger = LoggingConfig.GetLogger();
        private static readonly HashSet<string> AllowedOperators = new()
        {
            "+",
            "-",
            "*",
            "/",
            ">",
            "<",
            ">=",
            "<=",
            "==",
            "!=",
        };

        private static readonly HashSet<string> AllowedSpecialCharacters = new() { "(", ")" };

        private static readonly HashSet<string> AllowedFunctions = new()
        {
            "Math.Abs",
            "Math.Min",
            "Math.Max",
            "Math.Round",
            "Math.Pow",
            "Math.Sqrt",
            "Math.Sin",
            "Math.Cos",
            "Math.Tan",
            "Math.Log",
            "Math.Exp",
            "Math.Floor",
            "Math.Ceiling",
            // Add other allowed functions as needed
        };

        public static void ValidateExpression(string expression)
        {
            _logger.Debug($"Validating expression: {expression}");

            // Remove all whitespace for consistent processing
            expression = Regex.Replace(expression, @"\s+", "");
            _logger.Debug($"Expression after whitespace removal: {expression}");

            // Validate balanced parentheses
            ValidateParentheses(expression);

            // Split into tokens (operators, functions, variables, literals)
            var tokens = TokenizeExpression(expression);

            var tokenList = tokens.ToList();
            _logger.Debug($"Tokens: {string.Join(", ", tokenList)}");

            foreach (var token in tokenList)
            {
                _logger.Debug($"Processing token: {token}");
                if (IsOperator(token))
                {
                    if (!AllowedOperators.Contains(token))
                    {
                        throw new ArgumentException($"Invalid operator in expression: {token}");
                    }
                }
                else if (IsFunction(token))
                {
                    if (!AllowedFunctions.Contains(token))
                    {
                        throw new ArgumentException($"Invalid function in expression: {token}");
                    }
                }
                else if (
                    !IsValidIdentifier(token)
                    && !IsNumeric(token)
                    && !AllowedSpecialCharacters.Contains(token)
                )
                {
                    throw new ArgumentException($"Invalid token in expression: {token}");
                }
            }
        }

        private static void ValidateParentheses(string expression)
        {
            int count = 0;
            foreach (char c in expression)
            {
                if (c == '(')
                    count++;
                if (c == ')')
                    count--;
                if (count < 0)
                {
                    throw new ArgumentException("Unmatched parentheses in expression");
                }
            }
            if (count != 0)
            {
                throw new ArgumentException("Unmatched parentheses in expression");
            }
        }

        private static IEnumerable<string> TokenizeExpression(string expression)
        {
            // Updated pattern to better handle decimals and function calls
            var tokenPattern =
                @"(?:Math\.[a-zA-Z][a-zA-Z0-9]*)|[a-zA-Z_][a-zA-Z0-9_]*|\d+\.\d+|\d+|[+\-*/><]=?|==|!=|\(|\)";
            return Regex.Matches(expression, tokenPattern).Select(m => m.Value);
        }

        private static bool IsOperator(string token) => AllowedOperators.Contains(token);

        private static bool IsFunction(string token) => token.Contains(".") && !IsNumeric(token); // Don't treat decimals as functions

        private static bool IsValidIdentifier(string token) =>
            Regex.IsMatch(token, @"^[a-zA-Z_][a-zA-Z0-9_]*$");

        private static bool IsNumeric(string token) => double.TryParse(token, out _);

        private static void ValidateSyntax(string expression)
        {
            var code =
                $@"
            using System;
            public class ExpressionValidator {{
                public bool Validate() {{
                    return {expression};
                }}
            }}";

            var syntaxTree = CSharpSyntaxTree.ParseText(code);
            var diagnostics = syntaxTree
                .GetDiagnostics()
                .Where(d => d.Severity == DiagnosticSeverity.Error);

            if (diagnostics.Any())
            {
                throw new ArgumentException(
                    "Syntax validation failed: "
                        + string.Join(", ", diagnostics.Select(d => d.GetMessage()))
                );
            }
        }

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
                if (
                    Regex.IsMatch(
                        fullFunctionName,
                        @"[a-zA-Z_][a-zA-Z0-9_]*\.[a-zA-Z_][a-zA-Z0-9_]*"
                    )
                )
                {
                    if (!AllowedFunctions.Contains(fullFunctionName))
                    {
                        throw new ArgumentException(
                            $"Unauthorized function call: {fullFunctionName}"
                        );
                    }
                }
            }
        }

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
                    throw new ArgumentException($"Unauthorized operator: {op}");
                }
            }
        }

        private static void ValidateIdentifiers(string expression)
        {
            // Identify potential identifiers (sensors, variables)
            var identifierPattern = @"\b([a-zA-Z_][a-zA-Z0-9_]*)\b";
            var matches = Regex.Matches(expression, identifierPattern);

            foreach (Match match in matches)
            {
                var identifier = match.Value;

                // Exclude known keywords and math functions
                if (
                    IsReservedKeyword(identifier)
                    || AllowedFunctions.Any(f => f.EndsWith(identifier))
                )
                {
                    continue;
                }

                // Additional validation can be added here
                // For example, checking against a predefined set of valid sensors
            }
        }

        private static bool IsReservedKeyword(string identifier)
        {
            string[] reservedKeywords =
            {
                "true",
                "false",
                "null",
                "int",
                "double",
                "float",
                "decimal",
                "return",
                "if",
                "else",
                "for",
                "while",
            };

            return reservedKeywords.Contains(identifier);
        }
    }
}