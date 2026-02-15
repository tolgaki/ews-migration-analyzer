# GitHub Copilot Instructions — EWS Migration Analyzer

## Project Context

This repository helps developers migrate Exchange Web Services (EWS) applications to Microsoft Graph API. EWS is being deprecated for Exchange Online (October 2026).

The repo contains:
- **Roslyn Analyzer** — Detects EWS SDK usage at compile time (EWS000–EWS005 diagnostics)
- **MCP Server** — Model Context Protocol server for AI-assisted migration (GitHub Copilot, Claude, etc.)
- **Hybrid Converter** — 3-tier automatic code conversion (deterministic → template-LLM → full-context-LLM)
- **Sample Migration** — Step-by-step tutorial (Contoso.Mail, 5 phases)

## When Helping with This Codebase

### EWS → Graph Conversions
- The migration roadmap is in `src/Ews.Code.Analyzer/Ews.Analyzer/roadmap.json`
- Graph SDK v5+ is the target (`Microsoft.Graph` NuGet package)
- Authentication: Use `Azure.Identity` (ClientSecretCredential, InteractiveBrowserCredential, etc.)
- Always include required `using` statements and NuGet packages in suggestions

### Common Graph SDK Patterns
```csharp
// Mail: List messages
var messages = await graphClient.Me.Messages.GetAsync(config =>
{
    config.QueryParameters.Top = 50;
    config.QueryParameters.Select = new[] { "subject", "from", "receivedDateTime" };
});

// Mail: Send
await graphClient.Me.SendMail.PostAsync(new SendMailPostRequestBody
{
    Message = new Message { Subject = "...", Body = new ItemBody { Content = "...", ContentType = BodyType.Text } }
});

// Calendar: List events
var events = await graphClient.Me.CalendarView.GetAsync(config =>
{
    config.QueryParameters.StartDateTime = start.ToString("o");
    config.QueryParameters.EndDateTime = end.ToString("o");
});

// Authentication setup
var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
var graphClient = new GraphServiceClient(credential, new[] { "https://graph.microsoft.com/.default" });
```

### MCP Server Architecture
- Entry point: `Program.cs` — JSON-RPC 2.0 over stdio
- Tool dispatch: `ToolDispatcher.CallToolAsync()` routes to handler methods
- Path security: All file I/O goes through `PathSecurity.IsPathAllowed()`
- Conversion pipeline: `ConversionOrchestrator` → Tier 1/2/3 → `ConversionValidator`

### Security Requirements
- Never hardcode credentials or API keys
- Validate all file paths through PathSecurity before I/O
- Clamp numeric user inputs (e.g., maxFiles) with Math.Clamp
- Use HTTPS for all external API calls (except localhost)
- Sanitize error messages — don't expose internal paths or stack traces

## Available MCP Tools

When the EWS Analyzer MCP server is running, you can use these tools:

| Tool | What it does |
|------|-------------|
| `analyzeCode` | Analyze C# sources for EWS usage |
| `convertToGraph` | Auto-convert EWS code to Graph SDK |
| `convertAuth` | Convert ExchangeService auth to GraphServiceClient |
| `applyConversion` | Apply conversion diffs to source files |
| `getRoadmap` | Look up migration roadmap for an operation |
| `getMigrationReadiness` | Score how ready a project is for migration |
| `suggestGraphFixes` | Generate diff hunks with Graph replacement TODOs |

## Build Commands

```bash
dotnet build src/Ews.Code.Analyzer/Ews.Analyzer.sln
dotnet test src/Ews.Code.Analyzer/Ews.Analyzer.McpService.Tests/
dotnet test src/Ews.Code.Analyzer/Ews.Analyzer.Test/
```
