# Approach 4: Hybrid EWS-to-Graph Automatic Conversion — Implementation Plan

## Overview

Add automatic code conversion capabilities to the EWS Migration Analyzer using a three-tier hybrid strategy: deterministic Roslyn transforms for common patterns, template-guided LLM conversion for moderate complexity, and full-context LLM conversion for complex cases. All tiers feed through a Roslyn compilation validation gate before results are presented.

---

## Architecture

```
                         ┌─────────────────────┐
                         │   convertToGraph     │  (new MCP tool — entry point)
                         │   tool dispatcher    │
                         └─────────┬───────────┘
                                   │
                    ┌──────────────┼──────────────┐
                    ▼              ▼               ▼
            ┌──────────┐   ┌────────────┐   ┌───────────┐
            │  Tier 1   │   │   Tier 2    │   │  Tier 3    │
            │Deterministic│ │Template-LLM │   │FullCtx-LLM│
            │ Roslyn Xfm │ │  Conversion  │   │ Conversion │
            └──────┬───┘   └──────┬──────┘   └─────┬─────┘
                   │              │                  │
                   └──────────────┼──────────────────┘
                                  ▼
                      ┌───────────────────────┐
                      │  Validation Gate       │
                      │  (Roslyn compilation)  │
                      └───────────┬───────────┘
                                  ▼
                      ┌───────────────────────┐
                      │  Confidence Scoring    │
                      │  + Diff Output         │
                      └───────────────────────┘
```

---

## Step 1: Extend the Data Model

### Files to modify:
- `src/Ews.Code.Analyzer/Ews.Analyzer/EwsMigrationRoadmap.cs`
- `src/Ews.Code.Analyzer/Ews.Analyzer/Data/roadmap.json`

### Changes:

**1a. Add new fields to `EwsMigrationRoadmap`:**

```csharp
// New properties in EwsMigrationRoadmap.cs

/// <summary>
/// The conversion tier: 1 = deterministic, 2 = template-LLM, 3 = full-context-LLM
/// </summary>
public int ConversionTier { get; set; } = 2;

/// <summary>
/// EWS SDK code pattern (regex or Roslyn syntax pattern) to match for Tier 1 transforms.
/// Null means this operation is not eligible for Tier 1.
/// </summary>
public string? EwsCodePattern { get; set; }

/// <summary>
/// Graph SDK code template for Tier 1 deterministic replacement.
/// Uses placeholders like {{variable}}, {{folder}}, {{top}} extracted from matched pattern.
/// Null means this operation is not eligible for Tier 1.
/// </summary>
public string? GraphCodeTemplate { get; set; }

/// <summary>
/// Required Graph SDK using statements for the converted code.
/// </summary>
public string? GraphRequiredUsings { get; set; }

/// <summary>
/// Required NuGet package for the Graph SDK call (e.g., "Microsoft.Graph").
/// </summary>
public string? GraphRequiredPackage { get; set; }
```

**1b. Add Tier 1 templates to `roadmap.json` for the ~20 most common GA-parity operations:**

For each GA-parity entry, add `conversionTier`, `ewsCodePattern`, `graphCodeTemplate`, `graphRequiredUsings`. Example for FindItems:

```json
{
  "title": "Find Items",
  "ewsSoapOperation": "FindItem",
  "conversionTier": 1,
  "ewsCodePattern": "{{service}}.FindItems({{folder}}, {{view}})",
  "graphCodeTemplate": "await {{graphClient}}.Me.Messages.GetAsync(config => {\n    config.QueryParameters.Top = {{top}};\n});",
  "graphRequiredUsings": "using Microsoft.Graph;\nusing Microsoft.Graph.Models;",
  "graphRequiredPackage": "Microsoft.Graph",
  ...existing fields...
}
```

Priority Tier 1 operations (GA parity, high frequency, structurally simple):
1. `FindItems` (List messages)
2. `SendEmail` / `EmailMessage.Send` (Send mail)
3. `FindAppointments` (List events)
4. `CreateItem` mail (Create message draft)
5. `GetItem` mail (Get message)
6. `UpdateItem` mail (Update message)
7. `DeleteItem` mail (Delete message)
8. `MoveItem` (Move message)
9. `CopyItem` (Copy message)
10. `CreateItem` event (Create event)
11. `UpdateItem` event (Update event)
12. `DeleteItem` event (Delete event)
13. `GetItem` contact (Get contact)
14. `CreateItem` contact (Create contact)
15. `UpdateItem` contact (Update contact)
16. `DeleteItem` contact (Delete contact)
17. `CreateAttachment` (Add attachment)
18. `DeleteAttachment` (Delete attachment)
19. `GetAttachment` (List attachments)
20. `SyncFolderItems` (Delta messages)

