using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Ews.Analyzer;
using Ews.Analyzer.McpService;

using System.Runtime.CompilerServices;
[assembly: InternalsVisibleTo("Ews.Analyzer.McpService.Tests")]

internal static class Program
{
    private static readonly JsonSerializerOptions jsonOpts = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    private static readonly ToolDispatcher dispatcher = new ToolDispatcher();
    private static readonly ConcurrentDictionary<string, CancellationTokenSource> _requestCts = new();
    private static readonly object _writeLock = new object();

    static async Task Main()
    {
        // Ensure asynchronous context (addresses potential CS1998 on some builds)
        await Task.Yield();
        string? line;
        while ((line = Console.ReadLine()) != null)
        {
            JsonElement root;
            JsonDocument? doc = null;
            object id = null!;
            try
            {
                doc = JsonDocument.Parse(line);
                root = doc.RootElement;
                id = root.TryGetProperty("id", out var idElement) ? idElement.ValueKind switch
                {
                    JsonValueKind.Number => idElement.GetInt32(),
                    JsonValueKind.String => idElement.GetString()!,
                    _ => null!
                } : null!;
                if (id == null) id = Guid.NewGuid().ToString();

                var method = root.GetProperty("method").GetString();
                if (method == "cancel")
                {
                    var cancelId = root.GetProperty("params").GetProperty("id").ToString();
                    if (_requestCts.TryRemove(cancelId, out var cts))
                    {
                        cts.Cancel();
                        WriteResponse(id, new { cancelled = cancelId });
                    }
                    else
                    {
                        WriteError(id, -32801, $"No in-flight request with id {cancelId}");
                    }
                    continue;
                }

                // Long running operations run in background to allow cancellation.
                _ = Task.Run(async () =>
                {
                    CancellationTokenSource? cts = null;
                    try
                    {
                        object responseObj;
                        switch (method)
                        {
                            case "initialize":
                                var asm = typeof(Program).Assembly.GetName();
                                responseObj = new
                                {
                                    protocolVersion = "2024-11-05", // MCP protocol date (placeholder/version tag)
                                    serverInfo = new { name = "EwsMigrationAnalyzer", version = asm.Version?.ToString() ?? "1.0.0" },
                                    capabilities = new {
                                        tools = true,
                                        resources = true,
                                        prompts = true,
                                        logging = true,
                                        cancellation = true
                                    }
                                };
                                break;
                            case "tools/list":
                                responseObj = new { tools = dispatcher.ListTools() };
                                break;
                            case "tools/call":
                                var p = root.GetProperty("params");
                                var toolName = p.GetProperty("name").GetString()!;
                                var args = p.GetProperty("arguments");
                                cts = new CancellationTokenSource();
                                var key = id?.ToString() ?? Guid.NewGuid().ToString();
                                _requestCts[key] = cts;
                                responseObj = await dispatcher.CallToolAsync(toolName, args, cts.Token);
                                _requestCts.TryRemove(key, out _);
                                break;
                            case "resources/list":
                                responseObj = new { resources = dispatcher.ListResources() };
                                break;
                            case "resources/read":
                                var rp = root.GetProperty("params");
                                responseObj = dispatcher.ReadResource(rp.GetProperty("uri").GetString()!);
                                break;
                            case "prompts/list":
                                responseObj = new { prompts = dispatcher.ListPrompts() };
                                break;
                            case "prompts/get":
                                var pp = root.GetProperty("params");
                                responseObj = dispatcher.GetPrompt(pp.GetProperty("name").GetString()!, pp.GetProperty("arguments"));
                                break;
                            case "shutdown":
                                WriteResponse(id, new { });
                                Environment.Exit(0);
                                return;
                            default:
                                WriteError(id, -32601, $"Unknown method {method}");
                                return;
                        }
                        WriteResponse(id, responseObj);
                    }
                    catch (OperationCanceledException)
                    {
                        WriteError(id, -32800, "Request cancelled");
                    }
                    catch (Exception ex)
                    {
                        WriteError(id, -32000, ex.Message, new { stack = ex.StackTrace });
                    }
                });
            }
            catch (Exception ex)
            {
                WriteError(id, -32000, ex.Message, new { stack = ex.StackTrace });
            }
            finally
            {
                doc?.Dispose();
            }
        }
    }

    private static void WriteResponse(object id, object result)
    {
        var envelope = new { jsonrpc = "2.0", id, result };
        lock(_writeLock)
        {
            Console.WriteLine(JsonSerializer.Serialize(envelope, jsonOpts));
        }
    }

    private static void WriteError(object id, int code, string message, object? data = null)
    {
        var envelope = new { jsonrpc = "2.0", id, error = new { code, message, data } };
        lock(_writeLock)
        {
            Console.WriteLine(JsonSerializer.Serialize(envelope, jsonOpts));
        }
    }
}

internal sealed class ToolDispatcher
{
    private readonly AnalysisService _analysis = new AnalysisService();
    private readonly EwsMigrationNavigator _navigator = new EwsMigrationNavigator();
    private readonly PathSecurity _paths = new PathSecurity();
    private readonly Lazy<ConversionOrchestrator> _orchestrator;
    private bool _verbose;

