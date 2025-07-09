# GitHub Copilot Instructions for Contoso.Mail.Web

## Project Overview

Contoso.Mail.Web is an ASP.NET Core Razor Pages application that provides a web interface for viewing and replying to emails using Microsoft Graph API. The application demonstrates modern email integration using Microsoft's Graph API platform.

### Key Features

- User authentication via Microsoft Identity Platform (Azure AD)
- Email listing from user's inbox using Microsoft Graph API
- Ability to reply to emails using Microsoft Graph API
- End-to-end (E2E) testing with Playwright

## Solution Architecture

The solution consists of the following projects:

1. **Contoso.Mail.Web** - The main Razor Pages web application
2. **AppHost** - Application host project for .NET Aspire
3. **ServiceDefaults** - Common service configuration defaults

### Key Technologies

- **.NET 9** - Target framework
- **ASP.NET Core Razor Pages** - Web application framework
- **Microsoft Identity Web** - Authentication and authorization
- **Microsoft Graph API** - Email integration
- **Playwright** - End-to-end testing framework
- **TypeScript** - Used for Playwright test implementation
- **xUnit** - Unit testing framework
- **NSubstitute** - Mocking Framework

## Microsoft Graph API Best Practices

Follow these best practices when implementing Graph API functionality:

### 1. Authentication and Authorization

#### Use Microsoft Identity Platform
- Use `Microsoft.Identity.Web` for authentication in ASP.NET Core applications
- Implement proper token acquisition and caching
- Use incremental consent for scopes when possible
// Configure Microsoft Identity Web in Program.cs
builder.Services
    .AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"))
    .EnableTokenAcquisitionToCallDownstreamApi()
    .AddMicrosoftGraph(builder.Configuration.GetSection("MicrosoftGraph"))
    .AddInMemoryTokenCaches();
#### Scope Management
- Request only the minimum required permissions
- Use application permissions sparingly and only when delegated permissions are insufficient
- Common mail scopes:
  - `Mail.Read` - Read user's mail
  - `Mail.ReadWrite` - Read and write user's mail
  - `Mail.Send` - Send mail on behalf of user

### 2. GraphServiceClient Configuration

#### Dependency Injection Setup
// Register GraphServiceClient with proper authentication
builder.Services.AddScoped<GraphServiceClient>(provider =>
{
    var tokenAcquisition = provider.GetRequiredService<ITokenAcquisition>();
    var authProvider = new TokenAcquisitionAuthenticationProvider(tokenAcquisition, scopes);
    return new GraphServiceClient(authProvider);
});
#### Error Handling
- Always wrap Graph API calls in try-catch blocks
- Handle specific Graph exceptions:
  - `ServiceException` for Graph API errors
  - `MsalUiRequiredException` for authentication challenges
  - `MicrosoftIdentityWebChallengeUserException` for re-authentication
try
{
    var messages = await graphServiceClient.Me.Messages
        .GetAsync(requestConfiguration => requestConfiguration.QueryParameters.Top = count);
    return messages.Value;
}
catch (ServiceException ex) when (ex.Error.Code == "ItemNotFound")
{
    return null;
}
catch (MsalUiRequiredException)
{
    // Handle re-authentication
    throw;
}
### 3. Efficient Data Retrieval

#### Use $select to limit returned properties
var messages = await graphServiceClient.Me.Messages
    .GetAsync(requestConfiguration =>
    {
        requestConfiguration.QueryParameters.Select = ["id", "subject", "from", "receivedDateTime", "bodyPreview"];
        requestConfiguration.QueryParameters.Top = 10;
    });
#### Use $filter for server-side filtering
var unreadMessages = await graphServiceClient.Me.Messages
    .GetAsync(requestConfiguration =>
    {
        requestConfiguration.QueryParameters.Filter = "isRead eq false";
        requestConfiguration.QueryParameters.Top = 50;
    });
#### Implement Pagination
var allMessages = new List<Message>();
var messages = await graphServiceClient.Me.Messages.GetAsync();

while (messages?.Value?.Count > 0)
{
    allMessages.AddRange(messages.Value);
    
    if (messages.OdataNextLink != null)
    {
        messages = await graphServiceClient.Me.Messages
            .WithUrl(messages.OdataNextLink)
            .GetAsync();
    }
    else
    {
        break;
    }
}
### 4. Request Optimization

#### Batch Requests
Use batch requests for multiple operations:
var batchRequestContent = new BatchRequestContentCollection(graphServiceClient);
var request1 = graphServiceClient.Me.Messages["messageId1"].ToGetRequestInformation();
var request2 = graphServiceClient.Me.Messages["messageId2"].ToGetRequestInformation();

var batch = await batchRequestContent.AddBatchRequestStepAsync(request1);
await batchRequestContent.AddBatchRequestStepAsync(request2);

var response = await graphServiceClient.Batch.PostAsync(batchRequestContent);
#### Delta Queries for Change Tracking
// Initial request with delta
var deltaMessages = await graphServiceClient.Me.Messages.Delta.GetAsync();