All remaining operations default to `conversionTier: 2` (template-LLM) or `conversionTier: 3` (full-context for Gap/TBD status).

---

## Step 2: Build the Tier 1 Deterministic Transform Engine

### New file:
- `src/Ews.Code.Analyzer/Ews.Analyzer.McpService/DeterministicTransformer.cs`

### Responsibilities:

**2a. Pattern matcher using Roslyn semantic analysis:**
- Input: A `SyntaxNode` (the EWS invocation) + `SemanticModel` + the matched `EwsMigrationRoadmap` entry
- Extract variable names, arguments, and surrounding context from the AST
- Map EWS arguments to Graph SDK equivalents using the `ewsCodePattern` → `graphCodeTemplate` placeholders

**2b. Code generator using Roslyn `SyntaxFactory`:**
- Parse the `graphCodeTemplate` string with placeholders filled in
- Produce a replacement `SyntaxNode`
- Handle `using` statement additions (`graphRequiredUsings`)
- Handle auth pattern: detect `ExchangeService` variable and map to `GraphServiceClient`

**2c. Variable/context extraction rules:**

| EWS Pattern | Extracted Variables | Graph Mapping |
|---|---|---|
| `service.FindItems(folder, view)` | `service`, `folder`, `view.PageSize` | `graphClient.Me.Messages.GetAsync(...)` with `Top = PageSize` |
| `email.Send()` | `email` (EmailMessage) | `graphClient.Me.SendMail.PostAsync(...)` |
| `service.FindAppointments(calId, view)` | `service`, `calId`, `view` | `graphClient.Me.Events.GetAsync(...)` |
| `item.Delete(mode)` | `item`, `mode` | `graphClient.Me.Messages[id].DeleteAsync()` |

**2d. Auth migration helper:**
- Detect `new ExchangeService(...)` + credential setup blocks
- Produce `new GraphServiceClient(tokenCredential)` replacement
- This is a special-case Tier 1 transform that applies once per file/class

### Key design decisions:
- The transformer returns a `ConversionResult` object (see Step 5) — it never writes files directly
- If pattern matching fails (unexpected argument shapes, chained calls, etc.), the transformer returns `null`, signaling fallback to Tier 2

---

## Step 3: Build the Tier 2 Template-Guided LLM Conversion

### New file:
- `src/Ews.Code.Analyzer/Ews.Analyzer.McpService/TemplateGuidedConverter.cs`

### Responsibilities:

**3a. Prompt construction:**
- Use the existing `copilotPromptTemplate` from the roadmap entry as the base
- Enrich with:
  - The specific EWS code snippet (the method or statement containing the invocation)
  - The Graph API HTTP request pattern (`graphApiHttpRequest`)
  - The Graph SDK documentation URL
  - Variable names and types from Roslyn semantic model
  - The surrounding method signature for context
- Structured prompt format:

```
System: You are a code migration assistant. Convert EWS SDK code to Microsoft Graph SDK code.
Rules:
- Output ONLY the replacement C# code, no explanations
- Use Microsoft.Graph SDK v5+ idioms
- Preserve variable names where possible
- Include required using statements as a separate block

EWS Operation: {title}
Graph Equivalent: {graphApiDisplayName} — {graphApiHttpRequest}
Graph Docs: {graphApiDocumentationUrl}

EWS Code:
```csharp
{extracted_method_body}
```

{copilotPromptTemplate}

Output format:
```usings
{required using statements}
```
```csharp
{replacement code}
```
```

**3b. LLM invocation interface:**
- Define an `ILlmClient` interface so the LLM backend is pluggable:

```csharp
internal interface ILlmClient
{
    Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct);
}
```

- Provide two implementations:
  1. `McpRelayLlmClient` — returns the prompt as an MCP `sampling/createMessage` request, letting the MCP host (Copilot/Claude) handle the LLM call. This is the **primary** path since the MCP service already runs inside an LLM-powered host.
  2. `HttpLlmClient` — direct HTTP call to an LLM API (OpenAI, Azure OpenAI, Anthropic) for standalone use. Configured via environment variables (`LLM_API_KEY`, `LLM_ENDPOINT`, `LLM_MODEL`).

