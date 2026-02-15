# Claude Code Integration Guide

Set up Claude Code to use the EWS Migration Analyzer for migrating **your own EWS application**.

## Prerequisites

- Claude Code CLI installed (`npm install -g @anthropic-ai/claude-code`) or Claude Desktop
- .NET 8+ SDK installed
- The `ews-migration-analyzer` repo cloned somewhere (e.g., `~/repos/ews-migration-analyzer`)

---

## Setup for Your Own Project

### Step 1: Create `CLAUDE.md` in Your Project Root

Copy this file to `YOUR-PROJECT/CLAUDE.md` and customize:

```markdown
# Project Migration Context

## Overview
This project is migrating from Exchange Web Services (EWS) to Microsoft Graph SDK v5+.
EWS is deprecated for Exchange Online (October 2026).

## Migration Goals
- Replace all EWS SDK calls with Microsoft Graph SDK v5+ equivalents
- Migrate authentication from ExchangeService/WebCredentials to GraphServiceClient/Azure.Identity
- Maintain existing functionality and test coverage

## Key Files to Migrate
<!-- List your EWS-dependent files here -->
- src/Services/MailService.cs
- src/Services/CalendarService.cs
- src/Startup.cs (authentication setup)

## Build & Test
<!-- Your project's build commands -->
dotnet build
dotnet test

## Required NuGet Packages (post-migration)
- Microsoft.Graph (>= 5.0.0)
- Azure.Identity (>= 1.10.0)
```

### Step 2: Copy Slash Commands to Your Project

Create the `.claude/commands/` directory in your project and copy these files:

```bash
mkdir -p YOUR-PROJECT/.claude/commands
```

#### `YOUR-PROJECT/.claude/commands/analyze-ews.md`

```markdown
# Analyze EWS Usage

Scan the project for EWS SDK usage and report migration readiness.

## Instructions

1. Read the file or directory: $ARGUMENTS (default: current directory)
2. Find all `.cs` files and identify EWS SDK calls (`Microsoft.Exchange.WebServices.*`)
3. For each EWS usage, report:
   - File path and line number
   - EWS SDK method/class being used
   - Whether a Graph SDK equivalent exists
   - Migration difficulty: Easy / Medium / Hard
4. Summary:
   - Total EWS references
   - Migration readiness percentage
   - Recommended migration order
```

#### `YOUR-PROJECT/.claude/commands/convert-ews.md`

```markdown
# Convert EWS to Graph SDK

Convert EWS SDK code to Microsoft Graph SDK v5+.

## Instructions

1. Read the file or code: $ARGUMENTS
2. For each EWS call, generate the Graph SDK v5+ replacement:
   - Use `Microsoft.Graph` NuGet package (v5+)
   - Use `Azure.Identity` for authentication
   - Include all required `using` statements
   - Use async/await patterns
3. Show before/after diff for each conversion
4. Rate confidence: High / Medium / Low
5. List required NuGet packages
6. Note any EWS features without Graph equivalents

## Key Mappings
- `ExchangeService.FindItems` → `graphClient.Me.Messages.GetAsync`
- `EmailMessage.Send` → `graphClient.Me.SendMail.PostAsync`
- `Appointment.Save` → `graphClient.Me.Events.PostAsync`
- `Contact.Save` → `graphClient.Me.Contacts.PostAsync`
- `ExchangeService.FindAppointments` → `graphClient.Me.CalendarView.GetAsync`
```

#### `YOUR-PROJECT/.claude/commands/convert-auth.md`

```markdown
# Convert EWS Authentication to Graph SDK

Convert ExchangeService/WebCredentials to GraphServiceClient/Azure.Identity.

## Instructions

1. Read the code: $ARGUMENTS
2. Identify the EWS auth pattern (WebCredentials, OAuthCredentials, etc.)
3. Generate the Graph SDK replacement based on app type:

   **Daemon/service app (no user):**
   ```csharp
   var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
   var graphClient = new GraphServiceClient(credential, new[] { "https://graph.microsoft.com/.default" });
   ```

   **Interactive user app:**
   ```csharp
   var credential = new InteractiveBrowserCredential(new InteractiveBrowserCredentialOptions
   {
       TenantId = tenantId, ClientId = clientId, RedirectUri = new Uri("http://localhost")
   });
   var graphClient = new GraphServiceClient(credential, new[] { "Mail.Read", "Mail.Send" });
   ```

   **Managed identity (Azure hosted):**
   ```csharp
   var credential = new ManagedIdentityCredential();
   var graphClient = new GraphServiceClient(credential, new[] { "https://graph.microsoft.com/.default" });
   ```

4. Required packages: Microsoft.Graph (>= 5.0.0), Azure.Identity (>= 1.10.0)
5. List Azure AD app registration changes needed
6. Show complete before/after transformation
```

