using System.Collections.Generic;

namespace Ews.Analyzer.McpService;

/// <summary>
/// Result of converting a single EWS usage to Microsoft Graph SDK code.
/// </summary>
internal sealed class ConversionResult
{
    /// <summary>Which tier produced this result (1 = deterministic, 2 = template-LLM, 3 = full-context-LLM).</summary>
    public int Tier { get; set; }

    /// <summary>Confidence level: "high", "medium", or "low".</summary>
    public string Confidence { get; set; } = "low";

    /// <summary>The original EWS source code that was converted.</summary>
    public string OriginalCode { get; set; } = string.Empty;

    /// <summary>The replacement Graph SDK code.</summary>
    public string ConvertedCode { get; set; } = string.Empty;

    /// <summary>Using statements that must be added to the file.</summary>
    public string? RequiredUsings { get; set; }

    /// <summary>NuGet package needed (e.g. "Microsoft.Graph").</summary>
    public string? RequiredPackage { get; set; }

    /// <summary>Source file path (null for inline snippets).</summary>
    public string? FilePath { get; set; }

    /// <summary>1-based start line in the original file.</summary>
    public int StartLine { get; set; }

    /// <summary>1-based end line in the original file.</summary>
    public int EndLine { get; set; }

    /// <summary>Whether the converted code passed Roslyn compilation validation.</summary>
    public bool IsValid { get; set; }

    /// <summary>Compilation or semantic errors found during validation.</summary>
    public List<string> ValidationErrors { get; set; } = new();

    /// <summary>Unified diff ready to apply.</summary>
    public string? UnifiedDiff { get; set; }

    /// <summary>The EWS SDK qualified name that was converted.</summary>
    public string? EwsQualifiedName { get; set; }

    /// <summary>The Graph API display name for the equivalent operation.</summary>
    public string? GraphApiName { get; set; }
}
