# Suggest Graph SDK Replacements

Generate specific code fix suggestions for each EWS usage in the project.

## Instructions

1. Scan: $ARGUMENTS (default: current directory)
2. For each EWS SDK call found:
   - Show the original line with file path and line number
   - Provide the Graph SDK replacement code
   - Include required usings
   - Rate confidence level
3. Group suggestions by file
4. For each file, generate a unified diff that could be applied
5. At the end, summarize:
   - Total suggestions generated
   - High/Medium/Low confidence breakdown
   - Files that need manual review
