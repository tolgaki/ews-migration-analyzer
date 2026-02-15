#!/bin/bash
# ==============================================================================
# EWS Migration Analyzer — CI/CD Automation Script
# ==============================================================================
#
# This script demonstrates how to run the EWS Migration Analyzer MCP server
# in standalone mode for CI/CD pipelines. It sends JSON-RPC requests via stdin
# and reads responses from stdout.
#
# Prerequisites:
#   - .NET 9 SDK installed
#   - Built MCP server: dotnet build src/Ews.Code.Analyzer/Ews.Analyzer.McpService/Ews.Analyzer.McpService.csproj
#
# Usage:
#   ./ci-automation.sh /path/to/your/project
#
# Environment Variables (optional, for Tier 2/3 LLM conversions):
#   LLM_ENDPOINT  — API endpoint URL (must use HTTPS)
#   LLM_API_KEY   — Your API key
#   LLM_MODEL     — Model name (default: gpt-4o)
# ==============================================================================

set -euo pipefail

PROJECT_PATH="${1:-.}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
MCP_PROJECT="$REPO_ROOT/src/Ews.Code.Analyzer/Ews.Analyzer.McpService/Ews.Analyzer.McpService.csproj"

echo "=== EWS Migration Analyzer CI ==="
echo "Analyzing: $PROJECT_PATH"
echo ""

# Build the MCP server if needed
echo "Building MCP server..."
dotnet build "$MCP_PROJECT" --configuration Release --verbosity quiet 2>&1 || {
    echo "ERROR: Failed to build MCP server"
    exit 1
}

# Helper function: send a JSON-RPC request and capture the response
send_request() {
    local request="$1"
    echo "$request" | dotnet run --project "$MCP_PROJECT" --no-build --configuration Release 2>/dev/null | head -1
}

# Step 1: Initialize
echo "Step 1: Initializing..."
INIT_RESPONSE=$(send_request '{"jsonrpc":"2.0","id":0,"method":"initialize","params":{}}')
echo "Server info: $(echo "$INIT_RESPONSE" | python3 -c "import sys,json; d=json.load(sys.stdin); print(d.get('result',{}).get('serverInfo',{}).get('name','unknown'))" 2>/dev/null || echo "connected")"
echo ""

# Step 2: Add the project path to the allowlist and check migration readiness
echo "Step 2: Checking migration readiness..."
READINESS_REQUEST=$(cat <<EOF
{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"addAllowedPath","arguments":{"path":"$PROJECT_PATH"}}}
{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"getMigrationReadiness","arguments":{"rootPath":"$PROJECT_PATH","maxFiles":1000}}}
EOF
)
# Send both requests (the server processes line by line)
READINESS=$(echo "$READINESS_REQUEST" | dotnet run --project "$MCP_PROJECT" --no-build --configuration Release 2>/dev/null | tail -1)
echo "Migration readiness result:"
echo "$READINESS" | python3 -m json.tool 2>/dev/null || echo "$READINESS"
echo ""

# Step 3: Run automatic conversion (dry-run mode)
echo "Step 3: Converting EWS code to Graph SDK (dry-run)..."
CONVERT_REQUEST=$(cat <<EOF
{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"addAllowedPath","arguments":{"path":"$PROJECT_PATH"}}}
{"jsonrpc":"2.0","id":4,"method":"tools/call","params":{"name":"convertToGraph","arguments":{"rootPath":"$PROJECT_PATH","maxFiles":200,"dryRun":true}}}
EOF
)
CONVERT=$(echo "$CONVERT_REQUEST" | dotnet run --project "$MCP_PROJECT" --no-build --configuration Release 2>/dev/null | tail -1)
echo "Conversion results:"
echo "$CONVERT" | python3 -m json.tool 2>/dev/null || echo "$CONVERT"
echo ""

echo "=== CI Analysis Complete ==="
echo ""
echo "To apply conversions, use the applyConversion tool with the conversion results above."
echo "Always review the diffs before applying in production codebases."