    public ToolDispatcher()
    {
        _orchestrator = new Lazy<ConversionOrchestrator>(() => new ConversionOrchestrator(_analysis, _navigator));
    }

    public IEnumerable<object> ListTools()
    {
        return new object[]
        {
            Tool("analyzeCode","Analyze one or more sources for EWS usage (preferred)", new Dictionary<string,object>{
                {"sources", new { type="array", description="Array of sources", items = new { type="object", properties = new { path = new { type="string"}, code = new { type="string"}}, required = Array.Empty<string>() }}},
                {"references", new { type="array", description="Optional reference assembly paths", items = new { type="string"}}}
            }, new[]{"sources"}),
            Tool("analyzeSnippet","(Deprecated) Analyze a C# code snippet for EWS usage", Req("code","string","C# source code")),
            Tool("analyzeFile","(Deprecated) Analyze a C# file on disk", Req("path","string","Absolute or relative file path")),
            Tool("analyzeProject","(Deprecated) Analyze all C# files under a root path", new Dictionary<string,object>{
                {"rootPath", new { type="string", description="Root directory" }},
                {"maxFiles", new { type="integer", description="Max files to scan (default 500)" }},
                {"includeUsages", new { type="boolean", description="Include detailed usage list" }}
            }, new[]{"rootPath"}),
            Tool("listEwsUsages","List EWS SDK invocation usages", new Dictionary<string,object>{
                {"code", new { type="string", description="Inline code snippet (mutually exclusive)" }},
                {"path", new { type="string", description="Single file path" }},
                {"rootPath", new { type="string", description="Directory root" }},
            }),
            Tool("getRoadmap","Get migration roadmap for an EWS SOAP or SDK operation", new Dictionary<string,object>{
                {"ewsOperation", new { type="string", description="EWS SOAP operation"}},
                {"sdkQualifiedName", new { type="string", description="EWS SDK qualified name"}},
            }),
            Tool("generateGraphPrompt","Generate a prompt to migrate one EWS usage to Graph", new Dictionary<string,object>{
                {"sdkQualifiedName", new { type="string", description="EWS SDK qualified name"}},
                {"surroundingCode", new { type="string", description="Optional surrounding code context"}},
                {"goal", new { type="string", description="User migration intent"}},
            }, new[]{"sdkQualifiedName"}),
            Tool("setLogging","Enable or disable verbose logging", new Dictionary<string,object>{
                {"verbose", new { type="boolean", description="Verbose logging on/off"}}
            }, new[]{"verbose"}),
            Tool("suggestGraphFixes","Generate unified diff hunks with TODO comments for EWS usages having Graph parity", new Dictionary<string,object>{
                {"rootPath", new { type="string", description="Project root (directory)"}},
                {"path", new { type="string", description="Single file path"}},
                {"maxFiles", new { type="integer", description="Max files to scan (default 200)"}}
            }),
            Tool("getMigrationReadiness","Compute migration readiness score for a project", new Dictionary<string,object>{
                {"rootPath", new { type="string", description="Project root directory"}},
                {"maxFiles", new { type="integer", description="Max files (default 500)"}}
            }, new[]{"rootPath"}),
            Tool("addAllowedPath","Add an allowed base path for analysis", new Dictionary<string,object>{{"path", new { type="string", description="Directory to allow"}}}, new[]{"path"}),
            Tool("listAllowedPaths","List configured allowed base paths", new Dictionary<string,object>{ }),
            Tool("convertToGraph","Automatically convert EWS code to Microsoft Graph SDK code using a hybrid 3-tier approach (deterministic, template-LLM, full-context-LLM)", new Dictionary<string,object>{
                {"code", new { type="string", description="Inline C# code snippet to convert"}},
                {"path", new { type="string", description="Single file path to convert"}},
                {"rootPath", new { type="string", description="Project root to convert all files"}},
                {"tier", new { type="integer", description="Force a specific tier (1=deterministic, 2=template-LLM, 3=full-context-LLM). Default: auto"}},
                {"dryRun", new { type="boolean", description="If true, return diffs without applying (default true)"}},
                {"maxFiles", new { type="integer", description="Max files for project scan (default 200)"}}
            }),
            Tool("applyConversion","Apply a previously generated conversion diff to source files", new Dictionary<string,object>{
                {"conversions", new { type="array", description="Array of {file, diff} objects from convertToGraph output", items = new { type="object" }}},
                {"backup", new { type="boolean", description="Create .bak files before applying (default true)"}}
            }, new[]{"conversions"}),
            Tool("convertAuth","Convert EWS ExchangeService authentication setup to GraphServiceClient", new Dictionary<string,object>{
                {"code", new { type="string", description="Code containing ExchangeService initialization"}},
                {"authMethod", new { type="string", description="Target auth method: clientCredential, interactive, deviceCode, managedIdentity (default clientCredential)"}}
            }, new[]{"code"})
        };
    }

