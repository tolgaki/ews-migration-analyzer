#!/usr/bin/env python3
"""
EWS Migration Analyzer — Python MCP Client Example

Demonstrates how to interact with the EWS Analyzer MCP server programmatically.
The MCP server communicates via JSON-RPC 2.0 over stdio (stdin/stdout).

Prerequisites:
    - .NET 9 SDK installed
    - Built MCP server:
      dotnet build src/Ews.Code.Analyzer/Ews.Analyzer.McpService/Ews.Analyzer.McpService.csproj

Usage:
    python3 samples/mcp-client-example.py

Environment Variables (optional, for Tier 2/3 LLM conversions):
    LLM_ENDPOINT  — API endpoint URL
    LLM_API_KEY   — Your API key
    LLM_MODEL     — Model name (default: gpt-4o)
"""

import json
import subprocess
import sys
import os

# Path to the MCP server project
REPO_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
MCP_PROJECT = os.path.join(
    REPO_ROOT,
    "src", "Ews.Code.Analyzer", "Ews.Analyzer.McpService",
    "Ews.Analyzer.McpService.csproj"
)


class EwsAnalyzerClient:
    """Simple MCP client that communicates with the EWS Analyzer server via stdio."""

    def __init__(self):
        self.process = None
        self._request_id = 0

    def start(self):
        """Start the MCP server subprocess."""
        env = os.environ.copy()
        self.process = subprocess.Popen(
            ["dotnet", "run", "--project", MCP_PROJECT, "--no-build"],
            stdin=subprocess.PIPE,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
            env=env,
        )
        # Send initialize request
        response = self._send("initialize", {})
        server_name = response.get("result", {}).get("serverInfo", {}).get("name", "?")
        print(f"Connected to MCP server: {server_name}")
        return self

    def stop(self):
        """Stop the MCP server."""
        if self.process:
            try:
                self._send("shutdown", {})
            except Exception:
                pass
            self.process.terminate()
            self.process = None

    def _send(self, method: str, params: dict) -> dict:
        """Send a JSON-RPC request and read the response."""
        self._request_id += 1
        request = {
            "jsonrpc": "2.0",
            "id": self._request_id,
            "method": method,
            "params": params,
        }
        line = json.dumps(request) + "\n"
        self.process.stdin.write(line)
        self.process.stdin.flush()

        # Read response line
        response_line = self.process.stdout.readline()
        if not response_line:
            raise RuntimeError("MCP server closed unexpectedly")
        return json.loads(response_line)

    # ── High-level tool wrappers ──────────────────────────────────────

    def list_tools(self) -> list:
        """List all available MCP tools."""
        resp = self._send("tools/list", {})
        return resp.get("result", {}).get("tools", [])

    def analyze_code(self, code: str) -> dict:
        """Analyze inline C# code for EWS usage."""
        resp = self._send("tools/call", {
            "name": "analyzeCode",
            "arguments": {"sources": [{"code": code}]},
        })
        return resp.get("result", {})

    def analyze_file(self, path: str) -> dict:
        """Analyze a file on disk for EWS usage."""
        resp = self._send("tools/call", {
            "name": "analyzeFile",
            "arguments": {"path": os.path.abspath(path)},
        })
        return resp.get("result", {})

    def convert_to_graph(self, code: str, tier: int = None) -> dict:
        """Convert EWS code to Microsoft Graph SDK."""
        args = {"code": code}
        if tier is not None:
            args["tier"] = tier
        resp = self._send("tools/call", {
            "name": "convertToGraph",
            "arguments": args,
        })
        return resp.get("result", {})

    def convert_auth(self, code: str, auth_method: str = "clientCredential") -> dict:
        """Convert EWS authentication to Graph SDK auth."""
        resp = self._send("tools/call", {
            "name": "convertAuth",
            "arguments": {"code": code, "authMethod": auth_method},
        })
        return resp.get("result", {})

    def get_roadmap(self, sdk_qualified_name: str) -> dict:
        """Look up migration roadmap for an EWS operation."""
        resp = self._send("tools/call", {
            "name": "getRoadmap",
            "arguments": {"sdkQualifiedName": sdk_qualified_name},
        })
        return resp.get("result", {})

    def migration_readiness(self, root_path: str, max_files: int = 500) -> dict:
        """Check migration readiness for a project."""
        self.add_allowed_path(root_path)
        resp = self._send("tools/call", {
            "name": "getMigrationReadiness",
            "arguments": {"rootPath": os.path.abspath(root_path), "maxFiles": max_files},
        })
        return resp.get("result", {})

    def add_allowed_path(self, path: str) -> dict:
        """Add a path to the file access allowlist."""
        resp = self._send("tools/call", {
            "name": "addAllowedPath",
            "arguments": {"path": os.path.abspath(path)},
        })
        return resp.get("result", {})


# ── Demo ──────────────────────────────────────────────────────────────

def main():
    print("=" * 60)
    print("EWS Migration Analyzer — Python MCP Client Demo")
    print("=" * 60)
    print()

    # Build first
    print("Building MCP server...")
    build = subprocess.run(
        ["dotnet", "build", MCP_PROJECT, "--configuration", "Release", "--verbosity", "quiet"],
        capture_output=True, text=True,
    )
    if build.returncode != 0:
        print(f"Build failed:\n{build.stderr}")
        sys.exit(1)
    print("Build successful.\n")

    client = EwsAnalyzerClient()
    client.start()

    try:
        # Example 1: List tools
        print("─── Available Tools ───")
        tools = client.list_tools()
        for tool in tools:
            print(f"  {tool['name']:25s} {tool['description']}")
        print()

        # Example 2: Analyze a code snippet
        print("─── Analyzing EWS Code ───")
        ews_code = """
        using Microsoft.Exchange.WebServices.Data;

        var service = new ExchangeService(ExchangeVersion.Exchange2013);
        service.Credentials = new WebCredentials("user@contoso.com", "password");
        var results = service.FindItems(WellKnownFolderName.Inbox, new ItemView(50));
        """
        analysis = client.analyze_code(ews_code)
        print(json.dumps(analysis, indent=2))
        print()

        # Example 3: Convert EWS to Graph
        print("─── Converting to Graph SDK ───")
        conversion = client.convert_to_graph(
            "service.FindItems(WellKnownFolderName.Inbox, new ItemView(50))"
        )
        print(json.dumps(conversion, indent=2))
        print()

        # Example 4: Convert authentication
        print("─── Converting Authentication ───")
        auth_result = client.convert_auth(
            'var service = new ExchangeService(); service.Credentials = new WebCredentials("user", "pass");',
            auth_method="clientCredential",
        )
        print(json.dumps(auth_result, indent=2))
        print()

        # Example 5: Look up roadmap
        print("─── Roadmap Lookup ───")
        roadmap = client.get_roadmap(
            "Microsoft.Exchange.WebServices.Data.ExchangeService.FindItems"
        )
        print(json.dumps(roadmap, indent=2))
        print()

    finally:
        client.stop()
        print("Done.")


if __name__ == "__main__":
    main()
