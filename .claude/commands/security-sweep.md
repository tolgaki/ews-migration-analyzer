# Security Sweep

Perform a security review of the EWS Migration Analyzer codebase.

## Instructions

1. Scan: $ARGUMENTS (default: src/Ews.Code.Analyzer/)
2. Check for these security concerns:

   **Credential Exposure:**
   - Hardcoded API keys, passwords, tokens, or secrets
   - Secrets in error messages or log output
   - API keys sent over non-HTTPS connections

   **Path Traversal:**
   - File operations without PathSecurity validation
   - StartsWith checks without directory separator enforcement
   - User-supplied paths passed directly to File.ReadAllText/WriteAllText

   **Input Validation:**
   - Missing bounds checking on numeric parameters (maxFiles, etc.)
   - Unbounded string inputs
   - Missing null/empty checks on required parameters

   **Information Disclosure:**
   - Stack traces in error responses
   - Internal file paths in error messages
   - Raw API responses returned to clients

   **Injection Risks:**
   - Unsanitized user input in LLM prompts
   - Template substitution without validation
   - Command injection vectors

3. For each finding, report:
   - File path and line number
   - Severity: Critical / High / Medium / Low
   - Description of the vulnerability
   - Recommended fix with code example

4. Summary table of all findings sorted by severity