// Store the deltaLink for future requests
var deltaLink = deltaMessages.OdataDeltaLink;

// Later, get only changes
var changes = await graphServiceClient.Me.Messages.Delta
    .WithUrl(deltaLink)
    .GetAsync();
### 5. Rate Limiting and Throttling

#### Implement Retry Logic
public async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation, int maxRetries = 3)
{
    for (int attempt = 0; attempt < maxRetries; attempt++)
    {
        try
        {
            return await operation();
        }
        catch (ServiceException ex) when (ex.Error.Code == "TooManyRequests")
        {
            if (attempt == maxRetries - 1) throw;
            
            var retryAfter = ex.ResponseHeaders?.RetryAfter?.Delta ?? TimeSpan.FromSeconds(Math.Pow(2, attempt));
            await Task.Delay(retryAfter);
        }
    }
    throw new InvalidOperationException("Max retries exceeded");
}
#### Respect Throttling Headers
- Monitor `Retry-After` headers
- Implement exponential backoff
- Use the `RateLimitHandler` from Microsoft Graph SDK

### 6. Testing Graph API Code

#### Unit Testing with Mocking
[Fact]
public async Task GetMessagesAsync_ShouldReturnMessages()
{
    // Arrange
    var mockGraphServiceClient = Substitute.For<GraphServiceClient>();
    var mockMessages = new MessageCollectionResponse
    {
        Value = new List<Message> { new Message { Subject = "Test" } }
    };
    
    mockGraphServiceClient.Me.Messages.GetAsync(Arg.Any<Action<RequestConfiguration<MessagesRequestBuilder.MessagesRequestBuilderGetQueryParameters>>>())
        .Returns(mockMessages);
    
    var service = new GraphEmailService(mockGraphServiceClient);
    
    // Act
    var result = await service.GetMessagesAsync();
    
    // Assert
    Assert.Single(result);
}
#### Integration Testing
- Use test tenants for integration tests
- Mock Graph responses for unit tests
- Test authentication flows separately

### 7. Performance Best Practices

#### Connection Management
- Reuse GraphServiceClient instances
- Configure HttpClient properly in DI container
- Use connection pooling

#### Caching Strategies
// Cache frequently accessed data
[MemoryCache(Duration = 300)] // 5 minutes
public async Task<User> GetCurrentUserAsync()
{
    return await graphServiceClient.Me.GetAsync();
}
#### Minimize Roundtrips
- Use `$expand` to include related data
- Batch multiple requests when possible
- Use delta queries for change tracking

### 8. Security Considerations

#### Token Management
- Use token caching to avoid unnecessary authentication requests
- Implement proper token refresh logic
- Store tokens securely

#### Least Privilege Principle
- Request minimal required permissions
- Use delegated permissions when possible
- Regularly audit and review permissions

#### Data Protection
- Don't log sensitive email content
- Implement proper error handling without exposing sensitive data
- Follow GDPR and other privacy regulations

### 9. Service Interface Implementation

Maintain clean service interfaces:
public interface IEmailService
{
    Task<IList<EmailMessage>> GetInboxEmailsAsync(string userEmail, int count = 10);
    Task<EmailMessage?> GetEmailByIdAsync(string emailId, string userEmail);
    Task<bool> SendReplyAsync(EmailReplyModel replyModel, string userEmail);
}

// Graph implementation
public class GraphEmailService : IEmailService
{
    private readonly GraphServiceClient _graphServiceClient;
    
    public GraphEmailService(GraphServiceClient graphServiceClient)
    {
        _graphServiceClient = graphServiceClient;
    }
    
    // Implement interface methods using Graph API
}
#### Data Model Mapping
Create mapping between Graph models and domain models:
public static class MessageMapper
{
    public static EmailMessage ToEmailMessage(this Message graphMessage)
    {
        return new EmailMessage
        {
            Id = graphMessage.Id,
            Subject = graphMessage.Subject,
            From = graphMessage.From?.EmailAddress?.Address,
            ReceivedDateTime = graphMessage.ReceivedDateTime?.DateTime,
            Body = graphMessage.Body?.Content
        };
    }
}
### 10. Monitoring and Logging

#### Request Logging
// Add logging to Graph requests
public class LoggingGraphEmailService : IEmailService
{
    private readonly GraphServiceClient _graphServiceClient;
    private readonly ILogger<LoggingGraphEmailService> _logger;
    
    public async Task<IList<EmailMessage>> GetInboxEmailsAsync(string userEmail, int count = 10)
    {
        _logger.LogInformation("Fetching {Count} emails for user {UserEmail}", count, userEmail);
        
        try
        {
            var result = await _graphServiceClient.Me.Messages
                .GetAsync(config => config.QueryParameters.Top = count);
            
            _logger.LogInformation("Successfully retrieved {Count} emails", result.Value?.Count ?? 0);
            return result.Value?.Select(m => m.ToEmailMessage()).ToList() ?? new List<EmailMessage>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve emails for user {UserEmail}", userEmail);
            throw;
        }
    }
}
#### Performance Monitoring
- Track Graph API response times
- Monitor rate limit usage
- Log throttling events

