# Conversation Starters

Ready-to-use prompts for GitHub Copilot Chat and Claude Code. Copy-paste these to get started quickly.

---

## Quick Analysis

### "What EWS code do I have?"
```
Analyze my project for EWS SDK usage. Scan all C# files and tell me:
- How many EWS references exist
- Which files contain them
- Which operations are used (mail, calendar, contacts, etc.)
- What percentage can be automatically migrated to Graph SDK
```

### "Is my project ready to migrate?"
```
Check the migration readiness of my project. For each EWS operation found, tell me whether
a Microsoft Graph equivalent exists (Available, Preview, or Gap). Give me a readiness percentage.
```

---

## Code Conversion

### "Convert this file to Graph SDK"
```
Read [path/to/file.cs] and convert all EWS SDK calls to Microsoft Graph SDK v5+ equivalents.
For each conversion:
1. Show the original EWS code
2. Show the Graph SDK replacement
3. Rate confidence (high/medium/low)
4. List required using statements and NuGet packages
Show as a diff I can review before applying.
```

### "Convert my authentication setup"
```
I'm using ExchangeService with WebCredentials for a daemon/service application.
Convert my authentication to use Azure.Identity with ClientSecretCredential
and GraphServiceClient. Show the complete replacement including:
- NuGet packages to add
- New using statements
- Azure AD app registration changes needed
- The complete GraphServiceClient initialization code
```

### "Convert just the easy stuff first"
```
Convert only the Tier 1 (deterministic, high-confidence) EWS operations in my project to Graph SDK.
Skip anything that needs LLM assistance. I want to start with the safest, most reliable conversions.
Show me the diffs but don't apply them yet.
```

### "What can't be automatically converted?"
```
Analyze my EWS code and identify operations that:
1. Have no Graph SDK equivalent yet (Gap/TBD)
2. Require complex refactoring beyond simple API mapping
3. Use EWS features with no 1:1 Graph match
For each one, explain what the workaround or alternative approach would be.
```

---

## Migration Planning

### "Create a migration plan"
```
Help me create a step-by-step migration plan for moving my EWS application to Microsoft Graph:
1. Analyze the current EWS usage
2. Identify the migration order (easy → medium → hard)
3. List the NuGet packages I need to add
4. Identify authentication changes needed
5. Flag any EWS operations without Graph equivalents
6. Estimate the effort for each phase
```

### "What NuGet packages do I need?"
```
Based on the EWS operations in my project, what Microsoft Graph NuGet packages do I need?
List each package with the minimum version and what it's used for.
```

---

## Authentication Migration

### "Interactive user app auth"
```
Convert my EWS app's authentication from WebCredentials to Azure.Identity
for an interactive desktop/web application. Use InteractiveBrowserCredential.
Show the complete auth setup including scopes for Mail.Read, Mail.Send, and Calendars.ReadWrite.
```

### "Device code flow"
```
Set up Graph SDK authentication using device code flow (for CLI tools or devices without a browser).
Show the complete setup with DeviceCodeCredential and proper scope configuration.
```

### "Managed identity (Azure hosted)"
```
My app runs in Azure (App Service / Functions). Convert the auth to use ManagedIdentityCredential
so it doesn't need any stored secrets. Show the GraphServiceClient setup.
```

---

## Testing & Validation

### "Help me test the migration"
```
After converting my EWS code to Graph SDK, help me:
1. Create unit tests for the new Graph SDK code
2. Create integration test stubs
3. Verify the Graph SDK calls use correct endpoints
4. Check that error handling is properly migrated
```

### "Run the analyzer tests"
```
Build the EWS Analyzer solution and run all tests. Report any failures and suggest fixes.
```

---

## Advanced Usage

### "Batch convert with review"
```
Convert all EWS code in src/Services/ to Graph SDK. For each file:
1. Show the diff
2. Wait for my approval before moving to the next file
3. Create .bak backups of every modified file
4. After all conversions, run the tests to check for regressions
```

### "Compare EWS and Graph approaches"
```
For the EWS operation [X], show me side-by-side:
- The EWS implementation
- The Graph SDK implementation
- Key differences in behavior, error handling, and pagination
- Any limitations of the Graph approach
```
