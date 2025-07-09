# Microsoft Graph API Implementation Guide

This document provides guidance on using the Microsoft Graph API implementation of the IEmailService interface as an alternative to Exchange Web Services (EWS).

## Overview

The `GraphEmailService` class provides a Microsoft Graph API implementation of the `IEmailService` interface, allowing the application to interact with Microsoft 365 mailboxes using modern REST APIs instead of EWS.

## Key Features

- **Modern API**: Uses Microsoft Graph API v1.0 for reliable and future-proof email operations
- **Better Performance**: REST-based API with efficient querying capabilities
- **Enhanced Security**: OAuth 2.0 authentication with granular permissions
- **Cross-Platform**: Works across all platforms supported by .NET 9
- **Easy Migration**: Drop-in replacement for EWS implementation using the same interface

## Configuration

### 1. Application Registration

Register your application in Azure Active Directory:

1. Go to Azure Portal > Azure Active Directory > App registrations
2. Create a new registration or update existing one
3. Configure the following API permissions:
   - `Mail.Read` - Read user's mail
   - `Mail.ReadWrite` - Read and write user's mail  
   - `Mail.Send` - Send mail on behalf of user
4. Grant admin consent for the permissions

### 2. Application Configuration

Update your `appsettings.json`:

```json
{
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "Domain": "your-tenant.onmicrosoft.com",
    "TenantId": "your-tenant-id",
    "ClientId": "your-client-id"
  },
  "MicrosoftGraph": {
    "BaseUrl": "https://graph.microsoft.com/v1.0",
    "Scopes": "Mail.Read Mail.ReadWrite Mail.Send"
  },
  "EmailService": {
    "UseGraphApi": true
  }
}
```

### 3. Dependency Injection Setup

The application automatically configures the appropriate email service based on the `EmailService:UseGraphApi` setting:

```csharp
// In Program.cs - this is automatically handled
var useGraphApi = builder.Configuration.GetValue<bool>("EmailService:UseGraphApi", false);

if (useGraphApi)
{
    builder.Services.AddScoped<IEmailService, GraphEmailService>();
}
else
{
    builder.Services.AddScoped<IEmailService, EwsEmailService>();
}
```

## Usage

The `GraphEmailService` implements the same `IEmailService` interface as the EWS implementation, so no code changes are required in your controllers or business logic.

### Example Usage

```csharp
public class MailController : Controller
{
    private readonly IEmailService _emailService;

    public MailController(IEmailService emailService)
    {
        _emailService = emailService; // Could be EWS or Graph implementation
    }

    public async Task<IActionResult> Index()
    {
        var userEmail = GetCurrentUserEmail();
        var emails = await _emailService.GetInboxEmailsAsync(userEmail, 10);
        return View(emails);
    }
}
```

## API Methods

### GetInboxEmailsAsync

Retrieves emails from the user's inbox with support for:
- Efficient property selection using `$select`
- Ordering by received date
- Configurable page size

```csharp
var emails = await emailService.GetInboxEmailsAsync("user@contoso.com", 20);
```

### GetEmailByIdAsync

Retrieves a specific email by ID:

```csharp
var email = await emailService.GetEmailByIdAsync("message-id", "user@contoso.com");
```

### CreateReplyModelAsync

Creates a pre-populated reply model:

```csharp
var replyModel = await emailService.CreateReplyModelAsync("message-id", "user@contoso.com");
```

### SendReplyAsync

Sends a reply to an email:

```csharp
var success = await emailService.SendReplyAsync(replyModel, "user@contoso.com");
```

## Error Handling

The Graph implementation handles various error scenarios:

- **Authentication Errors**: `MsalUiRequiredException`, `MicrosoftIdentityWebChallengeUserException`
- **Graph API Errors**: `ServiceException` with specific error codes
- **Not Found**: Returns `null` for missing emails
- **Rate Limiting**: Automatic retry logic can be implemented

## Performance Optimizations

### Efficient Data Retrieval

The implementation uses several optimization techniques:

1. **Property Selection**: Only requests required properties using `$select`
2. **Ordering**: Server-side ordering by received date
3. **Minimal Data Transfer**: Uses `bodyPreview` instead of full body for list views

