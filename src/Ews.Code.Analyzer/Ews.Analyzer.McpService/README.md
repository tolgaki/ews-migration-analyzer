# EWS Analyzer MCP Service

This is a Model Context Protocol (MCP) server that provides EWS (Exchange Web Services) analysis and migration guidance capabilities.

## Overview

The McpService exposes the EWS Analyzer functionality through a JSON-RPC interface, allowing AI assistants and other tools to analyze C# code for EWS usage and get migration roadmaps to Microsoft Graph API.

## Features

### Tools Available

1. **analyzeCode** - Analyzes C# source code for EWS usage
   - Input: `code` (string) - C# source code to analyze
   - Output: Array of diagnostic messages with EWS-related findings

2. **getRoadmap** - Provides migration guidance for EWS operations
   - Input: `operation` (string) - EWS operation name
   - Output: Migration roadmap with Graph API alternatives

## Usage

### Running the Service

```bash
cd src/Ews.Code.Analyzer/Ews.Analyzer.McpService
dotnet run
```

The service reads JSON-RPC requests from stdin and writes responses to stdout.

### Example Requests

#### Initialize
```json
{"jsonrpc": "2.0", "id": 1, "method": "initialize"}
```

#### List Available Tools
```json
{"jsonrpc": "2.0", "id": 2, "method": "tools/list"}
```

#### Analyze Code
```json
{
  "jsonrpc": "2.0", 
  "id": 3, 
  "method": "tools/call", 
  "params": {
    "name": "analyzeCode", 
    "arguments": {
      "code": "using Microsoft.Exchange.WebServices.Data; class Test { void Method() { var service = new ExchangeService(); } }"
    }
  }
}
```

#### Get Migration Roadmap
```json
{
  "jsonrpc": "2.0", 
  "id": 4, 
  "method": "tools/call", 
  "params": {
    "name": "getRoadmap", 
    "arguments": {
      "operation": "ConvertId"
    }
  }
}
```

#### Shutdown
```json
{"jsonrpc": "2.0", "id": 5, "method": "shutdown"}
```

## Error Handling

The service provides comprehensive error handling:

- **Parse errors** (code -32700): Invalid JSON input
- **Internal errors** (code -32603): Unexpected exceptions
- **Tool errors**: Wrapped in response content with error details

## Implementation Details

### Key Improvements Made

1. **Fixed CompilationWithAnalyzers Constructor**: Updated to use the correct constructor with AnalyzerOptions parameter
2. **Enhanced Error Handling**: Added try-catch blocks for JSON parsing and tool execution
3. **Improved JSON-RPC Compliance**: Proper error codes and response formatting
4. **Null Safety**: Fixed nullable reference type warnings
5. **Sample Data**: Added realistic EWS-to-Graph mapping data

### Code Quality Features

- Comprehensive exception handling
- Proper resource disposal with `using` statements
- Standard JSON-RPC 2.0 protocol compliance
- Informative error messages
- Graceful handling of unknown operations

## Dependencies

- .NET 8.0
- Microsoft.CodeAnalysis.CSharp 4.11.0
- Ews.Analyzer (project reference)

## Notes

- The service processes one JSON-RPC request per line
- Unknown EWS operations return a fallback roadmap with helpful guidance
- The analyzer provides detailed diagnostics about EWS usage patterns
- All responses follow the MCP (Model Context Protocol) format