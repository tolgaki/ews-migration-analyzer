# Check Migration Readiness

Assess how ready a codebase is to migrate from EWS to Microsoft Graph.

## Instructions

1. Scan the project at the path provided: $ARGUMENTS (default: current directory)
2. Find all `*.cs` files in the project
3. Identify every EWS SDK reference:
   - `Microsoft.Exchange.WebServices.Data.*` calls
   - `ExchangeService` initialization
   - EWS authentication patterns
   - EWS-specific types (EmailMessage, Appointment, Contact, etc.)
4. Cross-reference each usage against the migration roadmap (`src/Ews.Code.Analyzer/Ews.Analyzer/roadmap.json`)
5. Categorize each reference:
   - **Ready** — Graph API equivalent is GA (Available)
   - **Preview** — Graph API equivalent is in Preview
   - **Blocked** — No Graph API equivalent yet (Gap/TBD)
6. Produce a readiness report:

```
## Migration Readiness Report

**Project:** [path]
**Files scanned:** X
**Total EWS references:** Y

### Readiness Score: XX%

| Category | Count | Percentage |
|----------|-------|-----------|
| Ready (GA) | X | X% |
| Preview | X | X% |
| Blocked (Gap) | X | X% |

### Blocked Operations (need workarounds)
- [List each blocked operation with explanation]

### Recommended Migration Order
1. [Start with highest-confidence conversions]
2. [Then medium-confidence]
3. [Address gaps last]

### Required NuGet Packages
- Microsoft.Graph (>= 5.0.0)
- Azure.Identity (>= 1.10.0)

### Estimated Effort
- Tier 1 (automatic): X references
- Tier 2 (LLM-assisted): X references
- Tier 3 (manual review): X references
```