**3c. Response parsing:**
- Extract code from fenced code blocks in the LLM response
- Separate `usings` block from `csharp` block
- Strip markdown artifacts
- Return as `ConversionResult`

### Key design decisions:
- Tier 2 operates at the **method level**: it extracts the full method containing the EWS call and asks the LLM to convert the entire method
- If the LLM response fails validation (Step 5), retry once with the compilation error appended to the prompt
- Max 2 LLM round-trips per conversion to bound latency/cost

---

## Step 4: Build the Tier 3 Full-Context LLM Conversion

### New file:
- `src/Ews.Code.Analyzer/Ews.Analyzer.McpService/FullContextConverter.cs`

### Responsibilities:

**4a. Context gathering:**
- Extract the entire class or file containing the EWS usage
- Include referenced types (EWS model classes used as parameters/return types)
- Include the project's existing Graph SDK references (if any) to avoid conflicts
- Cap context at ~8000 tokens to stay within prompt limits

**4b. Enhanced prompt:**
- Same `ILlmClient` interface as Tier 2
- Richer prompt including:
  - Full class source
  - All EWS usages in the class (batch conversion)
  - Auth setup code
  - Any existing Graph SDK code in the project (to match style)
  - Instructions to preserve method signatures for API compatibility

**4c. Use cases for Tier 3:**
- Operations with `graphApiStatus` = "Gap" or "TBD" (no direct equivalent — LLM suggests workarounds)
- Complex patterns: streaming notifications, batch requests, custom extended properties
- Multi-operation methods that mix several EWS calls
- Cases where Tier 2 failed validation twice

### Key design decisions:
- Tier 3 converts at the **class level**, not method level
- For Gap/TBD operations, the output includes `// WARNING: No direct Graph API equivalent` comments with workaround suggestions
- Tier 3 is always flagged as `confidence: low` to signal mandatory human review

---

## Step 5: Validation Gate

### New file:
- `src/Ews.Code.Analyzer/Ews.Analyzer.McpService/ConversionValidator.cs`

### Data model (`ConversionResult`):

```csharp
internal sealed class ConversionResult
{
    public int Tier { get; set; }                    // 1, 2, or 3
    public string Confidence { get; set; }           // "high", "medium", "low"
    public string OriginalCode { get; set; }         // EWS source
    public string ConvertedCode { get; set; }        // Graph replacement
    public string? RequiredUsings { get; set; }      // Using statements to add
    public string? RequiredPackage { get; set; }     // NuGet package needed
    public string? FilePath { get; set; }
    public int StartLine { get; set; }
    public int EndLine { get; set; }
    public bool IsValid { get; set; }                // Passed compilation check
    public List<string> ValidationErrors { get; set; } = new();
    public string? UnifiedDiff { get; set; }         // Ready-to-apply diff
}
```

### Validation steps:

**5a. Syntax check:**
- Parse the converted code with `CSharpSyntaxTree.ParseText()`
- Reject if syntax errors exist

**5b. Compilation check:**
- Create a compilation with the converted code + Graph SDK references
- Add `Microsoft.Graph` metadata references (bundled or from NuGet cache)
- Check for compilation errors (warnings are OK)
- Record errors in `ValidationErrors`

**5c. Semantic sanity check:**
- Verify no `Microsoft.Exchange.WebServices` references remain in the converted code
- Verify `Microsoft.Graph` types are referenced

**5d. Confidence scoring:**

| Condition | Confidence |
|---|---|
| Tier 1 + compiles | `high` |
| Tier 2 + compiles on first try | `high` |
| Tier 2 + compiles on retry | `medium` |
| Tier 3 + compiles | `medium` |
| Tier 3 + Gap/TBD operation | `low` |
| Any tier + fails compilation | `low` (returned with errors for human review) |

**5e. Diff generation:**
- Produce unified diff format from original → converted code
- Include file path, line numbers, context lines
- Reuse/improve the existing `BuildDiffsForFileAsync` logic from `SuggestGraphFixesAsync`

---

## Step 6: Conversion Orchestrator

### New file:
- `src/Ews.Code.Analyzer/Ews.Analyzer.McpService/ConversionOrchestrator.cs`

### Responsibilities:

**6a. Per-usage conversion flow:**

