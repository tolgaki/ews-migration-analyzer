using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ews.Analyzer;

namespace Ews.Analyzer.McpService;

/// <summary>
/// Orchestrates EWS-to-Graph conversion using Tier 1 deterministic transforms only.
///
/// Architecture: The AI assistant (Claude Code, GitHub Copilot) is the driver of
/// code conversion. This orchestrator provides deterministic transforms, analysis,
/// and validation — the AI handles all non-deterministic code generation itself.
/// </summary>
internal sealed class ConversionOrchestrator
{
    private readonly DeterministicTransformer _tier1;
    private readonly ConversionValidator _validator;
    private readonly AnalysisService _analysis;
    private readonly EwsMigrationNavigator _navigator;

    public ConversionOrchestrator(
        AnalysisService analysis,
        EwsMigrationNavigator navigator)
    {
        _analysis = analysis;
        _navigator = navigator;
        _validator = new ConversionValidator();
        _tier1 = new DeterministicTransformer(navigator);
    }

    /// <summary>
    /// Expose the validator for standalone validation via MCP tool.
    /// </summary>
    internal ConversionValidator Validator => _validator;

    /// <summary>
    /// Expose the deterministic transformer for standalone use via MCP tool.
    /// </summary>
    internal DeterministicTransformer Transformer => _tier1;

    /// <summary>
    /// Convert a single EWS code snippet using Tier 1 deterministic transforms.
    /// </summary>
    public async Task<ConversionResult> ConvertSnippetAsync(
        string code,
        int? forceTier = null,
        CancellationToken ct = default)
    {
        var analysisResult = await _analysis.AnalyzeSnippetAsync(code, null, ct);
        var ewsUsages = ExtractEwsUsages(analysisResult);

        if (!ewsUsages.Any())
        {
            return new ConversionResult
            {
                Tier = 0,
                Confidence = "high",
                OriginalCode = code,
                ConvertedCode = code,
                IsValid = true,
                EwsQualifiedName = null,
                GraphApiName = null
            };
        }

        var usage = ewsUsages.First();
        return ConvertSingleUsage(code, usage.QualifiedName, usage.Line, null);
    }

    /// <summary>
    /// Convert all EWS usages in a single file using Tier 1 deterministic transforms.
    /// </summary>
    public async Task<List<ConversionResult>> ConvertFileAsync(
        string filePath,
        int? forceTier = null,
        CancellationToken ct = default)
    {
        var code = await File.ReadAllTextAsync(filePath, ct);
        var analysisResult = await _analysis.AnalyzeSnippetAsync(code, filePath, ct);
        var ewsUsages = ExtractEwsUsages(analysisResult);

        var results = new List<ConversionResult>();
        foreach (var usage in ewsUsages.OrderByDescending(u => u.Line))
        {
            ct.ThrowIfCancellationRequested();
            var result = ConvertSingleUsage(code, usage.QualifiedName, usage.Line, filePath);
            result.UnifiedDiff = GenerateUnifiedDiff(filePath, code, result);
            results.Add(result);
        }

        return results;
    }

    /// <summary>
    /// Convert all EWS usages across a project directory using Tier 1 deterministic transforms.
    /// </summary>
    public async Task<ProjectConversionResult> ConvertProjectAsync(
        string rootPath,
        int maxFiles = 200,
        int? forceTier = null,
        CancellationToken ct = default)
    {
        var files = Directory.EnumerateFiles(rootPath, "*.cs", SearchOption.AllDirectories)
            .Take(maxFiles)
            .ToList();

        var allResults = new List<ConversionResult>();
        int filesProcessed = 0;

        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            var fileResults = await ConvertFileAsync(file, forceTier, ct);
            allResults.AddRange(fileResults);
            filesProcessed++;
        }

