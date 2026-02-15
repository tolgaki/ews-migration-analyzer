# Claude Code Integration Guide

This guide shows how to use the EWS Migration Analyzer with Claude Code (the CLI tool) and Claude Desktop.

## Prerequisites

- Claude Code CLI installed (`npm install -g @anthropic-ai/claude-code`) or Claude Desktop
- .NET 9 SDK installed
- This repository cloned locally

---

## Option A: Claude Code (CLI)

### Setup

Claude Code automatically reads the `CLAUDE.md` file at the repo root for project context. No additional configuration needed for basic usage.

### Custom Slash Commands

The repository includes pre-built slash commands in `.claude/commands/`:

| Command | Description |
|---------|-------------|
| `/analyze-ews [path]` | Analyze a file or directory for EWS usage |
| `/convert-ews [path or code]` | Convert EWS code to Microsoft Graph SDK |
| `/convert-auth [code]` | Convert EWS authentication to Graph SDK auth |
| `/migration-readiness [path]` | Assess migration readiness of a project |
| `/run-tests` | Build and run all tests |

### Using Slash Commands

```bash
# Start Claude Code in the repo directory
cd ews-migration-analyzer
claude

# Then in the Claude Code session:

# Analyze a file
> /analyze-ews src/MyApp/Services/MailService.cs

# Convert EWS code in a file
> /convert-ews src/MyApp/Services/MailService.cs

# Check migration readiness for a project
> /migration-readiness /path/to/my-ews-project

# Convert authentication
> /convert-auth src/MyApp/Startup.cs

# Build and run tests
> /run-tests
```

### Using the MCP Server with Claude Code

To use the MCP tools directly, configure Claude Code to connect to the MCP server. Add to your Claude Code MCP settings:

```json
{
  "mcpServers": {
    "ews-analyzer": {
      "command": "dotnet",
      "args": ["run", "--project", "src/Ews.Code.Analyzer/Ews.Analyzer.McpService/Ews.Analyzer.McpService.csproj"],
      "env": {
        "LLM_ENDPOINT": "",
        "LLM_API_KEY": "",
        "LLM_MODEL": "gpt-4o"
      }
    }
  }
}
```

Then you can ask Claude to use the MCP tools directly:

```
> Use the convertToGraph MCP tool to convert this code:
  service.FindItems(WellKnownFolderName.Inbox, new ItemView(50))
```

### Conversation Examples

**Analyze a project:**
```
> I have an EWS application at /path/to/my-project.
  Analyze all the C# files and tell me what needs to migrate to Graph SDK.
```

**Batch convert a directory:**
```
> Convert all EWS code in src/Services/ to Microsoft Graph SDK.
  Show me the diffs before applying. Use Tier 1 (deterministic) only for high confidence.
```

**Migrate authentication:**
```
> My app uses ExchangeService with WebCredentials. Convert the auth setup
  in src/Startup.cs to use Azure.Identity with client credentials for a daemon app.
```

**End-to-end migration workflow:**
```
> Help me migrate my EWS application to Graph SDK:
  1. First check migration readiness at /path/to/project
  2. Convert all Tier 1 (easy) operations
  3. Show me what remains for manual review
  4. Update the csproj to add Microsoft.Graph and Azure.Identity packages
```

---

## Option B: Claude Desktop

### Setup

Add the MCP server to Claude Desktop's configuration:

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
      ],
      "env": {}
    }
  }
}
```

> **Important:** Use the absolute path to the `.csproj` file in the `args` array.

### Usage in Claude Desktop

Once configured, the EWS Analyzer tools appear in Claude Desktop's tool list. You can ask:

```
Analyze this EWS code for migration readiness:

var service = new ExchangeService(ExchangeVersion.Exchange2013);
service.Credentials = new WebCredentials("user@contoso.com", "pass");
service.AutodiscoverUrl("user@contoso.com");

// Find inbox messages
var results = service.FindItems(WellKnownFolderName.Inbox, new ItemView(50));
foreach (var item in results)
{
    Console.WriteLine(item.Subject);
}

// Send an email
var message = new EmailMessage(service);
message.Subject = "Hello";
message.Body = new MessageBody(BodyType.Text, "World");
message.ToRecipients.Add("recipient@contoso.com");
message.Send();
```

Claude will use the MCP tools to analyze the code, convert it to Graph SDK, and show you the results with confidence ratings.

---

## Tips

1. **`CLAUDE.md` provides project context** — Claude Code reads it automatically, so Claude already understands the codebase architecture
2. **Slash commands are shortcuts** — Use `/convert-ews` instead of typing a full prompt
3. **Start with readiness check** — `/migration-readiness` gives you the big picture before diving in
4. **Tier 1 is safest** — Ask for deterministic-only conversions first, then review LLM results
5. **Always test** — Run `/run-tests` after making changes to catch regressions