    private object Tool(string name,string description, object properties, string[]? required = null)
    {
        return new
        {
            name,
            description,
            inputSchema = new
            {
                type = "object",
                properties,
                required = required ?? Array.Empty<string>()
            }
        };
    }

    private object Tool(string name,string description, object singleReq)
    {
        var prop = singleReq.GetType().GetProperty("name") ?? throw new InvalidOperationException("singleReq missing name property");
        var propName = (string)prop.GetValue(singleReq)!;
        return Tool(name, description, new Dictionary<string,object>{{ propName, singleReq }}, new[]{ propName });
    }

    private object Req(string name,string type,string description)
        => new { name, type, description };

    public async Task<object> CallToolAsync(string name, JsonElement args, CancellationToken ct = default)
    {
        return name switch
        {
            "analyzeCode" => await AnalyzeCodeUnifiedAsync(args, ct),
            "analyzeSnippet" => WrapJson(await _analysis.AnalyzeSnippetAsync(args.GetProperty("code").GetString() ?? string.Empty, null, ct)),
            "analyzeFile" => await AnalyzeFileAsync(args, ct),
            "analyzeProject" => await AnalyzeProjectAsync(args, ct),
            "listEwsUsages" => WrapJson(await ListUsagesAsync(args, ct)),
            "getRoadmap" => WrapJson(GetRoadmap(args)),
            "generateGraphPrompt" => WrapText(GeneratePrompt(args)),
            "setLogging" => SetLogging(args),
            "suggestGraphFixes" => await SuggestGraphFixesAsync(args, ct),
            "getMigrationReadiness" => await GetMigrationReadinessAsync(args, ct),
            "addAllowedPath" => AddAllowedPath(args),
            "listAllowedPaths" => ListAllowedPaths(),
            "convertToGraph" => await ConvertToGraphAsync(args, ct),
            "applyConversion" => await ApplyConversionAsync(args, ct),
            "convertAuth" => ConvertAuth(args),
            _ => WrapText($"Unknown tool {name}")
        };
    }

    private object WrapJson(object o) => new { content = new object[]{ new { type="json", data = o } } };
    private object WrapText(string t) => new { content = new object[]{ new { type="text", text = t } } };

    private object GetRoadmap(JsonElement args)
    {
    EwsMigrationRoadmap rm;
    if (args.TryGetProperty("ewsOperation", out var op) && op.GetString() is string o && !string.IsNullOrWhiteSpace(o))
            rm = _navigator.GetMapByEwsOperation(o);
        else if (args.TryGetProperty("sdkQualifiedName", out var q) && q.GetString() is string qv && !string.IsNullOrWhiteSpace(qv))
            rm = _navigator.GetMapByEwsSdkQualifiedName(qv);
        else
            rm = _navigator.GetMapByEwsOperation("Default");

        return new { items = new[]{ new {
            title = rm.Title,
            ewsSoapOperation = rm.EwsSoapOperation,
            functionalArea = rm.FunctionalArea,
            ewsSdkMethodName = rm.EwsSdkMethodName,
            ewsSdkQualifiedName = rm.EwsSdkQualifiedName,
            ewsDocsUrl = rm.EWSDocumentationUrl,
            graphDocsUrl = rm.GraphApiDocumentationUrl,
            graphDisplayName = rm.GraphApiDisplayName,
            graphHttp = rm.GraphApiHttpRequest,
            graphStatus = rm.GraphApiStatus,
            graphEta = rm.GraphApiEta,
            graphHasParity = rm.GraphApiHasParity,
            graphGapPlan = rm.GraphApiGapFillPlan,
            copilotPromptTemplate = rm.CopilotPromptTemplate
        }} };
    }

    private async Task<object> AnalyzeFileAsync(JsonElement args, CancellationToken ct)
    {
        var path = args.GetProperty("path").GetString() ?? string.Empty;
    if(!_paths.IsPathAllowed(path)) return WrapText("Path not allowed");
        var code = File.ReadAllText(path);
        var result = await _analysis.AnalyzeSnippetAsync(code, path, ct);
        return WrapJson(result);
    }

