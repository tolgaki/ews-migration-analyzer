# Microsoft Graph Authentication Fix

## Issue
The `GraphEmailService` was failing with the error "Access token is empty" because the `GraphServiceClient` was not properly configured with authentication.

## Root Cause
In the original `Program.cs`, the `GraphServiceClient` was being created with just a basic `HttpClient` without any authentication provider:

```csharp
// Previous incorrect implementation
builder.Services.AddScoped<GraphServiceClient>(provider =>
{
    var httpClient = new HttpClient();
    return new GraphServiceClient(httpClient);
});
```

This created a GraphServiceClient that had no way to acquire or attach access tokens to requests.

## Solution
The fix involved two key changes:

### 1. Added Required NuGet Package
Added the `Microsoft.Identity.Web.GraphServiceClient` package which provides the integration between Microsoft Identity Web and Microsoft Graph SDK.

```bash
dotnet add package Microsoft.Identity.Web.GraphServiceClient
```

### 2. Updated Program.cs Configuration
Changed the authentication chain to properly integrate Microsoft Graph:

```csharp
// Updated correct implementation
builder.Services
    .AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"))
    .EnableTokenAcquisitionToCallDownstreamApi()
    .AddMicrosoftGraph(builder.Configuration.GetSection("MicrosoftGraph"))  // This line was key
    .AddInMemoryTokenCaches();
```

The `.AddMicrosoftGraph()` method:
- Automatically registers a properly authenticated `GraphServiceClient` in the DI container
- Configures the authentication provider to use tokens acquired by Microsoft Identity Web
- Handles token acquisition, caching, and refresh automatically
- Uses the configuration from the "MicrosoftGraph" section in appsettings.json

### 3. Removed Manual GraphServiceClient Registration
Since `.AddMicrosoftGraph()` automatically registers the `GraphServiceClient`, we removed the manual registration that was creating an unauthenticated client.

## Configuration Requirements
The solution requires proper configuration in `appsettings.json`:

```json
{
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "Domain": "{Tenant Name}.onmicrosoft.com",
    "TenantId": "{Your Tenant ID}",
    "ClientId": "{Your Client ID}"
  },
  "MicrosoftGraph": {
    "BaseUrl": "https://graph.microsoft.com/v1.0",
    "Scopes": "Mail.Read Mail.ReadWrite Mail.Send"
  }
}
```

## How It Works Now
1. When a user authenticates via Azure AD, Microsoft Identity Web acquires tokens with the specified scopes
2. The `GraphServiceClient` automatically includes these tokens in all Microsoft Graph API calls
3. Tokens are cached and refreshed automatically as needed
4. The `GraphEmailService` can now successfully make authenticated calls to Microsoft Graph

## Testing
To verify the fix works:
1. Ensure your Azure AD app registration has the required API permissions (Mail.Read, Mail.ReadWrite, Mail.Send)
2. Update appsettings.json with your actual tenant and client IDs
3. Set `"EmailService:UseGraphApi": true` in configuration
4. Run the application and authenticate - Graph API calls should now work properly

## Benefits
- Automatic token management (acquisition, caching, refresh)
- Follows Microsoft recommended practices for Graph API authentication
- Handles complex authentication scenarios (incremental consent, conditional access, etc.)
- Integrates seamlessly with ASP.NET Core authentication pipeline