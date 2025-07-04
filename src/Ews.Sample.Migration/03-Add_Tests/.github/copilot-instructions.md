# GitHub Copilot Instructions for Contoso.Mail.Web

## Project Overview

Contoso.Mail.Web is an ASP.NET Core Razor Pages application that provides a web interface for viewing and replying to emails using Exchange Web Services (EWS). The application demonstrates how to use EWS to interact with Microsoft Exchange mailboxes, and serves as a reference implementation for migrating from EWS to Microsoft Graph API.

### Key Features

- User authentication via Microsoft Identity Platform (Azure AD)
- Email listing from user's inbox using EWS
- Ability to reply to emails using EWS
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
- **EWS Managed API** - Exchange Web Services integration
- **Playwright** - End-to-end testing framework
- **TypeScript** - Used for Playwright test implementation
- **xUnit** - Unit testing framework
- **NSubstitute** - Mocking Framework

## Unit Testing with xUnit

The project uses xUnit for unit testing to ensure code quality and facilitate the transition from EWS to Microsoft Graph API. Here's guidance on working with unit tests:

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
   - Use Moq for mocking interfaces and external dependencies
   - Create reusable mock setups for common scenarios
   - Consider creating a test base class for common mock setups

### Testing EWS Components

When writing tests for components that use EWS:

1. Create interface abstractions for EWS services to facilitate mocking
2. Use dependency injection to inject these interfaces
3. Mock the EWS responses to test different scenarios
4. Create test data that mimics EWS responses

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
2. **Migrating from EWS to Microsoft Graph API** - Replacing EWS components with Graph API equivalents

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
With test filter:dotnet test --filter "Category=UnitTest"
With coverage:dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura
### Creating a New Test Class

1. Create a new class in the appropriate test project
2. Name the class after the component being tested with the "Tests" suffix
3. Use the xUnit `[Fact]` attribute for simple tests
4. Use the xUnit `[Theory]` attribute with `[InlineData]` for parameterized tests

Example:public class MailServiceTests
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