        return new ProjectConversionResult
        {
            Conversions = allResults,
            Summary = ComputeSummary(allResults, filesProcessed)
        };
    }

    /// <summary>
    /// Convert a single EWS usage using Tier 1 deterministic transform only.
    /// Returns a guidance result if no deterministic transform is available.
    /// </summary>
    private ConversionResult ConvertSingleUsage(
        string code,
        string ewsQualifiedName,
        int line,
        string? filePath)
    {
        var roadmap = _navigator.GetMapByEwsSdkQualifiedName(ewsQualifiedName);

        // Tier 1: Deterministic transform
        var tier1Result = _tier1.Transform(code, ewsQualifiedName, line, filePath);
        if (tier1Result != null)
        {
            _validator.Validate(tier1Result);
            if (tier1Result.IsValid)
                return tier1Result;
        }

        // No deterministic transform available — return guidance for AI assistant
        return CreateGuidanceResult(code, ewsQualifiedName, line, filePath, roadmap);
    }

    /// <summary>
    /// Create a result that provides structured guidance for the AI assistant
    /// to perform the conversion itself, instead of calling an external LLM.
    /// </summary>
    private static ConversionResult CreateGuidanceResult(
        string code,
        string ewsQualifiedName,
        int line,
        string? filePath,
        EwsMigrationRoadmap? roadmap)
    {
        var graphName = roadmap?.GraphApiDisplayName ?? "Graph API equivalent";
        var graphHttp = roadmap?.GraphApiHttpRequest ?? "";
        var docsUrl = roadmap?.GraphApiDocumentationUrl ?? "https://learn.microsoft.com/graph/api/overview";
        var promptTemplate = roadmap?.CopilotPromptTemplate ?? "";

        var guidance = new StringBuilder();
        guidance.AppendLine($"// AI-ASSISTED CONVERSION NEEDED: {ewsQualifiedName} → {graphName}");
        if (!string.IsNullOrWhiteSpace(graphHttp))
            guidance.AppendLine($"// Graph API: {graphHttp}");
        guidance.AppendLine($"// Docs: {docsUrl}");
        if (!string.IsNullOrWhiteSpace(promptTemplate))
            guidance.AppendLine($"// Guidance: {promptTemplate}");
        guidance.AppendLine(code);

        return new ConversionResult
        {
            Tier = 0,
            Confidence = "low",
            OriginalCode = code,
            ConvertedCode = guidance.ToString(),
            FilePath = filePath,
            StartLine = line,
            EndLine = line,
            IsValid = false,
            ValidationErrors = new List<string> { "No deterministic transform available. Use the AI assistant with getConversionContext for guided conversion." },
            EwsQualifiedName = ewsQualifiedName,
            GraphApiName = roadmap?.GraphApiDisplayName
        };
    }

    internal static List<EwsUsageInfo> ExtractEwsUsages(SnippetAnalysisResult result)
    {
        var usages = new List<EwsUsageInfo>();
        foreach (var d in result.Diagnostics)
        {
            if (d.EwsUsage == null || !d.Id.StartsWith("EWS")) continue;

            string? qualifiedName = null;
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(d.EwsUsage);
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("sdkQualifiedName", out var qn))
                    qualifiedName = qn.GetString();
            }
            catch (System.Text.Json.JsonException)
            {
                continue;
            }

            if (!string.IsNullOrEmpty(qualifiedName))
            {
                usages.Add(new EwsUsageInfo
                {
                    QualifiedName = qualifiedName,
                    Line = d.Line,
                    DiagnosticId = d.Id
                });
            }
        }
        return usages;
    }

    private static string? GenerateUnifiedDiff(string filePath, string originalCode, ConversionResult result)
    {
        if (string.IsNullOrWhiteSpace(result.ConvertedCode) || result.ConvertedCode == result.OriginalCode)
            return null;

        var relative = Path.GetRelativePath(Directory.GetCurrentDirectory(), filePath);
        var sb = new StringBuilder();
        sb.AppendLine($"--- a/{relative}");
        sb.AppendLine($"+++ b/{relative}");
        sb.AppendLine($"@@ -{result.StartLine},1 +{result.StartLine},1 @@");

        var originalLines = result.OriginalCode.Split('\n');
        foreach (var line in originalLines)
            sb.AppendLine($"-{line}");

        var convertedLines = result.ConvertedCode.Split('\n');
        foreach (var line in convertedLines)
            sb.AppendLine($"+{line}");

        return sb.ToString();
    }

    private static ConversionSummary ComputeSummary(List<ConversionResult> results, int filesScanned)
    {
        return new ConversionSummary
        {
            FilesScanned = filesScanned,
            TotalUsages = results.Count,
            Converted = results.Count(r => r.IsValid),
            HighConfidence = results.Count(r => r.Confidence == "high"),
            MediumConfidence = results.Count(r => r.Confidence == "medium"),
            LowConfidence = results.Count(r => r.Confidence == "low"),
            Failed = results.Count(r => !r.IsValid),
            ReadinessPercent = results.Count == 0 ? 0 :
                (double)results.Count(r => r.IsValid) / results.Count * 100
        };
    }
}

internal sealed class EwsUsageInfo
{
    public string QualifiedName { get; set; } = string.Empty;
    public int Line { get; set; }
    public string DiagnosticId { get; set; } = string.Empty;
}

internal sealed class ProjectConversionResult
{
    public List<ConversionResult> Conversions { get; set; } = new();
    public ConversionSummary Summary { get; set; } = new();
}

internal sealed class ConversionSummary
{
    public int FilesScanned { get; set; }
    public int TotalUsages { get; set; }
    public int Converted { get; set; }
    public int HighConfidence { get; set; }
    public int MediumConfidence { get; set; }
    public int LowConfidence { get; set; }
    public int Failed { get; set; }
    public double ReadinessPercent { get; set; }
}