By following these best practices, you'll ensure optimal performance, security, and reliability with Microsoft Graph API.

## Unit Testing with xUnit

The project uses xUnit for unit testing to ensure code quality. Here's guidance on working with unit tests:

### Test Project Structure

1. **Test Projects**:
   - Each project should have a corresponding test project named `{ProjectName}.Tests`
   - Test projects should target the same .NET version as the main project
   - Test projects should be organized in a similar structure to the main project

2. **Test Organization**:
   - Group tests by the component they're testing
   - Use descriptive test class names with the suffix "Tests" (e.g., `MailServiceTests`)
   - Organize tests using xUnit fixtures and collections when appropriate

### Writing Effective Tests

1. **Naming Convention**:
   - Use the pattern `MethodName_Scenario_ExpectedBehavior` for test methods
   - Example: `GetMailbox_WithValidCredentials_ReturnsEmailList`

2. **Test Structure**:
   - Follow the Arrange-Act-Assert pattern
   - Keep tests focused on a single behavior
   - Use meaningful assertions that clearly indicate what's being tested

3. **Mocking Dependencies**:
   - Use NSubstitute for mocking interfaces and external dependencies
   - Create reusable mock setups for common scenarios
   - Consider creating a test base class for common mock setups

### Testing Graph Components

When writing tests for components that use Graph API:

1. Create interface abstractions for Graph services to facilitate mocking
2. Use dependency injection to inject these interfaces
3. Mock the Graph responses to test different scenarios
4. Create test data that mimics Graph responses

### Testing Authentication

For components that require authentication:

1. Mock the authentication services
2. Create test users with predetermined claims
3. Use `TestAuthenticationHandler` for integration tests requiring authentication

## End-to-End Testing with Playwright

The project uses Playwright for E2E testing to ensure all critical user flows are captured. Playwright was chosen because:

1. It supports testing across multiple browsers (Chromium, Firefox, WebKit)
2. It provides powerful browser automation capabilities
3. It can simulate user interactions with high fidelity
4. The TypeScript implementation offers the most comprehensive feature set
5. The Playwright UI provides excellent debugging capabilities

### Test Structure

E2E tests should follow these conventions:

1. Each major user flow should have its own test file
2. Tests should be organized under logical feature areas
3. Page Object Model pattern should be used to abstract page interactions
4. Test fixtures should be used for common setup and teardown code
5. Screenshots should be captured on test failures

### Authentication in Tests

When writing tests that require authentication:

1. Use the built-in auth fixture or auth helpers
2. Store authentication state to avoid logging in for every test
3. Use environment variables for credentials, never hardcode them
4. Consider using test users or mock authentication for CI/CD environments

### UI Component Testing

1. Each component should have data-testid attributes for reliable selection
2. Tests should verify both appearance and behavior
3. Use visual comparison only when necessary

## Upcoming Development

1. **Refactoring for modularity** - Improving code organization and separation of concerns
2. **Enhanced Graph API features** - Adding more advanced Graph API capabilities

## Best Practices

When modifying or adding to the codebase, please follow these guidelines:

1. Follow existing code style and patterns
2. Add appropriate comments for complex logic
3. Ensure all UI flows have corresponding E2E tests
4. Use data-testid attributes for elements that need to be selected in tests
5. Keep tests independent and avoid dependencies between tests
6. Use the Playwright UI for debugging tests
7. Write unit tests for all new functionality
8. Use TDD (Test-Driven Development) when implementing new features

## Common Tasks

### Running Unit Tests
dotnet test ProjectName.Tests
With test filter:
dotnet test --filter "Category=UnitTest"
With coverage:
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura
### Creating a New Test Class

1. Create a new class in the appropriate test project
2. Name the class after the component being tested with the "Tests" suffix
3. Use the xUnit `[Fact]` attribute for simple tests
4. Use the xUnit `[Theory]` attribute with `[InlineData]` for parameterized tests

Example:
public class MailServiceTests
{
    [Fact]
    public async Task GetMailbox_WithValidCredentials_ReturnsEmailList()
    {
        // Arrange
        var service = new MailService();
        
        // Act
        var result = await service.GetMailboxAsync();
        
        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }
    
    [Theory]
    [InlineData("subject1")]
    [InlineData("subject2")]
    public async Task FindEmail_WithSubject_ReturnsMatchingEmails(string subject)
    {
        // Test implementation
    }
}
### Running Playwright Tests
cd playwright-tests
npx playwright test
### Running Playwright UI
cd playwright-tests
npx playwright test --ui
### Adding New Tests

1. Create a new test file in the appropriate feature directory
2. Import necessary Playwright modules
3. Define page objects if needed
4. Write test cases that follow the Arrange-Act-Assert pattern
5. Run tests locally before committing

### Debugging Tests

1. Use `--debug` flag for real-time debugging
2. Add `page.pause()` in the test for interactive debugging
3. Use screenshots and videos for CI debugging
4. Check Playwright traces for detailed execution information