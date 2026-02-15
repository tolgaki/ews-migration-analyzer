# EWS Migration Tools

## Overview

Exchange Web Services (EWS) is a SOAP-based API that is used to access Exchange Online and Exchange Server. Microsoft has announced the deprecation of EWS for Exchange Online in favor of Microsoft Graph API, which provides access to many Microsoft 365 workloads including Exchange Online.

This repo contains tools to help you identify apps within your tenant that are using EWS and help you migrate them to Microsoft Graph API.

## EWS Usage Reporting

M365 provides [reports of EWS usage](https://admin.cloud.microsoft/?#/reportsUsage/EWSWeeklyUsage) in the Microsoft 365 admin center. This your first port of call to identify apps using EWS. The reports are available in the Microsoft 365 admin center under Reports > Usage > Exchange Web Services (EWS) Weekly Usage for tenants in the worldwide cloud.

If your tenant is in an isolated cloud (e.g. government or sovereign cloud), these reports are not available in the admin center at this time.

The tools in this repo can be used to identify EWS usage in all clouds with access to the Microsoft Graph AuditLog API.

The EWS Usage Reporting tools are located in folder `/src/Ews.AppUsage` and build on [Jim Martin](https://github.com/jmartinmsft)'s excellent and versatile scripts for detecting EWS usage in Exchange Online at [Exchange App Usage Reporting](https://github.com/jmartinmsft/Exchange-App-Usage-Reporting).

See the [EWS App Usage Reporting Readme](src/Ews.App.Usage/README.md) for more information on how to set up and run the tools.

## EWS Code Analyzer

Once you have identified applications in your tenant using EWS, you can use the EWS Code Analyzer to identify all EWS references in your code and get suggestions for migration to equivalent Microsoft Graph APIs from related documentation and GitHub Copilot.

The code analyzer is a Roslyn analyzer that can be used in Visual Studio and Visual Studio Code. At this time it supports any EWS application built using .NET SDKs for EWS.

The EWS Code Analyzer is located in folder `/src/Ews.CodeAnalyzer`.

See the [EWS Code Analyzer Readme](src/Ews.Code.Analyzer/README.md) for more information on how to set up and run the tools.

---

## MCP Server (GitHub Copilot / Claude / LLM Integration)

An experimental Model Context Protocol (MCP) server enables GitHub Copilot, Claude, and other MCP-aware clients to interact with the EWS Migration Analyzer programmatically. It provides:

- **Code analysis** — Scan snippets, files, or entire projects for EWS SDK usage
- **Migration roadmap** — Look up EWS-to-Graph migration status for any operation
- **Automatic code conversion** — Convert EWS code to Microsoft Graph SDK using a hybrid 3-tier approach
- **Authentication migration** — Convert `ExchangeService`/`WebCredentials` to `GraphServiceClient`
- **Migration readiness** — Compute how ready a project is for Graph migration

Location: `src/Ews.Code.Analyzer/Ews.Analyzer.McpService`

### Quick Start

1. **Build the MCP server:**
   ```bash
   dotnet build src/Ews.Code.Analyzer/Ews.Analyzer.McpService/Ews.Analyzer.McpService.csproj
   ```

2. **Register it with your MCP client.** Copy `mcp.sample.json` to your client's configuration location:

   - **GitHub Copilot (VS Code):** `.vscode/mcp.json` in your workspace
   - **Claude Desktop:** `~/Library/Application Support/Claude/claude_desktop_config.json` (macOS) or `%APPDATA%\Claude\claude_desktop_config.json` (Windows)
   - **Other MCP clients:** Refer to your client's documentation

3. **Interact via your AI assistant.** Ask it to analyze EWS code, convert to Graph, or check migration readiness.

### Sample `mcp.json`

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

> **Note:** The `env` block is only needed if you want to use Tier 2/3 LLM-powered conversions with a direct API endpoint. See [Configuring an LLM for Automatic Conversion](#configuring-an-llm-for-automatic-conversion) below for details.

---

### Available MCP Tools

| Tool | Purpose |
|------|---------|
| `analyzeCode` | Analyze one or more sources for EWS usage (preferred entry point) |
| `analyzeSnippet` | Analyze a single inline C# code snippet |
| `analyzeFile` | Analyze a file on disk (within allowlist) |
| `analyzeProject` | Analyze all `*.cs` files under a root directory |
| `listEwsUsages` | List EWS SDK invocation sites with Graph migration metadata |
| `getRoadmap` | Get migration roadmap entry by SOAP operation or SDK qualified name |
| `generateGraphPrompt` | Produce a tailored migration prompt for Copilot |
| `suggestGraphFixes` | Generate unified diff hunks with TODO comments for actionable EWS usages |
| `getMigrationReadiness` | Compute migration readiness percentage for a project |
| **`convertToGraph`** | **Automatically convert EWS code to Graph SDK (hybrid 3-tier)** |
| **`applyConversion`** | **Apply previously generated conversion diffs to source files** |
| **`convertAuth`** | **Convert EWS auth setup to GraphServiceClient** |
| `setLogging` | Toggle verbose progress notifications |
| `addAllowedPath` | Add an allowed base path for file analysis |
| `listAllowedPaths` | List configured allowed base paths |

---

### Automatic Code Conversion (Hybrid 3-Tier Approach)

The `convertToGraph` tool uses a hybrid strategy to convert EWS code to Microsoft Graph SDK v5+:

| Tier | Strategy | Confidence | When Used |
|------|----------|------------|-----------|
| **Tier 1** | Deterministic Roslyn transform | **High** | ~35 common EWS operations with known 1:1 Graph SDK equivalents (mail CRUD, calendar, contacts, tasks, sync, notifications, inbox rules) |
| **Tier 2** | Template-guided LLM | **High–Medium** | Operations with prompt templates in the migration roadmap; moderate complexity |
| **Tier 3** | Full-context LLM | **Medium–Low** | Complex multi-operation methods, Gap/TBD operations, full class refactoring |

**How it works:**

1. The tool analyzes your code to find EWS SDK usage sites
2. For each usage, it selects the best tier based on the operation type
3. Tier 1 applies deterministic pattern-based transforms (no LLM needed)
4. If Tier 1 can't handle it, Tier 2 sends a structured prompt to an LLM with roadmap context
5. If Tier 2 fails validation, Tier 3 sends the full class/file context to the LLM
6. Every conversion passes through a **Roslyn compilation validation gate**
7. Each result is tagged with a confidence level (`high`, `medium`, or `low`)

**Example — convert a snippet:**
```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "tools/call",
  "params": {
    "name": "convertToGraph",
    "arguments": {
      "code": "service.FindItems(WellKnownFolderName.Inbox, new ItemView(50))"
    }
  }
}
```

**Example — convert a whole file:**
```json
{
  "jsonrpc": "2.0",
  "id": 2,
  "method": "tools/call",
  "params": {
    "name": "convertToGraph",
    "arguments": {
      "path": "/path/to/your/EwsMailService.cs"
    }
  }
}
```

**Example — convert an entire project:**
```json
{
  "jsonrpc": "2.0",
  "id": 3,
  "method": "tools/call",
  "params": {
    "name": "convertToGraph",
    "arguments": {
      "rootPath": "/path/to/your/project",
      "maxFiles": 200
    }
  }
}
```

**Example — convert authentication:**
```json
{
  "jsonrpc": "2.0",
  "id": 4,
  "method": "tools/call",
  "params": {
    "name": "convertAuth",
    "arguments": {
      "code": "var service = new ExchangeService(); service.Credentials = new WebCredentials(\"user\", \"pass\");",
      "authMethod": "clientCredential"
    }
  }
}
```

Supported `authMethod` values: `clientCredential` (default), `interactive`, `deviceCode`, `managedIdentity`

---

### Configuring an LLM for Automatic Conversion

**Tier 1 (deterministic) conversions work without any LLM configuration.** For Tier 2 and Tier 3 conversions, you have two options:

#### Option A: MCP Host Relay (Default — No Configuration Needed)

If you're using the MCP server inside an AI assistant (GitHub Copilot, Claude, etc.), the assistant itself acts as the LLM. The server sends structured prompts to the MCP host, and the host handles the actual LLM call. **This is the default behavior and requires no extra setup.**

#### Option B: Direct LLM API (For Standalone / CI / Custom Use)

If you want the server to call an LLM directly (for example, in a CI pipeline, a custom script, or when running standalone), set these environment variables:

| Variable | Required | Description |
|----------|----------|-------------|
| `LLM_ENDPOINT` | Yes | The API endpoint URL (must use HTTPS) |
| `LLM_API_KEY` | Yes | Your API key or token |
| `LLM_MODEL` | No | Model name (default: `gpt-4o`) |

**The server uses the OpenAI-compatible chat completions API format**, which is supported by Azure OpenAI, OpenAI, and many other providers.

#### Setup for Azure OpenAI

1. Deploy a model (e.g., `gpt-4o`) in your [Azure OpenAI resource](https://portal.azure.com/#create/Microsoft.CognitiveServicesOpenAI)
2. Get your endpoint and API key from the Azure portal (Keys and Endpoint section)
3. Set the environment variables:

```json
{
  "mcpServers": {
    "ews-analyzer": {
      "command": "dotnet",
      "args": ["run", "--project", "src/Ews.Code.Analyzer/Ews.Analyzer.McpService/Ews.Analyzer.McpService.csproj"],
      "env": {
        "LLM_ENDPOINT": "https://YOUR-RESOURCE.openai.azure.com/openai/deployments/YOUR-DEPLOYMENT/chat/completions?api-version=2024-08-01-preview",
        "LLM_API_KEY": "your-azure-openai-api-key",
        "LLM_MODEL": "gpt-4o"
      }
    }
  }
}
```

> **Azure OpenAI note:** Use the full deployment URL including `/chat/completions` and the `api-version` query parameter. The API key goes in the `Authorization: Bearer` header (which Azure OpenAI accepts when using the OpenAI-compatible endpoint format).

#### Setup for OpenAI

1. Get your API key from [platform.openai.com/api-keys](https://platform.openai.com/api-keys)
2. Set the environment variables:

```json
{
  "env": {
    "LLM_ENDPOINT": "https://api.openai.com/v1/chat/completions",
    "LLM_API_KEY": "sk-your-openai-api-key",
    "LLM_MODEL": "gpt-4o"
  }
}
```

#### Setup for Anthropic (Claude API)

The server uses the OpenAI-compatible format. To use Anthropic's Claude directly, you would need an OpenAI-compatible proxy or adapter. Alternatively, use Option A (MCP Host Relay) by running the MCP server inside Claude Desktop.

#### Setup for Local Models (Ollama, LM Studio, etc.)

For local development, you can point to a local server. HTTP (non-HTTPS) is allowed for `localhost` and `127.0.0.1`:

```json
{
  "env": {
    "LLM_ENDPOINT": "http://localhost:11434/v1/chat/completions",
    "LLM_API_KEY": "ollama",
    "LLM_MODEL": "llama3.1"
  }
}
```

#### Security Notes for LLM Configuration

- **HTTPS is enforced** for all non-localhost endpoints. The server will refuse to start if `LLM_ENDPOINT` uses plain HTTP on a remote host.
- **API keys are never logged** and are not included in error messages.
- **Your source code is sent to the LLM** when using Tier 2/3 conversions. Be aware of your organization's data handling policies.
- **Environment variables** are the only supported method for passing credentials. Do not hardcode API keys in configuration files that are committed to source control.

---

### MCP Resources and Prompts

**Resources** (URI pattern: `roadmap/{EwsSoapOperation}`) expose individual migration roadmap entries as structured data.

**Prompts:**

| Prompt | Description | Required Args |
|--------|-------------|---------------|
| `migrate-ews-usage` | Migration guidance for a single EWS SDK usage | `sdkQualifiedName` |
| `summarize-project-ews` | High-level EWS usage summary across a project | `rootPath` |
| `convert-ews-to-graph` | Convert EWS code to Graph SDK with confidence scoring | `code` |
| `migrate-auth` | Convert EWS authentication to GraphServiceClient | `code` |

---

### Security and Access Controls

- **Path allowlist:** File and project operations are restricted to the current working directory by default. Use `addAllowedPath` to extend access to additional directories.
- **Max file limits:** Project scans are capped at configurable limits (default 200–500 files, max 5000) to prevent resource exhaustion.
- **Dry-run by default:** The `convertToGraph` tool returns conversion diffs without modifying files. Use `applyConversion` separately to write changes (with automatic `.bak` backup files).
- **Error sanitization:** Internal file paths and stack traces are not exposed in error responses.
- **HTTPS enforcement:** Direct LLM API calls require HTTPS (except localhost for development).
- **No credential logging:** API keys and authentication tokens are never included in logs or error messages.

---

### GitHub Copilot Integration

This repository is pre-configured for GitHub Copilot:

- **`.vscode/mcp.json`** — Registers the MCP server so Copilot can use all analyzer and conversion tools
- **`.github/copilot-instructions.md`** — Gives Copilot project context, architecture diagram, Graph SDK code patterns, and a **copyable template** for users to paste into their own projects

When you open this repo in VS Code with Copilot, you can immediately ask things like:
```
Use analyzeCode to check this for EWS usage: [paste code]
Use convertToGraph to convert: service.FindItems(WellKnownFolderName.Inbox, new ItemView(50))
Use getMigrationReadiness to check /path/to/project
```

**To set up Copilot in your own EWS project**, see **[`samples/github-copilot-setup.md`](samples/github-copilot-setup.md)** — includes step-by-step instructions for copying `.vscode/mcp.json` and `.github/copilot-instructions.md` to your repo.

### Claude Code Integration

This repository is pre-configured for Claude Code:

- **`CLAUDE.md`** — Project context file read automatically by Claude Code
- **`.claude/hooks/session-start.sh`** — SessionStart hook that installs .NET SDK and builds the solution on web sessions
- **`.claude/settings.json`** — Hook registration
- **`.claude/commands/`** — Custom slash commands:

| Command | Description |
|---------|-------------|
| `/analyze-ews [path]` | Analyze a file or directory for EWS usage |
| `/convert-ews [path]` | Convert EWS code to Microsoft Graph SDK |
| `/convert-auth [code]` | Convert EWS authentication to Graph SDK |
| `/migration-readiness [path]` | Assess migration readiness of a project |
| `/suggest-fixes [path]` | Generate specific Graph SDK replacement suggestions |
| `/roadmap-lookup [operation]` | Look up Graph equivalent for an EWS operation |
| `/security-sweep [path]` | Run security review on the codebase |
| `/add-transform [desc]` | Add a new Tier 1 deterministic transform |
| `/run-tests` | Build and run all tests |

Usage in Claude Code:
```bash
cd ews-migration-analyzer
claude

> /analyze-ews src/MyApp/Services/MailService.cs
> /convert-ews src/MyApp/Services/
> /migration-readiness /path/to/my-ews-project
```

**To set up Claude Code in your own EWS project**, see **[`samples/claude-code-setup.md`](samples/claude-code-setup.md)** — includes copyable `CLAUDE.md`, all slash command files, and MCP server configuration.

### Samples and Automation

The `samples/` directory contains guides and scripts for integrating with your own projects:

| File | Description |
|------|-------------|
| [`github-copilot-setup.md`](samples/github-copilot-setup.md) | Copy-to-your-project Copilot setup with examples |
| [`claude-code-setup.md`](samples/claude-code-setup.md) | Copy-to-your-project Claude Code setup with slash commands |
| [`conversation-starters.md`](samples/conversation-starters.md) | 15+ ready-to-paste prompts for both platforms |
| [`mcp-client-example.py`](samples/mcp-client-example.py) | Python script for programmatic MCP server interaction |
| [`ci-automation.sh`](samples/ci-automation.sh) | Shell script for CI/CD pipeline integration |

---

### Diagnostic Rules

The Roslyn analyzer produces these diagnostics:

| Rule ID | Severity | Description |
|---------|----------|-------------|
| EWS000 | Warning | Unclassified EWS reference detected |
| EWS001 | Error | Graph API equivalent is **available** (GA) — action required |
| EWS002 | Warning | Graph API equivalent is **in preview** |
| EWS003 | Warning | Graph API equivalent is **not available** (Gap/TBD) |
| EWS004 | Info | EWS reference count summary (end of compilation) |
| EWS005 | Warning | Custom call-to-action (end of compilation) |

### Roadmap

Future improvements may include: MSBuild-based full project loading, richer caching, telemetry opt-in, expanded Tier 1 deterministic templates, and Anthropic Messages API native support.

---

## Sample Migrations

The `/src/Ews.Sample.Migration/` directory contains step-by-step sample migration scenarios:

- `00-Baseline` — Starting point with EWS code
- `01-Build_Understanding` — Understand the EWS usage patterns
- `02-Add_Instrumentation` — Add logging and monitoring
- `03-Add_Tests` — Build test coverage before migration
- `04-Refactor` — Refactor to prepare for Graph SDK migration

---

## Feedback

We welcome your feedback on the tools in this repo and also on your migration experience from EWS to Microsoft Graph API. You can provide feedback by creating an issue in this [repo](https://github.com/OfficeDev/ews-migration-analyzer/issues) or by posting questions on StackOverflow with the tag [exchangewebservices](https://stackoverflow.com/questions/tagged/exchangewebservices).

## Learn More

- [Deprecation of Exchange Web Services in Exchange Online](https://aka.ms/ews1pageGH)
- [Microsoft 365 Reports in the admin center -- EWS usage](https://aka.ms/EwsAdminReports)
- [Exchange Web Services (EWS) to Microsoft Graph API mappings](https://aka.ms/ewsMapGH)

## Contributing

This project welcomes contributions and suggestions.  Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit https://cla.opensource.microsoft.com.

When you submit a pull request, a CLA bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., status check, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.

## Trademarks

This project may contain trademarks or logos for projects, products, or services. Authorized use of Microsoft
trademarks or logos is subject to and must follow [Microsoft's Trademark & Brand Guidelines](https://www.microsoft.com/en-us/legal/intellectualproperty/trademarks/usage/general).
Use of Microsoft trademarks or logos in modified versions of this project must not cause confusion or imply Microsoft sponsorship.
Any use of third-party trademarks or logos are subject to those third-party's policies.
