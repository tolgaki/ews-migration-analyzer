# GitHub Copilot Instructions — EWS Migration Analyzer

## Project Context

This repository helps developers migrate Exchange Web Services (EWS) applications to Microsoft Graph API. EWS is being deprecated for Exchange Online (October 2026).

The repo contains:
- **Roslyn Analyzer** — Detects EWS SDK usage at compile time (EWS000–EWS005 diagnostics)
- **MCP Server** — Model Context Protocol server for AI-assisted migration
- **Hybrid Converter** — 3-tier automatic code conversion (deterministic → template-LLM → full-context-LLM)
- **Sample Migration** — Step-by-step tutorial (Contoso.Mail, 5 phases)

---

## For Users of This Repo (Developers Working on the Analyzer)

### Architecture

```
MCP Client (Copilot/Claude) ←→ Program.cs (JSON-RPC) ←→ ToolDispatcher
                                                            ├── AnalysisService (Roslyn)
                                                            ├── ConversionOrchestrator
                                                            │   ├── DeterministicTransformer (Tier 1)
                                                            │   ├── TemplateGuidedConverter (Tier 2)
                                                            │   └── FullContextConverter (Tier 3)
                                                            ├── ConversionValidator (Roslyn compile check)
                                                            └── EwsMigrationNavigator (roadmap lookup)
```

### Build & Test Commands
```bash
dotnet build src/Ews.Code.Analyzer/Ews.Analyzer.sln
dotnet test src/Ews.Code.Analyzer/Ews.Analyzer.McpService.Tests/
dotnet test src/Ews.Code.Analyzer/Ews.Analyzer.Test/
```

### Security Requirements
- Never hardcode credentials or API keys
- Validate file paths through `PathSecurity.IsPathAllowed()` before any I/O
- Clamp numeric inputs with `Math.Clamp(value, 1, 5000)`
- Use HTTPS for external API calls (except localhost)
- Sanitize error messages — never expose internal paths or stack traces

---

## For Users Migrating Their EWS Apps

If you're using this tool to migrate your own EWS application, copy the template below
into your project's `.github/copilot-instructions.md` and customize it.

### Copyable Template: `.github/copilot-instructions.md`

```markdown
# EWS to Graph Migration

## Context
This project is migrating from Exchange Web Services (EWS) to Microsoft Graph SDK v5+.
EWS is deprecated for Exchange Online (October 2026).

## Rules for Code Suggestions
- Replace `Microsoft.Exchange.WebServices.Data.*` with `Microsoft.Graph` SDK v5+
- Replace `ExchangeService` + `WebCredentials` with `GraphServiceClient` + `Azure.Identity`
- Always use async/await for Graph SDK calls
- Always include required `using` statements
- Prefer `$select` to limit returned properties
- Handle `ODataError` for Graph API errors
- Use scoped permissions (not `.default`) for user-facing apps

## Common Replacements

### Mail
| EWS | Graph SDK v5+ |
|-----|---------------|
| `service.FindItems(Inbox, view)` | `graphClient.Me.Messages.GetAsync(c => c.QueryParameters.Top = N)` |
| `EmailMessage.Send()` | `graphClient.Me.SendMail.PostAsync(body)` |
| `message.Reply(body, all)` | `graphClient.Me.Messages[id].Reply.PostAsync(body)` |
| `EmailMessage.Bind(id)` | `graphClient.Me.Messages[id].GetAsync()` |
| `message.Update(mode)` | `graphClient.Me.Messages[id].PatchAsync(msg)` |
| `message.Delete(mode)` | `graphClient.Me.Messages[id].DeleteAsync()` |
| `message.Move(folder)` | `graphClient.Me.Messages[id].Move.PostAsync(body)` |
| `message.Copy(folder)` | `graphClient.Me.Messages[id].Copy.PostAsync(body)` |

### Calendar
| EWS | Graph SDK v5+ |
|-----|---------------|
| `service.FindAppointments(view)` | `graphClient.Me.CalendarView.GetAsync(c => { ... })` |
| `appointment.Save(mode)` | `graphClient.Me.Events.PostAsync(evt)` |
| `appointment.Update(mode)` | `graphClient.Me.Events[id].PatchAsync(evt)` |
| `appointment.Delete(mode)` | `graphClient.Me.Events[id].DeleteAsync()` |

### Contacts
| EWS | Graph SDK v5+ |
|-----|---------------|
| `Contact.Bind(id)` | `graphClient.Me.Contacts[id].GetAsync()` |
| `contact.Save()` | `graphClient.Me.Contacts.PostAsync(contact)` |
| `contact.Update(mode)` | `graphClient.Me.Contacts[id].PatchAsync(contact)` |

### Authentication
| EWS | Graph SDK v5+ |
|-----|---------------|
| `new WebCredentials(user, pass)` | `new ClientSecretCredential(tenant, client, secret)` |
| `new OAuthCredentials(token)` | `new InteractiveBrowserCredential(options)` |
| `service.AutodiscoverUrl(email)` | Not needed — Graph SDK handles endpoints |

## Authentication Code Pattern
```csharp
using Azure.Identity;
using Microsoft.Graph;

// Daemon/service app
var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
var graphClient = new GraphServiceClient(credential,
    new[] { "https://graph.microsoft.com/.default" });

// Interactive user app
var credential = new InteractiveBrowserCredential(
    new InteractiveBrowserCredentialOptions { TenantId = tenantId, ClientId = clientId });
var graphClient = new GraphServiceClient(credential,
    new[] { "Mail.Read", "Mail.Send", "Calendars.ReadWrite" });
```

## Error Handling Pattern
```csharp
try
{
    var messages = await graphClient.Me.Messages.GetAsync();
}
catch (Microsoft.Graph.Models.ODataErrors.ODataError ex)
{
    Console.WriteLine($"Graph error: {ex.Error?.Code} - {ex.Error?.Message}");
}
```

## Required NuGet Packages
- Microsoft.Graph (>= 5.0.0)
- Azure.Identity (>= 1.10.0)
```

---

## Available MCP Tools

When the EWS Analyzer MCP server is running (via `.vscode/mcp.json`), these tools are available:

| Tool | Purpose | Example Prompt |
|------|---------|----------------|
| `analyzeCode` | Scan C# for EWS usage | "Use analyzeCode to check src/ for EWS usage" |
| `convertToGraph` | Convert EWS to Graph SDK | "Use convertToGraph on this code: ..." |
| `convertAuth` | Convert auth patterns | "Use convertAuth with clientCredential" |
| `applyConversion` | Apply conversion diffs | "Apply conversions with backups" |
| `getRoadmap` | Look up operation mapping | "Use getRoadmap for FindItems" |
| `getMigrationReadiness` | Check readiness score | "Check migration readiness" |
| `suggestGraphFixes` | Generate diff hunks | "Suggest fixes for src/" |
| `addAllowedPath` | Grant file access | "Add /path as allowed" |
