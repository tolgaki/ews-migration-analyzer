# GitHub Copilot Integration Guide

Set up GitHub Copilot to use the EWS Migration Analyzer MCP server in **your own EWS application project**.

## Prerequisites

- Visual Studio Code with GitHub Copilot extension (v1.93+)
- .NET 8+ SDK installed
- The `ews-migration-analyzer` repo cloned somewhere on your machine (e.g., `~/repos/ews-migration-analyzer`)

---

## Setup (Copy to Your Project)

### Step 1: Create `.vscode/mcp.json` in Your Project

Create this file at `YOUR-PROJECT/.vscode/mcp.json`. Adjust the `--project` path to where you cloned the analyzer:

```json
{
  "mcpServers": {
    "ews-analyzer": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "/path/to/ews-migration-analyzer/src/Ews.Code.Analyzer/Ews.Analyzer.McpService/Ews.Analyzer.McpService.csproj"
      ],
      "env": {
        "LLM_ENDPOINT": "",
        "LLM_API_KEY": "",
        "LLM_MODEL": "gpt-4o"
      }
    }
  }
}
```

> **Replace** `/path/to/ews-migration-analyzer` with the actual path where you cloned the repo.
>
> **LLM env vars** are optional — leave empty to use Copilot itself as the LLM (recommended).

### Step 2: Create `.github/copilot-instructions.md` in Your Project

Create this file at `YOUR-PROJECT/.github/copilot-instructions.md`. Copy and customize:

```markdown
# Copilot Instructions — EWS to Graph Migration

## Project Context
This project is migrating from Exchange Web Services (EWS) to Microsoft Graph SDK v5+.
EWS is being deprecated for Exchange Online (October 2026).

## Migration Rules
- Replace all `Microsoft.Exchange.WebServices.Data.*` usage with `Microsoft.Graph` SDK v5+
- Replace `ExchangeService` + `WebCredentials` with `GraphServiceClient` + `Azure.Identity`
- Use async/await patterns for all Graph SDK calls
- Always include required `using` statements in suggestions

## Graph SDK Patterns
```csharp
// Authentication
var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
var graphClient = new GraphServiceClient(credential, new[] { "https://graph.microsoft.com/.default" });

// List messages (replaces FindItems)
var messages = await graphClient.Me.Messages.GetAsync(config =>
{
    config.QueryParameters.Top = 50;
    config.QueryParameters.Select = new[] { "subject", "from", "receivedDateTime" };
});

// Send mail (replaces EmailMessage.Send)
await graphClient.Me.SendMail.PostAsync(new SendMailPostRequestBody
{
    Message = new Message
    {
        Subject = "...",
        Body = new ItemBody { Content = "...", ContentType = BodyType.Text },
        ToRecipients = new List<Recipient>
        {
            new() { EmailAddress = new EmailAddress { Address = "user@example.com" } }
        }
    }
});

// List calendar events (replaces FindAppointments)
var events = await graphClient.Me.CalendarView.GetAsync(config =>
{
    config.QueryParameters.StartDateTime = start.ToString("o");
    config.QueryParameters.EndDateTime = end.ToString("o");
});
```

## Required NuGet Packages
- `Microsoft.Graph` (>= 5.0.0)
- `Azure.Identity` (>= 1.10.0)

## Available MCP Tools
The EWS Analyzer MCP server provides these tools:
- `analyzeCode` — Scan C# sources for EWS usage
- `convertToGraph` — Auto-convert EWS code to Graph SDK
- `convertAuth` — Convert authentication patterns
- `getMigrationReadiness` — Check how ready the project is
- `getRoadmap` — Look up Graph equivalent for any EWS operation
- `suggestGraphFixes` — Generate diff hunks with Graph replacements
```

### Step 3: Open Your Project in VS Code

Open your EWS project folder. Copilot will auto-discover the MCP server and load the instructions.

---

## Usage Examples

Once set up, use these in Copilot Chat:

### Analyze your code
```
Analyze all C# files in this project for EWS usage.
How many EWS references are there and what's my migration readiness?
```

### Convert a specific file
```
Use convertToGraph to convert the EWS code in src/Services/MailService.cs to Graph SDK
```

### Convert authentication
```
Use convertAuth to convert the ExchangeService setup in src/Startup.cs to use
ClientSecretCredential with GraphServiceClient
```

### Check migration readiness
```
Use getMigrationReadiness to check this project. Show me what percentage of EWS
operations have Graph SDK equivalents.
```

### Get roadmap for a specific operation
```
Use getRoadmap to look up Microsoft.Exchange.WebServices.Data.ExchangeService.FindItems.
What's the Graph equivalent?
```

### Convert an entire project
```
Use convertToGraph with rootPath set to the project root. Convert all EWS code.
Show me the diffs before applying anything.
```

### Apply conversions
```
Use applyConversion to apply the conversion results. Create backups first.
```

### Suggest fixes as diff hunks
```
Use suggestGraphFixes for this project. Generate diff hunks I can review.
```

---

## Tips

1. **Start with readiness** — `getMigrationReadiness` gives you the big picture
2. **Tier 1 is safest** — Deterministic conversions (no LLM) have the highest confidence
3. **Review before applying** — Always review diffs before using `applyConversion`
4. **LLM env vars are optional** — Copilot itself acts as the LLM for Tier 2/3 by default
5. **Test after each batch** — Convert and test incrementally, not all at once

## Troubleshooting

| Problem | Solution |
|---------|----------|
| MCP server not starting | Run `dotnet build` on the MCP project manually to check for build errors |
| Tools not appearing in Copilot | Restart VS Code; check Output panel (GitHub Copilot) for errors |
| "Path not allowed" errors | The MCP server only allows the current directory by default. Use `addAllowedPath` to grant access |
| LLM conversions return prompts | This is normal when Copilot is the LLM — it processes the prompt itself |