    private async Task<object> AnalyzeProjectAsync(JsonElement args, CancellationToken ct)
    {
        var root = args.GetProperty("rootPath").GetString() ?? string.Empty;
    if(!_paths.IsPathAllowed(root)) return WrapText("Root path not allowed");
        var maxFiles = args.TryGetProperty("maxFiles", out var mf) && mf.ValueKind==JsonValueKind.Number ? mf.GetInt32():500;
        var includeUsages = args.TryGetProperty("includeUsages", out var iu) && iu.ValueKind==JsonValueKind.True;
        var csFiles = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories).Take(maxFiles).ToList();
        var analyses = new List<SnippetAnalysisResult>();
        int processed = 0;
        foreach (var f in csFiles)
        {
            ct.ThrowIfCancellationRequested();
            var code = File.ReadAllText(f);
            analyses.Add(await _analysis.AnalyzeSnippetAsync(code, f, ct));
            processed++;
            if (processed % 25 == 0 || processed == csFiles.Count)
            {
                if (_verbose)
                {
                    ProgramExtensions.WriteNotification("events/partialResult", new { stage = "analyzeProject", processed, total = csFiles.Count });
                }
            }
        }
        var flatDiagnostics = analyses.SelectMany(a=>a.Diagnostics.Select(d=> d));
        var summary = new
        {
            filesScanned = analyses.Count,
            totalDiagnostics = flatDiagnostics.Count(),
            ewsUsages = flatDiagnostics.Count(d=> d.EwsUsage != null),
        };
        return WrapJson(new { summary, files = analyses });
    }

    private async Task<IEnumerable<object>> ListUsagesAsync(JsonElement args, CancellationToken ct)
    {
        if (args.TryGetProperty("code", out var codeEl) && codeEl.ValueKind == JsonValueKind.String)
        {
            var res = await _analysis.AnalyzeSnippetAsync(codeEl.GetString()!, null, ct);
            return res.Diagnostics.Where(d=> d.EwsUsage!=null).Select(d=> (object)d.EwsUsage!);
        }
        if (args.TryGetProperty("path", out var pathEl) && pathEl.ValueKind == JsonValueKind.String)
        {
            var code = File.ReadAllText(pathEl.GetString()!);
            var res = await _analysis.AnalyzeSnippetAsync(code, pathEl.GetString(), ct);
            return res.Diagnostics.Where(d=> d.EwsUsage!=null).Select(d=> (object)d.EwsUsage!);
        }
        if (args.TryGetProperty("rootPath", out var rootEl) && rootEl.ValueKind == JsonValueKind.String)
        {
            var root = rootEl.GetString()!;
            var usages = new List<object>();
            foreach (var f in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories).Take(500))
            {
                if(!_paths.IsPathAllowed(f)) continue;
                ct.ThrowIfCancellationRequested();
                var code = File.ReadAllText(f);
                var res = await _analysis.AnalyzeSnippetAsync(code, f, ct);
                usages.AddRange(res.Diagnostics.Where(d=> d.EwsUsage!=null).Select(d=> (object)d.EwsUsage!));
            }
            return usages;
        }
        return Array.Empty<object>();
    }

    private string GeneratePrompt(JsonElement args)
    {
        var qn = args.GetProperty("sdkQualifiedName").GetString() ?? string.Empty;
        var code = args.TryGetProperty("surroundingCode", out var sc) ? sc.GetString() : null;
        var goal = args.TryGetProperty("goal", out var g) ? g.GetString() : null;
        var roadmap = _navigator.GetMapByEwsSdkQualifiedName(qn);
        var sb = new StringBuilder();
        sb.AppendLine("You are assisting migration from EWS to Microsoft Graph.");
        sb.AppendLine($"EWS SDK Qualified Name: {qn}");
        sb.AppendLine($"Graph Status: {roadmap.GraphApiStatus}; Parity: {roadmap.GraphApiHasParity}");
        if (!string.IsNullOrWhiteSpace(roadmap.GraphApiDisplayName)) sb.AppendLine($"Graph API: {roadmap.GraphApiDisplayName}");
        if (!string.IsNullOrWhiteSpace(roadmap.GraphApiHttpRequest)) sb.AppendLine($"HTTP: {roadmap.GraphApiHttpRequest}");
        if (!string.IsNullOrWhiteSpace(goal)) sb.AppendLine($"Goal: {goal}");
        if (!string.IsNullOrWhiteSpace(code))
        {
            sb.AppendLine("Relevant Source:\n" + code.Trim());
        }
        sb.AppendLine("Provide idiomatic C# Graph SDK replacement code, add brief explanation, and note any gaps.");
        return sb.ToString();
    }

    private async Task<object> AnalyzeCodeUnifiedAsync(JsonElement args, CancellationToken ct)
    {
        var findings = new List<object>();
        if (!args.TryGetProperty("sources", out var sourcesEl) || sourcesEl.ValueKind != JsonValueKind.Array)
        {
            return WrapJson(new { findings });
        }
        foreach (var src in sourcesEl.EnumerateArray())
        {
            ct.ThrowIfCancellationRequested();
            string? path = null; string? code = null;
            if (src.TryGetProperty("path", out var p) && p.ValueKind == JsonValueKind.String)
            {
                path = p.GetString();
            }
            if (src.TryGetProperty("code", out var c) && c.ValueKind == JsonValueKind.String)
            {
                code = c.GetString();
            }
            if (path != null && code == null)
            {
                if(!_paths.IsPathAllowed(path)) continue;
                if (File.Exists(path)) code = await File.ReadAllTextAsync(path, ct); else continue;
            }
            if (code == null) continue;
            var result = await _analysis.AnalyzeSnippetAsync(code, path, ct);
            foreach (var d in result.Diagnostics)
            {
                var line = d.Line;
                var col = d.Column;
                string ruleId = d.Id;
                string title = d.Id; // fallback (analyzer descriptors not surfaced here)
                string? ewsSymbol = null; string? graphAlt=null; string? docsUrl=null;
                if (d.EwsUsage != null)
                {
                    try
                    {
                        var usageJson = JsonSerializer.Serialize(d.EwsUsage);
                        using var usageDoc = JsonDocument.Parse(usageJson);
                        if (usageDoc.RootElement.TryGetProperty("sdkQualifiedName", out var sk)) ewsSymbol = sk.GetString();
                        if (usageDoc.RootElement.TryGetProperty("graph", out var ge))
                        {
                            // Adjusted property names after simplifying serialization (no 'roadmap.' prefix expected)
                            if (ge.TryGetProperty("GraphApiDisplayName", out var ga) && ga.ValueKind==JsonValueKind.String) graphAlt = ga.GetString();
                            if (ge.TryGetProperty("GraphApiDocumentationUrl", out var du) && du.ValueKind==JsonValueKind.String) docsUrl = du.GetString();
                        }
                    }
                    catch { }
                }
                findings.Add(new {
                    file = path,
                    line,
                    col,
                    ruleId,
                    title,
                    severity = d.Severity,
                    message = d.Message,
                    ewsSymbol,
                    graphAlt,
                    docsUrl
                });
            }
        }
        return WrapJson(new { findings });
    }

    private object SetLogging(JsonElement args)
    {
        _verbose = args.GetProperty("verbose").GetBoolean();
        return WrapText($"Verbose logging set to {_verbose}");
    }

    private object AddAllowedPath(JsonElement args)
    {
        var path = args.GetProperty("path").GetString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(path)) return WrapText("Invalid path");
        _paths.Add(path);
        return WrapText($"Added allowed path: {Path.GetFullPath(path)}");
    }

    private object ListAllowedPaths()
    {
        return WrapJson(new { allowlist = _paths.Allowed });
    }

    private async Task<object> SuggestGraphFixesAsync(JsonElement args, CancellationToken ct)
    {
        // Determine scope
        var diffs = new List<string>();
        if (args.TryGetProperty("path", out var pathEl) && pathEl.ValueKind == JsonValueKind.String)
        {
            var path = pathEl.GetString()!;
            if(!_paths.IsPathAllowed(path)) return WrapText("Path not allowed");
            diffs.AddRange(await BuildDiffsForFileAsync(path, ct));
        }
        else if (args.TryGetProperty("rootPath", out var rootEl) && rootEl.ValueKind == JsonValueKind.String)
        {
            var root = rootEl.GetString()!;
            if(!_paths.IsPathAllowed(root)) return WrapText("Root path not allowed");
            var maxFiles = args.TryGetProperty("maxFiles", out var mf) && mf.ValueKind==JsonValueKind.Number ? mf.GetInt32():200;
            var files = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories).Take(maxFiles);
            foreach (var f in files)
            {
                ct.ThrowIfCancellationRequested();
                diffs.AddRange(await BuildDiffsForFileAsync(f, ct));
            }
        }
        else
        {
            return WrapText("Provide either path or rootPath");
        }
        var unified = string.Join("\n", diffs);
        return WrapJson(new { diff = unified });
    }

    private async Task<IEnumerable<string>> BuildDiffsForFileAsync(string path, CancellationToken ct)
    {
        var code = await File.ReadAllTextAsync(path, ct);
        var analysis = await _analysis.AnalyzeSnippetAsync(code, path, ct);
        var lines = code.Split('\n');
    // Simplified: treat diagnostics with ID EWS001 as actionable for TODO insertion.
    var fixTargets = analysis.Diagnostics.Where(d=> d.Id == "EWS001").ToList();
        if (!fixTargets.Any()) return Array.Empty<string>();
        var relative = Path.GetRelativePath(Directory.GetCurrentDirectory(), path);
        var sb = new StringBuilder();
        foreach (var diag in fixTargets.GroupBy(d=> d.Line))
        {
            var lineIndex = diag.Key -1;
            if (lineIndex <0 || lineIndex >= lines.Length) continue;
            var original = lines[lineIndex];
            // Build hunk
            sb.AppendLine($"--- a/{relative}");
            sb.AppendLine($"+++ b/{relative}");
            sb.AppendLine($"@@ -{diag.Key},1 +{diag.Key},2 @@");
            sb.AppendLine($"+// TODO: Replace EWS usage at line {diag.Key} with Microsoft Graph SDK equivalent (see mappings). This comment auto-generated.");
            sb.AppendLine($" {original}");
        }
    if (sb.Length == 0) return Array.Empty<string>();
    return new string[]{ sb.ToString() };
    }

    private async Task<object> GetMigrationReadinessAsync(JsonElement args, CancellationToken ct)
    {
        var root = args.GetProperty("rootPath").GetString() ?? string.Empty;
        if(!_paths.IsPathAllowed(root)) return WrapText("Root path not allowed");
        var maxFiles = args.TryGetProperty("maxFiles", out var mf) && mf.ValueKind==JsonValueKind.Number ? mf.GetInt32():500;
        var files = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories).Take(maxFiles).ToList();
        int available=0, preview=0, unavailable=0, total=0;
        foreach (var f in files)
        {
            ct.ThrowIfCancellationRequested();
            var code = await File.ReadAllTextAsync(f, ct);
            var result = await _analysis.AnalyzeSnippetAsync(code, f, ct);
            foreach (var d in result.Diagnostics)
            {
                if (!d.Id.StartsWith("EWS")) continue;
                if (d.Id == "EWS001") { available++; total++; }
                else if (d.Id == "EWS002") { preview++; total++; }
                else if (d.Id == "EWS003" || d.Id == "EWS000") { unavailable++; total++; }
            }
        }
        var parity = available + preview;
        var readiness = total == 0 ? 0 : (double)parity / total * 100d;
        return WrapJson(new { totalReferences = total, available, preview, unavailable, readinessPercent = readiness });
    }

    // ─── Conversion Tools ─────────────────────────────────────────────

    private async Task<object> ConvertToGraphAsync(JsonElement args, CancellationToken ct)
    {
        var orchestrator = _orchestrator.Value;
        int? forceTier = args.TryGetProperty("tier", out var tierEl) && tierEl.ValueKind == JsonValueKind.Number
            ? tierEl.GetInt32() : null;

        // Inline code snippet
        if (args.TryGetProperty("code", out var codeEl) && codeEl.ValueKind == JsonValueKind.String)
        {
            var result = await orchestrator.ConvertSnippetAsync(codeEl.GetString()!, forceTier, ct);
            return WrapJson(new
            {
                conversions = new[] { FormatConversionResult(result) },
                summary = new { totalUsages = 1, converted = result.IsValid ? 1 : 0, highConfidence = result.Confidence == "high" ? 1 : 0, mediumConfidence = result.Confidence == "medium" ? 1 : 0, lowConfidence = result.Confidence == "low" ? 1 : 0, failed = result.IsValid ? 0 : 1 }
            });
        }

        // Single file
        if (args.TryGetProperty("path", out var pathEl) && pathEl.ValueKind == JsonValueKind.String)
        {
            var path = pathEl.GetString()!;
            if (!_paths.IsPathAllowed(path)) return WrapText("Path not allowed");
            var results = await orchestrator.ConvertFileAsync(path, forceTier, ct);
            return WrapJson(new
            {
                conversions = results.Select(FormatConversionResult),
                summary = new
                {
                    totalUsages = results.Count,
                    converted = results.Count(r => r.IsValid),
                    highConfidence = results.Count(r => r.Confidence == "high"),
                    mediumConfidence = results.Count(r => r.Confidence == "medium"),
                    lowConfidence = results.Count(r => r.Confidence == "low"),
                    failed = results.Count(r => !r.IsValid)
                }
            });
        }

        // Project root
        if (args.TryGetProperty("rootPath", out var rootEl) && rootEl.ValueKind == JsonValueKind.String)
        {
            var root = rootEl.GetString()!;
            if (!_paths.IsPathAllowed(root)) return WrapText("Root path not allowed");
            var maxFiles = args.TryGetProperty("maxFiles", out var mf) && mf.ValueKind == JsonValueKind.Number ? mf.GetInt32() : 200;
            var projectResult = await orchestrator.ConvertProjectAsync(root, maxFiles, forceTier, ct);
            return WrapJson(new
            {
                conversions = projectResult.Conversions.Select(FormatConversionResult),
                summary = projectResult.Summary
            });
        }

        return WrapText("Provide one of: code, path, or rootPath");
    }

    private static object FormatConversionResult(ConversionResult r)
    {
        return new
        {
            file = r.FilePath,
            tier = r.Tier,
            confidence = r.Confidence,
            ewsQualifiedName = r.EwsQualifiedName,
            graphApiName = r.GraphApiName,
            originalCode = r.OriginalCode,
            convertedCode = r.ConvertedCode,
            requiredUsings = r.RequiredUsings,
            requiredPackage = r.RequiredPackage,
            diff = r.UnifiedDiff,
            isValid = r.IsValid,
            validationErrors = r.ValidationErrors.Any() ? r.ValidationErrors : null
        };
    }

    private async Task<object> ApplyConversionAsync(JsonElement args, CancellationToken ct)
    {
        var backup = !args.TryGetProperty("backup", out var backupEl) || backupEl.ValueKind != JsonValueKind.False;

        if (!args.TryGetProperty("conversions", out var conversionsEl) || conversionsEl.ValueKind != JsonValueKind.Array)
            return WrapText("conversions array is required");

        var applied = new List<string>();
        var errors = new List<string>();

        foreach (var conv in conversionsEl.EnumerateArray())
        {
            ct.ThrowIfCancellationRequested();
            string? file = null;
            string? convertedCode = null;

            if (conv.TryGetProperty("file", out var fEl) && fEl.ValueKind == JsonValueKind.String)
                file = fEl.GetString();
            if (conv.TryGetProperty("convertedCode", out var cEl) && cEl.ValueKind == JsonValueKind.String)
                convertedCode = cEl.GetString();

            if (string.IsNullOrWhiteSpace(file) || string.IsNullOrWhiteSpace(convertedCode))
            {
                errors.Add("Skipped entry: missing file or convertedCode");
                continue;
            }

            if (!_paths.IsPathAllowed(file))
            {
                errors.Add($"Path not allowed: {file}");
                continue;
            }

            if (!File.Exists(file))
            {
                errors.Add($"File not found: {file}");
                continue;
            }

            if (backup)
            {
                File.Copy(file, file + ".bak", overwrite: true);
            }

            await File.WriteAllTextAsync(file, convertedCode, ct);
            applied.Add(file);
        }

        return WrapJson(new { applied, errors, backupCreated = backup });
    }

    private object ConvertAuth(JsonElement args)
    {
        var code = args.GetProperty("code").GetString() ?? string.Empty;
        var authMethod = args.TryGetProperty("authMethod", out var am) && am.ValueKind == JsonValueKind.String
            ? am.GetString() ?? "clientCredential"
            : "clientCredential";

        var result = DeterministicTransformer.TransformAuth(code, authMethod);
        if (result == null)
            return WrapText("No ExchangeService or WebCredentials patterns found in the provided code.");

        return WrapJson(FormatConversionResult(result));
    }

    // Resources (roadmap entries)
    public IEnumerable<object> ListResources()
    {
        return _navigator.Map
            .Where(r => !string.Equals(r.EwsSoapOperation, "Default", StringComparison.OrdinalIgnoreCase))
            .Select(r => new {
                uri = $"roadmap/{r.EwsSoapOperation}",
                name = r.Title,
                mimeType = "application/json"
            });
    }

    public object ReadResource(string uri)
    {
        if (!uri.StartsWith("roadmap/", StringComparison.OrdinalIgnoreCase))
            return WrapText("Resource not found");
        var op = uri.Substring("roadmap/".Length);
        var roadmap = _navigator.GetMapByEwsOperation(op);
        return WrapJson(roadmap);
    }

    // Prompts
    public IEnumerable<object> ListPrompts()
    {
    return new object[]
        {
            new { name = "migrate-ews-usage", description = "General migration assistance for an EWS SDK usage", inputSchema = new { type="object", properties = new { sdkQualifiedName = new { type="string"}, code = new { type="string"}, goal = new { type="string"}}, required = new[]{"sdkQualifiedName"} } },
            new { name = "summarize-project-ews", description = "Summarize EWS usage across project", inputSchema = new { type="object", properties = new { rootPath = new { type="string"}}, required = new[]{"rootPath"} } },
            new { name = "convert-ews-to-graph", description = "Automatically convert EWS code to Microsoft Graph SDK with confidence scoring", inputSchema = new { type="object", properties = new { code = new { type="string", description="EWS code to convert" }, filePath = new { type="string", description="Optional file path" }}, required = new[]{"code"} } },
            new { name = "migrate-auth", description = "Convert EWS ExchangeService authentication to GraphServiceClient", inputSchema = new { type="object", properties = new { code = new { type="string", description="EWS auth code" }, authMethod = new { type="string", description="Target: clientCredential, interactive, deviceCode, managedIdentity" }}, required = new[]{"code"} } }
        };
    }

    public object GetPrompt(string name, JsonElement args)
    {
        switch (name)
        {
            case "migrate-ews-usage":
                var qn = args.GetProperty("sdkQualifiedName").GetString() ?? string.Empty;
                var roadmap = _navigator.GetMapByEwsSdkQualifiedName(qn);
                var code = args.TryGetProperty("code", out var c) ? c.GetString() : null;
                var goal = args.TryGetProperty("goal", out var g) ? g.GetString() : null;
                var prompt = GeneratePrompt(JsonDocument.Parse(JsonSerializer.Serialize(new { sdkQualifiedName = qn, surroundingCode = code, goal})).RootElement);
                return new { name, messages = new[]{ new { role = "user", content = new object[]{ new { type="text", text = prompt } } } } };
            case "summarize-project-ews":
                var root = args.GetProperty("rootPath").GetString() ?? string.Empty;
                var txt = $"Analyze EWS usage under {root} and summarize migration readiness.";
                return new { name, messages = new[]{ new { role="user", content = new object[]{ new { type="text", text = txt } } } } };
            case "convert-ews-to-graph":
                var convertCode = args.GetProperty("code").GetString() ?? string.Empty;
                var convertPrompt = $"Analyze the following EWS code and convert each EWS SDK call to its Microsoft Graph SDK equivalent.\n\nFor each conversion:\n1. Show the original EWS code\n2. Show the Graph SDK replacement with confidence level (high/medium/low)\n3. List any required using statements and NuGet packages\n4. Note any gaps or manual steps needed\n\nCode:\n```csharp\n{convertCode}\n```";
                return new { name, messages = new[]{ new { role = "user", content = new object[]{ new { type="text", text = convertPrompt } } } } };
            case "migrate-auth":
                var authCode = args.GetProperty("code").GetString() ?? string.Empty;
                var authMethodPrompt = args.TryGetProperty("authMethod", out var am2) ? am2.GetString() : "clientCredential";
                var authPrompt = $"Convert the following EWS authentication code (ExchangeService/WebCredentials) to Microsoft Graph SDK authentication using {authMethodPrompt} flow.\n\nShow the complete replacement including:\n- Required NuGet packages (Microsoft.Graph, Azure.Identity)\n- Using statements\n- GraphServiceClient initialization\n\nEWS Auth Code:\n```csharp\n{authCode}\n```";
                return new { name, messages = new[]{ new { role = "user", content = new object[]{ new { type="text", text = authPrompt } } } } };
            default:
                return new { name, messages = Array.Empty<object>() };
        }
    }
}

