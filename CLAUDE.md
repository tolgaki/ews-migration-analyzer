# EWS Migration Analyzer

## Project Overview

This repository contains tools for migrating Exchange Web Services (EWS) applications to Microsoft Graph API. EWS is being deprecated for Exchange Online (October 2026).

## Architecture

The repo has three main components:

1. **EWS App Usage Reporting** (`src/Ews.App.Usage/`) — Python/PowerShell tools to identify EWS usage in your M365 tenant via audit logs
2. **EWS Code Analyzer** (`src/Ews.Code.Analyzer/`) — Roslyn-based analyzer + MCP server for detecting EWS SDK usage and converting to Graph
3. **Sample Migration** (`src/Ews.Sample.Migration/`) — Step-by-step migration tutorial (Contoso.Mail app, 5 phases)

## Key Source Files

### MCP Server (primary development target)

- `src/Ews.Code.Analyzer/Ews.Analyzer.McpService/Program.cs` — MCP JSON-RPC server, tool dispatcher, path security
- `src/Ews.Code.Analyzer/Ews.Analyzer.McpService/ConversionOrchestrator.cs` — Hybrid 3-tier conversion coordinator
- `src/Ews.Code.Analyzer/Ews.Analyzer.McpService/DeterministicTransformer.cs` — Tier 1: ~35 pattern-based EWS→Graph transforms
- `src/Ews.Code.Analyzer/Ews.Analyzer.McpService/TemplateGuidedConverter.cs` — Tier 2: LLM with roadmap prompt templates
- `src/Ews.Code.Analyzer/Ews.Analyzer.McpService/FullContextConverter.cs` — Tier 3: Full-context LLM conversion
- `src/Ews.Code.Analyzer/Ews.Analyzer.McpService/ConversionValidator.cs` — Roslyn compilation validation gate
- `src/Ews.Code.Analyzer/Ews.Analyzer.McpService/ILlmClient.cs` — LLM abstraction (MCP relay + HTTP direct)
- `src/Ews.Code.Analyzer/Ews.Analyzer.McpService/ConversionResult.cs` — Conversion output data model

### Roslyn Analyzer

- `src/Ews.Code.Analyzer/Ews.Analyzer/EwsAnalyzer.cs` — Diagnostic analyzer (EWS000–EWS005)
- `src/Ews.Code.Analyzer/Ews.Analyzer/EwsMigrationRoadmap.cs` — Migration roadmap data model + JSON loader
- `src/Ews.Code.Analyzer/Ews.Analyzer/EwsMigrationNavigator.cs` — Roadmap lookup service
- `src/Ews.Code.Analyzer/Ews.Analyzer.CodeFixes/EwsAnalyzerCodeFixProvider.cs` — IDE code fix actions

### Tests

- `src/Ews.Code.Analyzer/Ews.Analyzer.McpService.Tests/` — MCP service unit tests
- `src/Ews.Code.Analyzer/Ews.Analyzer.Test/` — Analyzer unit tests

## Build & Test

```bash
# Build the MCP server
dotnet build src/Ews.Code.Analyzer/Ews.Analyzer.McpService/Ews.Analyzer.McpService.csproj

# Build the full solution
dotnet build src/Ews.Code.Analyzer/Ews.Analyzer.sln

# Run MCP service tests
dotnet test src/Ews.Code.Analyzer/Ews.Analyzer.McpService.Tests/

# Run analyzer tests
dotnet test src/Ews.Code.Analyzer/Ews.Analyzer.Test/
```

## Conventions

- Target framework: .NET 9 (net9.0) for MCP service, netstandard2.0 for analyzer
- Internal types with `InternalsVisibleTo` for test access
- MCP tools use JSON-RPC 2.0 over stdio
- File access restricted to `PathSecurity` allowlist (current directory by default)
- All maxFiles parameters are clamped to 1–5000
- HTTPS enforced for LLM endpoints (except localhost)
- Error messages are sanitized — no internal paths or stack traces exposed

## Security Rules

- NEVER hardcode API keys, secrets, or credentials in source files
- NEVER commit `.env` files or `appsettings.local*.json`
- Always validate file paths through `PathSecurity.IsPathAllowed()` before any I/O
- Always use `Math.Clamp()` on user-provided numeric limits
- Sanitize error messages before returning to MCP clients
- LLM endpoints must use HTTPS (except localhost/127.0.0.1)