```
For each EWS usage detected by AnalysisService:
  1. Look up roadmap entry via EwsMigrationNavigator
  2. Check roadmap.conversionTier
  3. If Tier 1 eligible:
       → DeterministicTransformer.Transform(...)
       → If result != null → validate → return
       → If null → fall through to Tier 2
  4. If Tier 2 eligible (or Tier 1 fell through):
       → TemplateGuidedConverter.ConvertAsync(...)
       → Validate
       → If valid → return
       → If invalid → retry once with error context
       → If still invalid → fall through to Tier 3
  5. Tier 3:
       → FullContextConverter.ConvertAsync(...)
       → Validate
       → Return (even if invalid, with errors attached)
```

**6b. File-level orchestration:**
- Group all EWS usages in a file
- Convert bottom-to-top (so line numbers don't shift)
- Merge `usings` additions (deduplicate)
- Handle auth migration once per file (detect ExchangeService initialization)
- Produce a single unified diff per file

**6c. Project-level orchestration:**
- Iterate all `.cs` files (reuse existing project scanning logic)
- Track aggregate statistics: total usages, converted, failed, by tier and confidence
- Produce a migration summary report

---

## Step 7: New MCP Tools

### File to modify:
- `src/Ews.Code.Analyzer/Ews.Analyzer.McpService/Program.cs`

### New tools to register in `ToolDispatcher.ListTools()`:

**7a. `convertToGraph` — Primary conversion tool:**

```
name: "convertToGraph"
description: "Automatically convert EWS code to Microsoft Graph SDK code"
parameters:
  - code (string): Inline C# code snippet to convert
  - path (string): Single file path to convert
  - rootPath (string): Project root to convert all files
  - tier (integer, optional): Force a specific tier (1, 2, or 3). Default: auto
  - dryRun (boolean, optional): If true, return diffs without applying. Default: true
  - maxFiles (integer, optional): Max files for project scan. Default: 200
required: at least one of [code, path, rootPath]
```

Returns:
```json
{
  "conversions": [
    {
      "file": "path/to/file.cs",
      "tier": 1,
      "confidence": "high",
      "originalCode": "...",
      "convertedCode": "...",
      "diff": "--- a/file.cs\n+++ b/file.cs\n...",
      "isValid": true,
      "validationErrors": []
    }
  ],
  "summary": {
    "totalUsages": 15,
    "converted": 12,
    "highConfidence": 8,
    "mediumConfidence": 3,
    "lowConfidence": 1,
    "failed": 3,
    "readinessPercent": 80.0
  }
}
```

**7b. `applyConversion` — Apply a previous conversion's diffs:**

```
name: "applyConversion"
description: "Apply a previously generated conversion diff to source files"
parameters:
  - diffs (array of {file, diff}): The diffs from convertToGraph output
  - backup (boolean, optional): Create .bak files before applying. Default: true
```

**7c. `convertAuth` — Standalone auth migration:**

```
name: "convertAuth"
description: "Convert EWS ExchangeService authentication setup to GraphServiceClient"
parameters:
  - code (string): Code containing ExchangeService initialization
  - authMethod (string): Target auth method — "clientCredential", "interactive", "deviceCode", "managedIdentity"
```

### Modify existing tools:
- **`suggestGraphFixes`**: Upgrade from TODO comments to actual conversion diffs. Internally delegates to `ConversionOrchestrator` with `dryRun: true`. Keep backward compatibility — if the orchestrator fails, fall back to the existing TODO-comment behavior.
- **`getMigrationReadiness`**: Add conversion success rate to the readiness calculation.

---

## Step 8: New MCP Prompts

### File to modify:
- `src/Ews.Code.Analyzer/Ews.Analyzer.McpService/Program.cs` (inside `ToolDispatcher`)

### New prompts:

**8a. `convert-ews-to-graph`:**
- Purpose: Guide the MCP host through a complete file/project conversion
- Messages:
  1. Analyze the code for EWS usages
  2. For each usage, show the proposed conversion with confidence
  3. Ask for confirmation before applying

**8b. `migrate-auth`:**
- Purpose: Guide auth migration specifically
- Messages: Template for converting ExchangeService credentials to Graph auth

---

## Step 9: Update the Roslyn Code Fix Provider

### File to modify:
- `src/Ews.Code.Analyzer/Ews.Analyzer.CodeFixes/EwsAnalyzerCodeFixProvider.cs`

### Changes:
- For EWS001 diagnostics (GA parity), add a second code fix action: **"Convert to Microsoft Graph SDK"**
- This action invokes the Tier 1 `DeterministicTransformer` inline (no LLM needed)
- If the roadmap entry has `conversionTier: 1` and a `graphCodeTemplate`, replace the EWS code with the Graph equivalent directly in the editor
- Keep the existing "Insert Copilot migration instructions" action as an alternative
- For EWS002/EWS003, keep only the existing instruction-based fix (no auto-convert for preview/gap operations)

---

## Step 10: Tests

### New test files:
- `src/Ews.Code.Analyzer/Ews.Analyzer.McpService.Tests/DeterministicTransformerTests.cs`
- `src/Ews.Code.Analyzer/Ews.Analyzer.McpService.Tests/ConversionValidatorTests.cs`
- `src/Ews.Code.Analyzer/Ews.Analyzer.McpService.Tests/ConversionOrchestratorTests.cs`
- `src/Ews.Code.Analyzer/Ews.Analyzer.McpService.Tests/TemplateGuidedConverterTests.cs`
- `src/Ews.Code.Analyzer/Ews.Analyzer.Test/EwsAnalyzerCodeFixTests.cs`

### Test categories:

**10a. Tier 1 deterministic transform tests:**
- Each of the 20 Tier 1 operations gets at least one test case
- Test: simple invocation → correct Graph SDK output
- Test: unexpected argument shape → returns null (falls through)
- Test: auth migration (ExchangeService → GraphServiceClient)

**10b. Validation gate tests:**
- Test: valid Graph code → `IsValid = true`
- Test: syntax error → `IsValid = false` with error details
- Test: remaining EWS reference → flagged in validation
- Test: confidence scoring logic

**10c. Orchestrator integration tests:**
- Test: Tier 1 success path (no LLM needed)
- Test: Tier 1 failure → Tier 2 fallback (mock ILlmClient)
- Test: Tier 2 failure → Tier 3 fallback
- Test: file-level multi-usage conversion
- Test: project-level scanning

**10d. MCP tool tests:**
- Test: `convertToGraph` with inline code
- Test: `convertToGraph` with file path
- Test: `applyConversion` creates backup and applies diff
- Test: `convertAuth` for each auth method

---

## Step 11: Documentation and Configuration

### Files to modify:
- `README.md` — Add "Automatic Conversion" section documenting the new tools
- `mcp.sample.json` — Add optional `LLM_ENDPOINT` / `LLM_API_KEY` environment variable config for standalone Tier 2/3 use

### New file:
- `src/Ews.Code.Analyzer/Ews.Analyzer.McpService/appsettings.json` — Optional configuration:

```json
{
  "Conversion": {
    "MaxTier": 3,
    "EnableLlm": true,
    "LlmMaxRetries": 1,
    "ValidationStrictness": "errors",
    "DefaultDryRun": true
  }
}
```

---

## Implementation Order

| Phase | Steps | Deliverable | Depends On |
|-------|-------|-------------|------------|
| **Phase 1** | Steps 1, 2, 5 | Tier 1 deterministic conversion + validation | Nothing |
| **Phase 2** | Step 6 (partial), 7a | `convertToGraph` MCP tool (Tier 1 only) | Phase 1 |
| **Phase 3** | Steps 3, 4 | LLM-based Tiers 2 & 3 | Phase 1 |
| **Phase 4** | Step 6 (complete), 7b, 7c | Full orchestrator + apply + auth tools | Phases 2, 3 |
| **Phase 5** | Steps 8, 9 | Prompts + Roslyn code fix upgrade | Phase 4 |
| **Phase 6** | Steps 10, 11 | Tests + documentation | Phase 5 |

### New files created:
1. `DeterministicTransformer.cs`
2. `TemplateGuidedConverter.cs`
3. `FullContextConverter.cs`
4. `ConversionValidator.cs`
5. `ConversionOrchestrator.cs`
6. `ILlmClient.cs` (interface + 2 implementations)
7. `ConversionResult.cs` (data model)
8. Test files (5)

### Existing files modified:
1. `EwsMigrationRoadmap.cs` (new properties)
2. `roadmap.json` (Tier 1 templates for 20 operations)
3. `Program.cs` (new tools, prompts, dispatcher routing)
4. `EwsAnalyzerCodeFixProvider.cs` (new code fix action)
5. `Ews.Analyzer.McpService.csproj` (new file references if needed)
6. `README.md` (documentation)
7. `mcp.sample.json` (env var config)
