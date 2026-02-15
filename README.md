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

## MCP Server (GitHub Copilot Integration)

An experimental Model Context Protocol (MCP) server is included to allow GitHub Copilot (and other MCP-aware clients) to:

- Analyze snippets / files / directories for EWS usage
- List EWS SDK invocation sites with Graph migration metadata
- Retrieve migration roadmap entries (also exposed as MCP resources)
- Generate a tailored prompt to convert one EWS usage to Microsoft Graph (also via MCP prompts)
- **Automatically convert EWS code to Microsoft Graph SDK** using a hybrid 3-tier approach
- Convert EWS authentication (ExchangeService) to Graph SDK authentication (GraphServiceClient)
- Stream partial progress notifications (when verbose logging enabled)

Location: `src/Ews.Code.Analyzer/Ews.Analyzer.McpService`

### Running Locally

1. Restore & build:
	 ```bash
	 dotnet build src/Ews.Code.Analyzer/Ews.Analyzer.McpService/Ews.Analyzer.McpService.csproj
	 ```
2. (Optional) Run standalone and send JSON-RPC lines:
	 ```bash
	 dotnet run --project src/Ews.Code.Analyzer/Ews.Analyzer.McpService/Ews.Analyzer.McpService.csproj
	 ```

### Sample `mcp.json`

Place this in your Copilot client configuration (or use the provided `mcp.sample.json`).

```json
{
	"mcpServers": {
		"ews-analyzer": {
			"command": "dotnet",
			"args": ["run", "--project", "src/Ews.Code.Analyzer/Ews.Analyzer.McpService/Ews.Analyzer.McpService.csproj"],
			"alwaysAllow": true
		}
	}
}
```

### Available Tools

| Tool | Purpose |
|------|---------|
| analyzeCode | Analyze one or more sources for EWS usage (preferred) |
| analyzeSnippet | Analyze inline C# code |
| analyzeFile | Analyze a file path (within repo allowlist) |
| analyzeProject | Analyze all `*.cs` files under a root (capped) |
| listEwsUsages | Return just EWS usages with Graph metadata |
| getRoadmap | Get roadmap by SOAP op or SDK qualified name |
| generateGraphPrompt | Produce migration prompt for Copilot |
| suggestGraphFixes | Generate unified diff hunks for EWS usages with Graph parity |
| getMigrationReadiness | Compute migration readiness score for a project |
| **convertToGraph** | **Automatically convert EWS code to Graph SDK (hybrid 3-tier)** |
| **applyConversion** | **Apply previously generated conversion diffs to source files** |
| **convertAuth** | **Convert EWS auth setup to GraphServiceClient** |
| setLogging | Toggle verbose notifications |
| addAllowedPath | Add an allowed base path for analysis |
| listAllowedPaths | List configured allowed base paths |

### Automatic Conversion (Hybrid 3-Tier Approach)

The `convertToGraph` tool uses a hybrid strategy to convert EWS code to Microsoft Graph SDK:

| Tier | Strategy | Confidence | When Used |
|------|----------|------------|-----------|
| **Tier 1** | Deterministic Roslyn transform | High | ~35 common EWS operations with known Graph SDK equivalents |
| **Tier 2** | Template-guided LLM | High-Medium | Operations with copilot prompt templates, moderate complexity |
| **Tier 3** | Full-context LLM | Medium-Low | Complex patterns, multi-operation methods, Gap/TBD operations |

Each conversion passes through a **validation gate** (Roslyn compilation check) and is tagged with a confidence level (`high`/`medium`/`low`).

**Example usage:**
```json
{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"convertToGraph","arguments":{"code":"service.FindItems(WellKnownFolderName.Inbox, new ItemView(50))"}}}
```

**Authentication conversion:**
```json
{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"convertAuth","arguments":{"code":"var service = new ExchangeService(); service.Credentials = new WebCredentials(\"user\", \"pass\");","authMethod":"clientCredential"}}}
```

To use Tier 2/3 with a direct LLM backend (instead of MCP host relay), set environment variables:
- `LLM_ENDPOINT` — API endpoint (e.g., Azure OpenAI)
- `LLM_API_KEY` — API key
- `LLM_MODEL` — Model name (default: `gpt-4o`)

### Resources & Prompts

MCP Resources (URI pattern `roadmap/<EwsSoapOperation>`) expose individual roadmap entries.

MCP Prompts:

| Prompt | Description | Required Args |
|--------|-------------|---------------|
| migrate-ews-usage | Migration guidance for a single usage | sdkQualifiedName |
| summarize-project-ews | High-level summary request (client should call analyzeProject first) | rootPath |
| convert-ews-to-graph | Automatically convert EWS code to Graph SDK with confidence scoring | code |
| migrate-auth | Convert EWS authentication to GraphServiceClient | code |

### Security / Limits

- File & project tools are restricted to the current working directory tree.
- Max project files scanned defaults to 500 (override with `maxFiles`).
- Basic in-memory hashing cache avoids re-analyzing unchanged code.
- Conversion diffs are returned in dry-run mode by default; use `applyConversion` to write changes.

### Roadmap

Future improvements may include: MSBuild-based full project loading, richer caching, cancellation, telemetry opt-in, and expanded Tier 1 deterministic templates.

## Feedback

We welcome your feedback on the tools in this repo and also on your migration experience from EWS to Microsoft Graph API. You can provide feedback by creating an issue in this [repo](https://github.com/OfficeDev/ews-migration-analyzer/issues) or by posting questions on StackOverflow with the tag [exchangewebservices](https://stackoverflow.com/questions/tagged/exchangewebservices).

## Learn More

- [Deprecation of Exchange Web Services in Exchange Online](https://aka.ms/ews1pageGH)
- [Microsoft 365 Reports in the admin center – EWS usage](https://aka.ms/EwsAdminReports)
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