### Caching Strategies

Consider implementing caching for frequently accessed data:

```csharp
// Example: Cache user profile information
[MemoryCache(Duration = 300)] // 5 minutes
public async Task<User> GetCurrentUserAsync()
{
    return await graphServiceClient.Me.GetAsync();
}
```

## Migration from EWS

### Switching Implementations

To switch from EWS to Graph API:

1. Update `appsettings.json`: Set `"EmailService:UseGraphApi": true`
2. Ensure proper Azure AD permissions are configured
3. Test authentication flow
4. Verify email operations work correctly

### Data Model Compatibility

Both implementations use the same domain model (`Contoso.Mail.Models.EmailMessage`), ensuring compatibility:

```csharp
public class EmailMessage
{
    public string Id { get; set; }
    public string Subject { get; set; }
    public string From { get; set; }
    public string FromName { get; set; }
    public DateTime DateTimeReceived { get; set; }
    public string Body { get; set; }
    // ... other properties
}
```

### ID Format Differences

- **EWS**: Uses complex ItemId with UniqueId and ChangeKey
- **Graph**: Uses simple string IDs
- **Handling**: Both are URL-encoded/decoded automatically

## Testing

### Unit Testing

The `GraphEmailService` is fully unit testable using mocking:

```csharp
[Fact]
public async Task GetInboxEmailsAsync_ReturnsEmails()
{
    // Arrange
    var mockGraphClient = Substitute.For<GraphServiceClient>();
    var service = new GraphEmailService(mockGraphClient, logger);
    
    // Setup mock response
    var mockResponse = new MessageCollectionResponse 
    { 
        Value = new List<Message> { /* test data */ } 
    };
    mockGraphClient.Me.Messages.GetAsync(Arg.Any<Action<...>>())
        .Returns(mockResponse);
    
    // Act
    var result = await service.GetInboxEmailsAsync("user@test.com", 10);
    
    // Assert
    Assert.NotEmpty(result);
}
```

### Integration Testing

For integration testing:

1. Use test tenants with sample data
2. Configure test-specific application registrations
3. Use environment-specific configuration

## Security Considerations

### Permissions

- Use the principle of least privilege
- Request only necessary scopes
- Regularly audit permissions

### Token Management

- Tokens are managed by Microsoft Identity Web
- Automatic refresh handling
- Secure token storage

### Data Protection

- No sensitive data in logs
- Proper error handling without data exposure
- GDPR compliance considerations

## Monitoring and Troubleshooting

### Logging

The service includes comprehensive logging:

```csharp
_logger.LogInformation("Retrieving {Count} emails for user {UserEmail}", count, userEmail);
_logger.LogError(ex, "Failed to retrieve emails for user {UserEmail}", userEmail);
```

### Common Issues

1. **Authentication Failures**: Check Azure AD configuration and permissions
2. **Missing Emails**: Verify user has access to mailbox
3. **Rate Limiting**: Implement retry logic with exponential backoff
4. **Network Issues**: Ensure connectivity to graph.microsoft.com

### Debugging Tips

1. Enable detailed logging for Microsoft Graph
2. Use Graph Explorer to test API calls manually
3. Check Azure AD sign-in logs for authentication issues
4. Verify API permissions in Azure Portal

## Best Practices

1. **Error Handling**: Always wrap Graph calls in try-catch blocks
2. **Logging**: Log important operations and errors
3. **Performance**: Use property selection and filtering
4. **Security**: Follow least privilege principle
5. **Testing**: Write comprehensive unit and integration tests

## Future Enhancements

Potential improvements to consider:

1. **Batching**: Implement batch requests for multiple operations
2. **Delta Queries**: Use delta queries for change tracking
3. **Retry Logic**: Add automatic retry with exponential backoff
4. **Caching**: Implement intelligent caching strategies
5. **Real-time Updates**: Use webhooks for real-time notifications

## Support

For issues or questions:

1. Check Microsoft Graph documentation
2. Review application logs
3. Test with Graph Explorer
4. Contact development team for application-specific issues