# EWS Analyzer MCP Service

## Overview

The EWS Analyzer MCP (Model Context Protocol) Service provides a JSON-RPC 2.0 compliant interface for analyzing C# code and retrieving migration roadmaps for Exchange Web Services (EWS) operations. This service helps identify EWS usage in code and provides guidance for migrating to Microsoft Graph API.

## Protocol

This service implements the **Model Context Protocol (MCP)** using **newline-delimited JSON** over stdin/stdout. Each request and response is a single JSON object on its own line, following the JSON-RPC 2.0 specification.

### Supported JSON-RPC 2.0 Features

- **Standard requests** with `id` field that expect responses
- **Notifications** without `id` field that don't expect responses  
- **Proper error handling** with structured error objects
- **Mutually exclusive result vs error** fields in responses

## Launching the Service

### Prerequisites

- .NET 8.0 or later
- EWS Analyzer project built successfully

### Building

```bash
cd src/Ews.Code.Analyzer/Ews.Analyzer.McpService
dotnet build
```

### Running

```bash
# From the project directory
dotnet run

# Or from the built binary
dotnet bin/Debug/net8.0/Ews.Analyzer.McpService.dll
```

## Interacting with the Service

The service reads JSON-RPC 2.0 requests from stdin and writes responses to stdout. Each request/response must be on a single line.

### Available Methods

#### 1. initialize

Initializes the MCP session.

**Request:**
```json
{"jsonrpc": "2.0", "id": 1, "method": "initialize", "params": {}}
```

**Response:**
```json
{"jsonrpc": "2.0", "id": 1, "result": {"capabilities": {}}}
```

#### 2. tools/list

Returns available tools.

**Request:**
```json
{"jsonrpc": "2.0", "id": 2, "method": "tools/list"}
```

**Response:**
```json
{
  "jsonrpc": "2.0",
  "id": 2,
  "result": {
    "tools": [
      {
        "name": "analyzeCode",
        "description": "Run EWS analyzer on provided C# source code",
        "inputSchema": {
          "type": "object",
          "properties": {
            "code": {
              "type": "string",
              "description": "C# source code to analyze"
            }
          },
          "required": ["code"]
        }
      },
      {
        "name": "getRoadmap",
        "description": "Return migration roadmap for an EWS operation",
        "inputSchema": {
          "type": "object",
          "properties": {
            "operation": {
              "type": "string",
              "description": "EWS operation name"
            }
          },
          "required": ["operation"]
        }
      }
    ]
  }
}
```

#### 3. tools/call

Executes a tool with specified arguments.

##### analyzeCode Tool

Analyzes C# code for EWS usage patterns.

**Request:**
```json
{
  "jsonrpc": "2.0",
  "id": 3,
  "method": "tools/call",
  "params": {
    "name": "analyzeCode",
    "arguments": {
      "code": "using Microsoft.Exchange.WebServices.Data;\nclass Test { void Method() { var service = new ExchangeService(); } }"
    }
  }
}
```

**Response:**
```json
{
  "jsonrpc": "2.0",
  "id": 3,
  "result": {
    "content": [
      {
        "type": "text",
        "text": "Analysis completed",
        "diagnostics": [
          {
            "id": "EWS001",
            "message": "Exchange Web Services usage detected",
            "severity": "Warning",
            "location": {
              "line": 2,
              "column": 45,
              "endLine": 2,
              "endColumn": 60
            },
            "descriptor": {
              "id": "EWS001",
              "title": "EWS Usage",
              "description": "...",
              "category": "Migration",
              "helpLinkUri": "https://..."
            }
          }
        ]
      }
    ]
  }
}
```

##### getRoadmap Tool

Retrieves migration roadmap for a specific EWS operation.

**Request:**
```json
{
  "jsonrpc": "2.0",
  "id": 4,
  "method": "tools/call",
  "params": {
    "name": "getRoadmap",
    "arguments": {
      "operation": "FindItems"
    }
  }
}
```

**Response:**
```json
{
  "jsonrpc": "2.0",
  "id": 4,
  "result": {
    "content": [
      {
        "type": "text",
        "text": "Roadmap retrieved",
        "roadmap": {
          "operation": "FindItems",
          "graphEquivalent": "GET /me/messages",
          "status": "Available",
          "notes": "Migration guidance..."
        }
      }
    ]
  }
}
```

#### 4. shutdown

Gracefully shuts down the service. Can be sent as a request or notification.

**Request:**
```json
{"jsonrpc": "2.0", "id": 5, "method": "shutdown"}
```

**Notification:**
```json
{"jsonrpc": "2.0", "method": "shutdown"}
```

### Error Handling

The service follows JSON-RPC 2.0 error handling conventions:

#### Parse Error
```json
{
  "jsonrpc": "2.0",
  "id": null,
  "error": {
    "code": -32700,
    "message": "Parse error",
    "data": "Invalid JSON received"
  }
}
```

#### Invalid Request
```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "error": {
    "code": -32600,
    "message": "Invalid Request",
    "data": "Missing 'method' field"
  }
}
```

#### Method Not Found
```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "error": {
    "code": -32601,
    "message": "Method not found",
    "data": "Unknown method: invalidMethod"
  }
}
```

#### Internal Error
```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "error": {
    "code": -32603,
    "message": "Internal error",
    "data": "Analysis failed: compilation error"
  }
}
```

## Example Session

```bash
# Start the service
echo '{"jsonrpc": "2.0", "id": 1, "method": "initialize", "params": {}}' | dotnet run

# List available tools
echo '{"jsonrpc": "2.0", "id": 2, "method": "tools/list"}' | dotnet run

# Analyze some code
echo '{"jsonrpc": "2.0", "id": 3, "method": "tools/call", "params": {"name": "analyzeCode", "arguments": {"code": "using Microsoft.Exchange.WebServices.Data; class Test { }"}}}' | dotnet run

# Get roadmap
echo '{"jsonrpc": "2.0", "id": 4, "method": "tools/call", "params": {"name": "getRoadmap", "arguments": {"operation": "FindItems"}}}' | dotnet run

# Shutdown (notification - no response expected)
echo '{"jsonrpc": "2.0", "method": "shutdown"}' | dotnet run
```

## Technical Details

### Input Validation

- All JSON input is validated for proper structure
- Required fields are checked before processing
- Argument types are validated against expected schemas
- Comprehensive error messages are provided for invalid input

### Code Analysis Features

- **Expanded metadata references**: The analyzer now includes additional .NET runtime assemblies for more accurate analysis
- **Structured diagnostic objects**: Returns detailed diagnostic information with location, severity, and descriptor metadata
- **Robust error handling**: Compilation and analysis errors are caught and reported properly

### JSON-RPC 2.0 Compliance

- **Proper response structure**: Uses mutually exclusive `result` and `error` fields
- **Notification support**: Handles requests without `id` field appropriately  
- **Standard error codes**: Implements standard JSON-RPC 2.0 error codes
- **Version validation**: Validates `jsonrpc: "2.0"` field on all requests

## Integration

This service is designed to be used by:

- MCP-compatible clients
- IDEs and editors with MCP support
- Development tools requiring EWS analysis capabilities
- CI/CD pipelines for automated code analysis

The newline-delimited JSON protocol makes it easy to integrate with shell scripts, streaming processors, and other tools that work with line-based input/output.