internal sealed class AnalysisService
{
    private readonly EwsAnalyzer _analyzer = new EwsAnalyzer();
    private readonly EwsMigrationNavigator _navigator = new EwsMigrationNavigator();
    private readonly Dictionary<string, SnippetAnalysisResult> _cache = new();
    private readonly object _cacheLock = new();

    public async Task<SnippetAnalysisResult> AnalyzeSnippetAsync(string code, string? filePath = null, CancellationToken ct = default)
    {
        var cacheKey = CreateKey(code, filePath ?? "<snippet>");
        if (_cache.TryGetValue(cacheKey, out var existing))
        {
            return existing;
        }

        var tree = CSharpSyntaxTree.ParseText(code, cancellationToken: ct);
        var compilation = CSharpCompilation.Create(
            "Analysis",
            new[] { tree },
            new[] {
                        MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                        MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
                        MetadataReference.CreateFromFile(typeof(Uri).Assembly.Location),
                    },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        // Use non-obsolete overload (cooperative cancellation occurs before invocation loop)
        var cwa = compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(_analyzer));
        var diags = await cwa.GetAnalyzerDiagnosticsAsync();
        var list = new List<DiagnosticResultDto>();
        foreach (var d in diags)
        {
            var span = d.Location.GetLineSpan();
            object? usage = null;
            if (d.Id.StartsWith("EWS"))
            {
                // attempt to extract SDK qualified name from message arguments
                var msg = d.GetMessage();
                // Heuristic: first token containing 'Microsoft.Exchange.WebServices'
                var token = msg.Split(new[]{' ', '\n', '\r', '\t', '(', ')', ':'}, StringSplitOptions.RemoveEmptyEntries)
                    .FirstOrDefault(t=> t.StartsWith("Microsoft.Exchange.WebServices"));
                if (token != null)
                {
                    var roadmap = _navigator.GetMapByEwsSdkQualifiedName(token);
                    usage = new {
                        sdkQualifiedName = token,
                        graph = new {
                            roadmap.GraphApiStatus,
                            roadmap.GraphApiEta,
                            roadmap.GraphApiDocumentationUrl,
                            roadmap.GraphApiHasParity,
                            roadmap.GraphApiGapFillPlan,
                            roadmap.GraphApiDisplayName,
                            roadmap.GraphApiHttpRequest
                        }
                    };
                }
            }
            list.Add(new DiagnosticResultDto
            {
                Id = d.Id,
                Message = d.GetMessage(),
                Severity = d.Severity.ToString(),
                Line = span.StartLinePosition.Line + 1,
                Column = span.StartLinePosition.Character + 1,
                File = filePath,
                EwsUsage = usage
            });
        }
        var result = new SnippetAnalysisResult
        {
            File = filePath,
            Diagnostics = list
        };
        lock(_cacheLock)
        {
            _cache[cacheKey] = result;
        }
        return result;
    }

