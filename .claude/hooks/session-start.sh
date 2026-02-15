#!/bin/bash
set -euo pipefail

# EWS Migration Analyzer — SessionStart Hook
# Installs .NET SDK and restores NuGet packages for Claude Code on the web.

# Only run in remote (web) environments
if [ "${CLAUDE_CODE_REMOTE:-}" != "true" ]; then
  exit 0
fi

PROJECT_DIR="${CLAUDE_PROJECT_DIR:-$(pwd)}"
SOLUTION="$PROJECT_DIR/src/Ews.Code.Analyzer/Ews.Analyzer.sln"
MCP_PROJECT="$PROJECT_DIR/src/Ews.Code.Analyzer/Ews.Analyzer.McpService/Ews.Analyzer.McpService.csproj"

# Install .NET SDK 8.0 if not available
if ! command -v dotnet &>/dev/null; then
  echo "Installing .NET SDK 8.0..."
  curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --channel 8.0 --install-dir "$HOME/.dotnet"
  echo "export DOTNET_ROOT=\"$HOME/.dotnet\"" >> "$CLAUDE_ENV_FILE"
  echo "export PATH=\"$HOME/.dotnet:\$PATH\"" >> "$CLAUDE_ENV_FILE"
  export DOTNET_ROOT="$HOME/.dotnet"
  export PATH="$HOME/.dotnet:$PATH"
fi

echo ".NET SDK version: $(dotnet --version)"

# Restore NuGet packages for the solution
echo "Restoring NuGet packages..."
dotnet restore "$SOLUTION" --verbosity quiet

# Build the solution to verify everything compiles
echo "Building solution..."
dotnet build "$SOLUTION" --no-restore --verbosity quiet

echo "SessionStart hook complete."
