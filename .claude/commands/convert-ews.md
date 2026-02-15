# Convert EWS to Graph

Convert EWS SDK code to Microsoft Graph SDK v5+ equivalent.

## Instructions

1. Read the file or code snippet provided: $ARGUMENTS
2. Identify all EWS SDK calls in the code
3. For each EWS call, generate the Graph SDK v5+ replacement:
   - Use `Microsoft.Graph` NuGet package (v5+)
   - Use `Azure.Identity` for authentication
   - Include all required `using` statements
   - Follow async/await patterns
4. Show the conversion as a before/after diff
5. Rate each conversion's confidence:
   - **High**: Direct 1:1 mapping, well-documented
   - **Medium**: Requires structural changes but path is clear
   - **Low**: Complex migration, may need manual review
6. List any required NuGet packages to add
7. Note any EWS features that don't have Graph equivalents yet

Reference the migration roadmap at `src/Ews.Code.Analyzer/Ews.Analyzer/roadmap.json` for operation mappings.

## Example Output Format

```
### Conversion 1: FindItems → Messages.GetAsync
**Confidence:** High
**Original (EWS):**
  service.FindItems(WellKnownFolderName.Inbox, new ItemView(50))
**Replacement (Graph SDK v5):**
  await graphClient.Me.Messages.GetAsync(c => { c.QueryParameters.Top = 50; })
**Required usings:** Microsoft.Graph, Microsoft.Graph.Models
**Required package:** Microsoft.Graph (>= 5.0.0)
```
