# Run Tests

Build the solution and run all tests for the EWS Migration Analyzer.

## Instructions

1. Build the full solution:
   ```bash
   dotnet build src/Ews.Code.Analyzer/Ews.Analyzer.sln
   ```

2. Run the MCP service tests:
   ```bash
   dotnet test src/Ews.Code.Analyzer/Ews.Analyzer.McpService.Tests/ --verbosity normal
   ```

3. Run the analyzer tests:
   ```bash
   dotnet test src/Ews.Code.Analyzer/Ews.Analyzer.Test/ --verbosity normal
   ```

4. Report results:
   - Total tests run
   - Passed / Failed / Skipped counts
   - If any tests failed, read the failing test file and the source file it tests, diagnose the issue, and suggest a fix

If $ARGUMENTS contains "fix", also attempt to fix any failing tests.
