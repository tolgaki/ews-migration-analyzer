using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Ews.Analyzer;

namespace Ews.Analyzer.McpService;

/// <summary>
/// Tier 2: Template-guided LLM conversion. Uses the roadmap's copilotPromptTemplate
/// enriched with code context to ask an LLM for the Graph SDK replacement.
/// Operates at the method/statement level.
/// </summary>
internal sealed class TemplateGuidedConverter
{
    private readonly EwsMigrationNavigator _navigator;
    private readonly ILlmClient _llm;
    private const int MaxRetries = 1;

    public TemplateGuidedConverter(EwsMigrationNavigator navigator, ILlmClient llm)
    {
        _navigator = navigator;
        _llm = llm;
    }

    /// <summary>
    /// Convert a single EWS usage to Graph SDK code using template-guided LLM prompting.
    /// </summary>
    public async Task<ConversionResult> ConvertAsync(
        string code,
        string ewsQualifiedName,
        int line,
        string? filePath = null,
        string? surroundingMethod = null,
        CancellationToken ct = default)
    {
        var roadmap = _navigator.GetMapByEwsSdkQualifiedName(ewsQualifiedName);

        var systemPrompt = BuildSystemPrompt();
        var userPrompt = BuildUserPrompt(code, roadmap, surroundingMethod);

        var response = await _llm.CompleteAsync(systemPrompt, userPrompt, ct);
        var (convertedCode, usings) = ParseLlmResponse(response);

        var result = new ConversionResult
        {
            Tier = 2,
            OriginalCode = code,
            ConvertedCode = convertedCode,
            RequiredUsings = usings ?? "using Microsoft.Graph;\nusing Microsoft.Graph.Models;",
            RequiredPackage = "Microsoft.Graph",
            FilePath = filePath,
            StartLine = line,
            EndLine = line,
            EwsQualifiedName = ewsQualifiedName,
            GraphApiName = roadmap.GraphApiDisplayName
        };

        return result;
    }

    /// <summary>
    /// Retry conversion with compilation error context appended to the prompt.
    /// </summary>
    public async Task<ConversionResult> RetryWithErrorsAsync(
        ConversionResult previousResult,
        CancellationToken ct = default)
    {
        var roadmap = _navigator.GetMapByEwsSdkQualifiedName(previousResult.EwsQualifiedName ?? string.Empty);

        var systemPrompt = BuildSystemPrompt();
        var userPrompt = BuildRetryPrompt(previousResult, roadmap);

        var response = await _llm.CompleteAsync(systemPrompt, userPrompt, ct);
        var (convertedCode, usings) = ParseLlmResponse(response);

        return new ConversionResult
        {
            Tier = 2,
            OriginalCode = previousResult.OriginalCode,
            ConvertedCode = convertedCode,
            RequiredUsings = usings ?? previousResult.RequiredUsings,
            RequiredPackage = previousResult.RequiredPackage,
            FilePath = previousResult.FilePath,
            StartLine = previousResult.StartLine,
            EndLine = previousResult.EndLine,
            EwsQualifiedName = previousResult.EwsQualifiedName,
            GraphApiName = previousResult.GraphApiName
        };
    }

    private static string BuildSystemPrompt()
    {
        return @"You are a code migration assistant specializing in converting Exchange Web Services (EWS) SDK code to Microsoft Graph SDK v5+ code in C#.

Rules:
- Output ONLY the replacement C# code, no explanations or markdown
- Use Microsoft.Graph SDK v5+ idioms (fluent API with GetAsync/PostAsync/PatchAsync/DeleteAsync)
- Preserve variable names where possible
- Use async/await patterns
- Include required using statements as a separate block prefixed with [USINGS]
- Include the replacement code prefixed with [CODE]

Output format:
[USINGS]
using Microsoft.Graph;
using Microsoft.Graph.Models;

[CODE]
var messages = await graphClient.Me.Messages.GetAsync();";
    }