    private static string CreateKey(string code, string id)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(code + "|" + id);
        return Convert.ToHexString(sha.ComputeHash(bytes));
    }
}

internal sealed class SnippetAnalysisResult
{
    public string? File { get; set; }
    public List<DiagnosticResultDto> Diagnostics { get; set; } = new();
}

internal sealed class DiagnosticResultDto
{
    public string Id { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public int Line { get; set; }
    public int Column { get; set; }
    public string? File { get; set; }
    public object? EwsUsage { get; set; }
}

internal sealed class PathSecurity
{
    private readonly string _cwd = Directory.GetCurrentDirectory();
    private readonly List<string> _allow = new();
    public IEnumerable<string> Allowed => _allow.AsReadOnly();

    public PathSecurity()
    {
        // By default allow current directory subtree only
        _allow.Add(_cwd);
    }

    public bool IsPathAllowed(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        try
        {
            var full = Path.GetFullPath(path);
            foreach (var root in _allow)
            {
                if (full.StartsWith(root, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }
        catch { return false; }
    }

    public void Add(string path)
    {
        try
        {
            var full = Path.GetFullPath(path);
            if (!_allow.Any(a=> string.Equals(a, full, StringComparison.OrdinalIgnoreCase)))
            {
                _allow.Add(full);
            }
        }
        catch { }
    }
}

internal static class ProgramExtensions
{
    private static readonly JsonSerializerOptions opts = new JsonSerializerOptions{ PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    public static void WriteNotification(string method, object parameters)
    {
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(parameters, opts));
        var obj = new Dictionary<string, object?>
        {
            ["jsonrpc"] = "2.0",
            ["method"] = method,
            ["params"] = parameters
        };
        Console.WriteLine(JsonSerializer.Serialize(obj, opts));
    }
}