#### `YOUR-PROJECT/.claude/commands/migration-readiness.md`

```markdown
# Check Migration Readiness

Assess how ready this project is to migrate from EWS to Graph SDK.

## Instructions

1. Scan: $ARGUMENTS (default: current directory)
2. Find all `*.cs` files with EWS SDK references
3. Categorize each reference:
   - **Ready** — Graph API equivalent is GA
   - **Preview** — Graph API equivalent is in Preview
   - **Blocked** — No Graph API equivalent yet
4. Produce a readiness report with:
   - Readiness score percentage
   - Breakdown table (Ready / Preview / Blocked counts)
   - List of blocked operations with workarounds
   - Recommended migration order
   - Required NuGet packages
   - Effort estimate (automatic vs manual)
```

#### `YOUR-PROJECT/.claude/commands/suggest-fixes.md`

```markdown
# Suggest Graph SDK Replacements

Generate specific code fix suggestions for each EWS usage in the project.

## Instructions

1. Scan: $ARGUMENTS (default: current directory)
2. For each EWS SDK call found:
   - Show the original line with file path and line number
   - Provide the Graph SDK replacement code
   - Include required usings
   - Rate confidence level
3. Group suggestions by file
4. For each file, generate a unified diff that could be applied
5. At the end, summarize:
   - Total suggestions generated
   - High/Medium/Low confidence breakdown
   - Files that need manual review
```

### Step 3: Set Up MCP Server (Optional — For Tool-Based Usage)

If you want Claude Code to use the MCP tools directly (not just the slash commands), add the MCP server to your Claude Code settings.

Create or edit `YOUR-PROJECT/.claude/settings.json`:

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

> **Replace** `/path/to/ews-migration-analyzer` with the actual path.

---

## Usage in Your Project

### With Slash Commands (no MCP server needed)

```bash
cd your-ews-project
claude

# Analyze the whole project
> /analyze-ews .

# Convert a specific file
> /convert-ews src/Services/MailService.cs

# Convert authentication
> /convert-auth src/Startup.cs

# Check readiness
> /migration-readiness .

# Get fix suggestions
> /suggest-fixes src/Services/
```

### With Natural Language

```
> I need to migrate this project from EWS to Microsoft Graph.
  Start by analyzing all C# files for EWS usage and tell me what needs to change.
```

```
> Convert all the mail operations in src/Services/MailService.cs from EWS to Graph SDK.
  Show me diffs but don't apply yet.
```

```
> My app uses ExchangeService with WebCredentials.
  Convert the auth in src/Startup.cs to Azure.Identity ClientSecretCredential.
```

### End-to-End Migration Workflow

```
> Help me migrate this EWS app to Graph SDK step by step:
  1. First check migration readiness
  2. Convert the easy (high-confidence) operations
  3. Show me what's left for manual review
  4. Update the csproj to add Microsoft.Graph and Azure.Identity
  5. Run the tests to verify nothing broke
```

---

## Claude Desktop Setup

For Claude Desktop (GUI), add the MCP server to your configuration:

**macOS:** `~/Library/Application Support/Claude/claude_desktop_config.json`
**Windows:** `%APPDATA%\Claude\claude_desktop_config.json`

```json
{
  "mcpServers": {
    "ews-analyzer": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "/absolute/path/to/ews-migration-analyzer/src/Ews.Code.Analyzer/Ews.Analyzer.McpService/Ews.Analyzer.McpService.csproj"
      ]
    }
  }
}
```

---

## Quick Copy Checklist

To set up your project for EWS migration with Claude Code:

- [ ] Create `CLAUDE.md` at project root (customize with your files)
- [ ] Create `.claude/commands/analyze-ews.md`
- [ ] Create `.claude/commands/convert-ews.md`
- [ ] Create `.claude/commands/convert-auth.md`
- [ ] Create `.claude/commands/migration-readiness.md`
- [ ] Create `.claude/commands/suggest-fixes.md`
- [ ] (Optional) Configure MCP server in `.claude/settings.json`
- [ ] Add `.claude/` to your `.gitignore` if you don't want to commit these
