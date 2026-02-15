# Convert EWS Authentication to Graph

Convert EWS authentication patterns (ExchangeService, WebCredentials) to Microsoft Graph SDK authentication.

## Instructions

1. Read the code provided: $ARGUMENTS
2. Identify the current EWS authentication pattern:
   - `WebCredentials` (username/password)
   - `OAuthCredentials` (OAuth token)
   - `X509CertificateCredentials` (certificate)
   - Autodiscover URL configuration
3. Suggest the appropriate Graph SDK authentication replacement:

   **For service/daemon apps (no user):**
   ```csharp
   var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
   var graphClient = new GraphServiceClient(credential, new[] { "https://graph.microsoft.com/.default" });
   ```

   **For interactive user apps:**
   ```csharp
   var credential = new InteractiveBrowserCredential(new InteractiveBrowserCredentialOptions
   {
       TenantId = tenantId, ClientId = clientId, RedirectUri = new Uri("http://localhost")
   });
   var graphClient = new GraphServiceClient(credential, new[] { "Mail.Read", "Mail.Send" });
   ```

   **For device code flow:**
   ```csharp
   var credential = new DeviceCodeCredential(new DeviceCodeCredentialOptions
   {
       TenantId = tenantId, ClientId = clientId,
       DeviceCodeCallback = (code, _) => { Console.WriteLine(code.Message); return Task.CompletedTask; }
   });
   var graphClient = new GraphServiceClient(credential);
   ```

   **For managed identity (Azure hosted):**
   ```csharp
   var credential = new ManagedIdentityCredential();
   var graphClient = new GraphServiceClient(credential, new[] { "https://graph.microsoft.com/.default" });
   ```

4. List the required NuGet packages:
   - `Microsoft.Graph` (>= 5.0.0)
   - `Azure.Identity` (>= 1.10.0)

5. List the required Azure AD app registration changes (permissions, redirect URIs)
6. Show the complete before/after code transformation
