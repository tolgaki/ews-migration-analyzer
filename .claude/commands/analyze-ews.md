# Analyze EWS Usage

Analyze the provided C# file or directory for EWS SDK usage and report migration readiness.

## Instructions

1. Read the file or directory path provided by the user: $ARGUMENTS
2. If it's a single `.cs` file, read it and identify all EWS SDK calls (anything from `Microsoft.Exchange.WebServices.*`)
3. If it's a directory, find all `*.cs` files and analyze each one
4. For each EWS usage found, report:
   - File path and line number
   - The EWS SDK method/class being used
   - Whether a Microsoft Graph equivalent exists (check the roadmap at `src/Ews.Code.Analyzer/Ews.Analyzer/roadmap.json`)
   - Migration difficulty: Easy (1:1 mapping), Medium (structural changes needed), Hard (no direct equivalent)
5. Provide a summary with:
   - Total EWS references found
   - Migration readiness percentage (references with Graph equivalents / total)
   - Recommended migration order (start with Easy conversions)

If no path is provided, analyze the current working directory.
