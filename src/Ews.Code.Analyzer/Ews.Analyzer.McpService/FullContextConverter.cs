using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Ews.Analyzer;

namespace Ews.Analyzer.McpService;

/// <summary>
/// Tier 3: Full-context LLM conversion. Sends entire class/file context to the LLM
/// for complex patterns, multi-operation methods, or Gap/TBD operations.
/// </summary>
internal sealed class FullContextConverter
{
    private readonly EwsMigrationNavigator _navigator;
    private readonly ILlmClient _llm;
    private const int MaxContextChars = 32000; // ~8000 tokens

    public FullContextConverter(EwsMigrationNavigator navigator, ILlmClient llm)
    {
        _navigator = navigator;
        _llm = llm;
    }

    /// <summary>
    /// Convert EWS code with full class/file context.
    /// </summary>
    public async Task<ConversionResult> ConvertAsync(
        string fullClassOrFileCode,
        IEnumerable<string> ewsQualifiedNames,
        int startLine,
        string? filePath = null,
        CancellationToken ct = default)
    {
        var roadmaps = ewsQualifiedNames
            .Select(qn => _navigator.GetMapByEwsSdkQualifiedName(qn))
            .Where(r => r != null)
            .ToList()!;

        var systemPrompt = BuildSystemPrompt();
        var userPrompt = BuildUserPrompt(fullClassOrFileCode, roadmaps);

        var response = await _llm.CompleteAsync(systemPrompt, userPrompt, ct);
        var (convertedCode, usings) = TemplateGuidedConverter.ParseLlmResponse(response);

        var hasGapOperations = roadmaps.Any(r =>
            !r.GraphApiHasParity ||
            r.GraphApiStatus == "Gap" ||
            r.GraphApiEta == "TBD");

        return new ConversionResult
        {
            Tier = 3,
            Confidence = hasGapOperations ? "low" : "medium",
            OriginalCode = TruncateForResult(fullClassOrFileCode),
            ConvertedCode = convertedCode,
            RequiredUsings = usings ?? "using Microsoft.Graph;\nusing Microsoft.Graph.Models;\nusing Azure.Identity;",
            RequiredPackage = "Microsoft.Graph, Azure.Identity",
            FilePath = filePath,
            StartLine = startLine,
            EndLine = startLine,
            EwsQualifiedName = string.Join(", ", ewsQualifiedNames),
            GraphApiName = string.Join(", ", roadmaps.Select(r => r.GraphApiDisplayName).Where(n => n != null))
        };
    }

    private static string BuildSystemPrompt()
    {
        return @"You are a code migration expert specializing in converting Exchange Web Services (EWS) SDK code to Microsoft Graph SDK v5+ in C#.

Rules:
- Convert ALL EWS usages in the provided code to their Graph SDK equivalents
- Preserve the class structure, method signatures, and variable names where possible
- Use Microsoft.Graph SDK v5+ fluent API patterns
- Use async/await throughout
- For operations with no Graph equivalent, add a // WARNING comment explaining the gap and suggesting alternatives
- Include required using statements as a separate block prefixed with [USINGS]
- Include the full converted code prefixed with [CODE]
- Convert authentication from ExchangeService/WebCredentials to GraphServiceClient/TokenCredential

Output format:
[USINGS]
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Azure.Identity;

[CODE]
// Full converted class/code here";
    }

    private string BuildUserPrompt(string fullCode, List<EwsMigrationRoadmap> roadmaps)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Convert this entire class/file from EWS to Microsoft Graph SDK.");
        sb.AppendLine();

        sb.AppendLine("EWS operations found in this code:");
        foreach (var rm in roadmaps)
        {
            sb.AppendLine($"  - {rm.EwsSdkQualifiedName} → {rm.GraphApiDisplayName ?? "NO EQUIVALENT"} ({rm.GraphApiStatus})");
            if (!string.IsNullOrWhiteSpace(rm.GraphApiHttpRequest))
                sb.AppendLine($"    HTTP: {rm.GraphApiHttpRequest}");
            if (!rm.GraphApiHasParity)
                sb.AppendLine($"    WARNING: No Graph parity. Gap plan: {rm.GraphApiGapFillPlan}");
        }

        sb.AppendLine();
        sb.AppendLine("Source code to convert:");
        sb.AppendLine("```csharp");
        var truncated = fullCode.Length > MaxContextChars
            ? fullCode.Substring(0, MaxContextChars) + "\n// ... truncated ..."
            : fullCode;
        sb.AppendLine(truncated);
        sb.AppendLine("```");

        return sb.ToString();
    }

    private static string TruncateForResult(string code)
    {
        const int maxLen = 2000;
        return code.Length > maxLen
            ? code.Substring(0, maxLen) + "\n// ... truncated ..."
            : code;
    }
}
