# GitHub Copilot Integration Guide

This guide shows how to use the EWS Migration Analyzer MCP server with GitHub Copilot in VS Code.

## Prerequisites

- Visual Studio Code with GitHub Copilot extension (v1.93+)
- .NET 9 SDK installed
- This repository cloned locally

## Setup

### Step 1: Configure the MCP Server

The repository already includes `.vscode/mcp.json` which registers the MCP server. If you want to add LLM support for Tier 2/3 conversions, edit the `env` section:

```json
{
  "mcpServers": {
    "ews-analyzer": {
      "command": "dotnet",
      "args": [
        "run", "--project",
        "src/Ews.Code.Analyzer/Ews.Analyzer.McpService/Ews.Analyzer.McpService.csproj"
      ],
      "env": {
        "LLM_ENDPOINT": "https://api.openai.com/v1/chat/completions",
        "LLM_API_KEY": "sk-your-key-here",
        "LLM_MODEL": "gpt-4o"
      }
    }
  }
}
```

### Step 2: Open the Workspace

Open the repository folder in VS Code. GitHub Copilot will automatically detect and start the MCP server.

### Step 3: Verify the Server is Running

In Copilot Chat, type:
```
@workspace List the available EWS analyzer tools
```

Copilot should show the available MCP tools.

---

## Usage Examples

### Example 1: Analyze a File for EWS Usage

In Copilot Chat:
```
Use the analyzeFile tool to check this file for EWS usage: /path/to/your/MailService.cs
```

Or use the `analyzeCode` tool with inline code:
```
Use analyzeCode to check this for EWS usage:

var service = new ExchangeService(ExchangeVersion.Exchange2013);
service.Credentials = new WebCredentials("user@contoso.com", "password");
service.AutodiscoverUrl("user@contoso.com");
var results = service.FindItems(WellKnownFolderName.Inbox, new ItemView(50));
```

### Example 2: Convert EWS Code to Graph SDK

```
Use convertToGraph to convert this code:

service.FindItems(WellKnownFolderName.Inbox, new ItemView(50))
```

Expected response includes:
- The Graph SDK equivalent code
- Confidence level (high/medium/low)
- Required `using` statements
- Required NuGet packages

### Example 3: Convert Authentication

```
Use convertAuth to convert this authentication code to Graph SDK using clientCredential:

var service = new ExchangeService();
service.Credentials = new WebCredentials("admin@contoso.com", "P@ssw0rd");
service.Url = new Uri("https://outlook.office365.com/EWS/Exchange.asmx");
```

### Example 4: Check Migration Readiness

```
Use getMigrationReadiness to check the project at /path/to/your/project
```

### Example 5: Convert an Entire Project

```
Use convertToGraph with rootPath set to /path/to/your/project to convert all EWS code to Graph SDK
```

### Example 6: Get Migration Guidance for a Specific Operation

```
Use getRoadmap to look up the Graph equivalent for Microsoft.Exchange.WebServices.Data.ExchangeService.FindItems
```

### Example 7: Generate a Migration Prompt

```
Use generateGraphPrompt for sdkQualifiedName: Microsoft.Exchange.WebServices.Data.EmailMessage.Send
with this surrounding code:

public void SendEmail(string to, string subject, string body)
{
    var message = new EmailMessage(service);
    message.Subject = subject;
    message.Body = new MessageBody(BodyType.HTML, body);
    message.ToRecipients.Add(to);
    message.Send();
}
```

### Example 8: Apply Conversions to Files

After getting results from `convertToGraph`, ask Copilot to apply them:
```
Use applyConversion to apply the conversion results to the source files. Create backups.
```

---

## Tips for Best Results

1. **Start with `getMigrationReadiness`** to understand the scope of migration
2. **Use `analyzeProject`** to get a full picture of EWS usage across your codebase
3. **Convert Tier 1 operations first** — these are deterministic and highest confidence
4. **Review Tier 2/3 conversions** — LLM-generated code should always be reviewed
5. **Always keep `.bak` backups** when using `applyConversion`
6. **Test after each batch** of conversions rather than converting everything at once

## Copilot Custom Instructions

The repository includes `.github/copilot-instructions.md` which gives Copilot context about the project. This file is automatically loaded by Copilot when working in this repo.

## Troubleshooting

**MCP server not starting:**
- Check that `dotnet` is in your PATH
- Run `dotnet build src/Ews.Code.Analyzer/Ews.Analyzer.McpService/Ews.Analyzer.McpService.csproj` manually to check for build errors

**Tools not appearing:**
- Restart VS Code
- Check the Output panel (View → Output → GitHub Copilot) for errors

**LLM conversions returning prompts instead of code:**
- The `LLM_ENDPOINT` and `LLM_API_KEY` environment variables may not be set
- Without these, Tier 2/3 returns structured prompts for the MCP host to process (this is normal behavior when using Copilot as the LLM)
