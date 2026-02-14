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
/// Orchestrates EWS-to-Graph conversion across three tiers:
///   Tier 1: Deterministic Roslyn transforms
///   Tier 2: Template-guided LLM conversion
///   Tier 3: Full-context LLM conversion
/// </summary>
internal sealed class ConversionOrchestrator
{
    private readonly DeterministicTransformer _tier1;
    private readonly TemplateGuidedConverter _tier2;
    private readonly FullContextConverter _tier3;
    private readonly ConversionValidator _validator;
    private readonly AnalysisService _analysis;
    private readonly EwsMigrationNavigator _navigator;
    private readonly int _maxTier;

    public ConversionOrchestrator(
        AnalysisService analysis,
        EwsMigrationNavigator navigator,
        ILlmClient? llmClient = null,
        int maxTier = 3)
    {
        _analysis = analysis;
        _navigator = navigator;
        _maxTier = maxTier;
        _validator = new ConversionValidator();
        _tier1 = new DeterministicTransformer(navigator);

        var llm = llmClient ?? CreateDefaultLlmClient();
        _tier2 = new TemplateGuidedConverter(navigator, llm);
        _tier3 = new FullContextConverter(navigator, llm);
    }

    private static ILlmClient CreateDefaultLlmClient()
    {
        // If LLM_ENDPOINT is configured, use HTTP client; otherwise use MCP relay
        var endpoint = Environment.GetEnvironmentVariable("LLM_ENDPOINT");
        if (!string.IsNullOrWhiteSpace(endpoint))
            return new HttpLlmClient();
        return new McpRelayLlmClient();
    }

    /// <summary>
    /// Convert a single EWS code snippet.
    /// </summary>
    public async Task<ConversionResult> ConvertSnippetAsync(
        string code,
        int? forceTier = null,
        CancellationToken ct = default)
    {
        // Analyze the code to find EWS usages
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

        // For single-usage snippets, convert the first usage
        var usage = ewsUsages.First();
        return await ConvertSingleUsageAsync(code, usage.QualifiedName, usage.Line, null, forceTier, ct);
    }

    /// <summary>
    /// Convert all EWS usages in a single file.
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
        // Process bottom-to-top so line numbers don't shift
        foreach (var usage in ewsUsages.OrderByDescending(u => u.Line))
        {
            ct.ThrowIfCancellationRequested();
            var result = await ConvertSingleUsageAsync(code, usage.QualifiedName, usage.Line, filePath, forceTier, ct);
            result.UnifiedDiff = GenerateUnifiedDiff(filePath, code, result);
            results.Add(result);
        }

        return results;
    }

    /// <summary>
    /// Convert all EWS usages across a project directory.
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
    /// Convert a single EWS usage through the tiered pipeline.
    /// </summary>
    private async Task<ConversionResult> ConvertSingleUsageAsync(
        string code,
        string ewsQualifiedName,
        int line,
        string? filePath,
        int? forceTier,
        CancellationToken ct)
    {
        var roadmap = _navigator.GetMapByEwsSdkQualifiedName(ewsQualifiedName);
        var effectiveTier = forceTier ?? roadmap.ConversionTier;

        // Tier 1: Deterministic transform
        if (effectiveTier <= 1 || forceTier == null)
        {
            var tier1Result = _tier1.Transform(code, ewsQualifiedName, line, filePath);
            if (tier1Result != null)
            {
                _validator.Validate(tier1Result);
                if (tier1Result.IsValid)
                    return tier1Result;
                // Tier 1 failed validation — fall through to Tier 2
            }
        }

        if (_maxTier < 2) return CreateFallbackResult(code, ewsQualifiedName, line, filePath, roadmap);

        // Tier 2: Template-guided LLM
        if (effectiveTier <= 2 || forceTier == null)
        {
            var tier2Result = await _tier2.ConvertAsync(code, ewsQualifiedName, line, filePath, null, ct);
            _validator.Validate(tier2Result);

            if (tier2Result.IsValid)
                return tier2Result;

            // Retry once with error context
            var retryResult = await _tier2.RetryWithErrorsAsync(tier2Result, ct);
            _validator.Validate(retryResult);

            if (retryResult.IsValid)
            {
                retryResult.Confidence = "medium"; // Downgrade since it needed a retry
                return retryResult;
            }

            // Tier 2 exhausted — fall through to Tier 3
        }

        if (_maxTier < 3) return CreateFallbackResult(code, ewsQualifiedName, line, filePath, roadmap);

        // Tier 3: Full-context LLM
        var tier3Result = await _tier3.ConvertAsync(
            code,
            new[] { ewsQualifiedName },
            line,
            filePath,
            ct);
        _validator.Validate(tier3Result);

        // Return Tier 3 result even if invalid (with errors for human review)
        return tier3Result;
    }

    private static ConversionResult CreateFallbackResult(string code, string ewsQualifiedName, int line, string? filePath, EwsMigrationRoadmap roadmap)
    {
        return new ConversionResult
        {
            Tier = 0,
            Confidence = "low",
            OriginalCode = code,
            ConvertedCode = $"// TODO: Manually convert {ewsQualifiedName} to {roadmap.GraphApiDisplayName ?? "Graph API equivalent"}\n// See: {roadmap.GraphApiDocumentationUrl}\n{code}",
            FilePath = filePath,
            StartLine = line,
            EndLine = line,
            IsValid = false,
            ValidationErrors = new List<string> { "Automatic conversion not available at the configured tier level." },
            EwsQualifiedName = ewsQualifiedName,
            GraphApiName = roadmap.GraphApiDisplayName
        };
    }

    /// <summary>
    /// Extract EWS usage information from analysis results.
    /// </summary>
    private static List<EwsUsageInfo> ExtractEwsUsages(SnippetAnalysisResult result)
    {
        var usages = new List<EwsUsageInfo>();
        foreach (var d in result.Diagnostics)
        {
            if (d.EwsUsage == null || !d.Id.StartsWith("EWS")) continue;

            // Extract the qualified name from the EwsUsage object
            string? qualifiedName = null;
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(d.EwsUsage);
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("sdkQualifiedName", out var qn))
                    qualifiedName = qn.GetString();
            }
            catch { }

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

    /// <summary>
    /// Generate a unified diff for a conversion result.
    /// </summary>
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
