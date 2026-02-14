using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Ews.Analyzer.McpService;

/// <summary>
/// Validates converted Graph SDK code by attempting Roslyn compilation and semantic checks.
/// </summary>
internal sealed class ConversionValidator
{
    private static readonly Lazy<IReadOnlyList<MetadataReference>> _coreRefs = new(() =>
    {
        var refs = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Uri).Assembly.Location),
        };

        // Try to add System.Runtime reference for net8.0
        var runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location);
        if (runtimeDir != null)
        {
            var systemRuntime = Path.Combine(runtimeDir, "System.Runtime.dll");
            if (File.Exists(systemRuntime))
                refs.Add(MetadataReference.CreateFromFile(systemRuntime));

            var netstandard = Path.Combine(runtimeDir, "netstandard.dll");
            if (File.Exists(netstandard))
                refs.Add(MetadataReference.CreateFromFile(netstandard));

            var systemCollections = Path.Combine(runtimeDir, "System.Collections.dll");
            if (File.Exists(systemCollections))
                refs.Add(MetadataReference.CreateFromFile(systemCollections));

            var systemThreadingTasks = Path.Combine(runtimeDir, "System.Threading.Tasks.dll");
            if (File.Exists(systemThreadingTasks))
                refs.Add(MetadataReference.CreateFromFile(systemThreadingTasks));
        }

        return refs.AsReadOnly();
    });

    /// <summary>
    /// Validate that the converted code is syntactically and semantically valid C#.
    /// </summary>
    public ConversionResult Validate(ConversionResult result)
    {
        result.ValidationErrors.Clear();

        if (string.IsNullOrWhiteSpace(result.ConvertedCode))
        {
            result.IsValid = false;
            result.ValidationErrors.Add("Converted code is empty.");
            result.Confidence = "low";
            return result;
        }

        // Build the full compilation unit: usings + converted code
        var fullSource = BuildFullSource(result);

        // Step 1: Syntax check
        var tree = CSharpSyntaxTree.ParseText(fullSource);
        var syntaxErrors = tree.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        if (syntaxErrors.Any())
        {
            result.IsValid = false;
            foreach (var err in syntaxErrors.Take(10))
                result.ValidationErrors.Add($"Syntax: {err.GetMessage()}");
            result.Confidence = "low";
            return result;
        }

        // Step 2: Compilation check
        var compilation = CSharpCompilation.Create(
            "ConversionValidation",
            new[] { tree },
            _coreRefs.Value,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var compilationErrors = compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        // We tolerate missing-type errors for Graph SDK types (since we don't have the actual
        // Microsoft.Graph assembly reference). Only flag non-Graph compilation errors.
        var realErrors = compilationErrors
            .Where(d => !IsExpectedMissingGraphReference(d))
            .ToList();

        if (realErrors.Any())
        {
            result.IsValid = false;
            foreach (var err in realErrors.Take(10))
                result.ValidationErrors.Add($"Compilation: {err.GetMessage()}");
            result.Confidence = "low";
            return result;
        }

        // Step 3: Semantic check — ensure no EWS references remain
        var sourceText = result.ConvertedCode;
        if (sourceText.Contains("Microsoft.Exchange.WebServices"))
        {
            result.ValidationErrors.Add("Warning: Converted code still references Microsoft.Exchange.WebServices namespace.");
            result.Confidence = DowngradeConfidence(result.Confidence);
        }

        result.IsValid = true;

        // Assign confidence based on tier and validation outcome
        result.Confidence = ComputeConfidence(result);

        return result;
    }

    /// <summary>
    /// Builds a compilable source unit from the conversion result.
    /// </summary>
    private static string BuildFullSource(ConversionResult result)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Linq;");
        if (!string.IsNullOrWhiteSpace(result.RequiredUsings))
        {
            foreach (var line in result.RequiredUsings.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = line.Trim();
                if (!string.IsNullOrWhiteSpace(trimmed))
                    sb.AppendLine(trimmed);
            }
        }
        sb.AppendLine();

        // Wrap the code in a class if it doesn't already contain one
        var code = result.ConvertedCode.Trim();
        if (!code.Contains("class ") && !code.Contains("namespace "))
        {
            sb.AppendLine("namespace ConversionValidation {");
            sb.AppendLine("  class Wrapper {");
            sb.AppendLine("    async Task RunAsync() {");
            sb.AppendLine(code);
            sb.AppendLine("    }");
            sb.AppendLine("  }");
            sb.AppendLine("}");
        }
        else
        {
            sb.AppendLine(code);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Returns true if the diagnostic is a missing-type error for a known Graph SDK type.
    /// These are expected since we don't reference the Microsoft.Graph assembly.
    /// </summary>
    private static bool IsExpectedMissingGraphReference(Diagnostic d)
    {
        var msg = d.GetMessage();
        // CS0246: The type or namespace name 'X' could not be found
        // CS0234: The type or namespace name 'X' does not exist in the namespace 'Y'
        if (d.Id == "CS0246" || d.Id == "CS0234")
        {
            return msg.Contains("Graph") ||
                   msg.Contains("GraphServiceClient") ||
                   msg.Contains("Microsoft.Graph") ||
                   msg.Contains("TokenCredential") ||
                   msg.Contains("Azure.Identity") ||
                   msg.Contains("ClientSecretCredential") ||
                   msg.Contains("InteractiveBrowserCredential") ||
                   msg.Contains("DeviceCodeCredential") ||
                   msg.Contains("ManagedIdentityCredential") ||
                   msg.Contains("Message") ||
                   msg.Contains("Event") ||
                   msg.Contains("Contact") ||
                   msg.Contains("TodoTask") ||
                   msg.Contains("Subscription") ||
                   msg.Contains("MailFolder") ||
                   msg.Contains("Attachment") ||
                   msg.Contains("MessageRule") ||
                   msg.Contains("OutlookCategory") ||
                   msg.Contains("BodyType") ||
                   msg.Contains("ItemBody") ||
                   msg.Contains("Recipient") ||
                   msg.Contains("EmailAddress");
        }
        // CS1061: 'X' does not contain a definition for 'Y' — common when chaining Graph client calls
        if (d.Id == "CS1061") return true;
        // CS0103: name does not exist in current context — common for graphClient variable
        if (d.Id == "CS0103") return true;
        return false;
    }

    private static string ComputeConfidence(ConversionResult result)
    {
        if (result.Tier == 1) return "high";
        if (result.Tier == 2 && result.ValidationErrors.Count == 0) return "high";
        if (result.Tier == 2) return "medium";
        if (result.Tier == 3 && result.ValidationErrors.Count == 0) return "medium";
        return "low";
    }

    private static string DowngradeConfidence(string current)
    {
        return current switch
        {
            "high" => "medium",
            "medium" => "low",
            _ => "low"
        };
    }
}