    private static string BuildUserPrompt(string code, EwsMigrationRoadmap roadmap, string? surroundingMethod)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Convert this EWS operation to Microsoft Graph SDK:");
        sb.AppendLine();
        sb.AppendLine($"EWS Operation: {roadmap.Title}");
        sb.AppendLine($"EWS SDK: {roadmap.EwsSdkQualifiedName}");
        sb.AppendLine($"Graph Equivalent: {roadmap.GraphApiDisplayName} — {roadmap.GraphApiHttpRequest}");

        if (!string.IsNullOrWhiteSpace(roadmap.GraphApiDocumentationUrl))
            sb.AppendLine($"Graph Docs: {roadmap.GraphApiDocumentationUrl}");

        if (!string.IsNullOrWhiteSpace(roadmap.CopilotPromptTemplate))
        {
            sb.AppendLine();
            sb.AppendLine($"Migration guidance: {roadmap.CopilotPromptTemplate}");
        }

        sb.AppendLine();
        sb.AppendLine("EWS Code to convert:");
        sb.AppendLine("```csharp");
        sb.AppendLine(code.Trim());
        sb.AppendLine("```");

        if (!string.IsNullOrWhiteSpace(surroundingMethod))
        {
            sb.AppendLine();
            sb.AppendLine("Surrounding method context:");
            sb.AppendLine("```csharp");
            sb.AppendLine(surroundingMethod.Trim());
            sb.AppendLine("```");
        }

        return sb.ToString();
    }

    private static string BuildRetryPrompt(ConversionResult previousResult, EwsMigrationRoadmap? roadmap)
    {
        var sb = new StringBuilder();
        sb.AppendLine("The previous conversion attempt had compilation errors. Please fix them.");
        sb.AppendLine();
        sb.AppendLine("Previous output:");
        sb.AppendLine("```csharp");
        sb.AppendLine(previousResult.ConvertedCode);
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("Compilation errors:");
        foreach (var err in previousResult.ValidationErrors)
        {
            sb.AppendLine($"- {err}");
        }
        sb.AppendLine();
        sb.AppendLine("Original EWS code:");
        sb.AppendLine("```csharp");
        sb.AppendLine(previousResult.OriginalCode);
        sb.AppendLine("```");
        sb.AppendLine();
        var graphName = roadmap?.GraphApiDisplayName ?? "Graph API equivalent";
        var graphHttp = roadmap?.GraphApiHttpRequest ?? "";
        sb.AppendLine($"Target: {graphName}{(string.IsNullOrEmpty(graphHttp) ? "" : $" — {graphHttp}")}");
        sb.AppendLine();
        sb.AppendLine("Please output the corrected code in the same [USINGS] / [CODE] format.");

        return sb.ToString();
    }

    /// <summary>
    /// Parse the LLM response to extract usings and code blocks.
    /// </summary>
    internal static (string code, string? usings) ParseLlmResponse(string response)
    {
        string? usings = null;
        string code = response;

        // Try [USINGS] / [CODE] format
        var usingsMatch = Regex.Match(response, @"\[USINGS\]\s*\n(.*?)(?=\[CODE\])", RegexOptions.Singleline);
        var codeMatch = Regex.Match(response, @"\[CODE\]\s*\n(.*?)$", RegexOptions.Singleline);

        if (usingsMatch.Success && codeMatch.Success)
        {
            usings = usingsMatch.Groups[1].Value.Trim();
            code = codeMatch.Groups[1].Value.Trim();
        }
        else
        {
            // Try fenced code blocks
            var fencedUsings = Regex.Match(response, @"```usings\s*\n(.*?)```", RegexOptions.Singleline);
            var fencedCode = Regex.Match(response, @"```csharp\s*\n(.*?)```", RegexOptions.Singleline);

            if (fencedUsings.Success)
                usings = fencedUsings.Groups[1].Value.Trim();
            if (fencedCode.Success)
                code = fencedCode.Groups[1].Value.Trim();
        }

        // Strip any remaining markdown fences
        code = Regex.Replace(code, @"^```\w*\s*\n?", "", RegexOptions.Multiline);
        code = Regex.Replace(code, @"\n?```\s*$", "", RegexOptions.Multiline);

        return (code, usings);
    }
}
