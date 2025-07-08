# Contoso.Mail.Web Tests

This project contains unit tests for the Contoso.Mail.Web application, with a focus on testing controllers, models, and services.

## Testing Approach

### Testing Strategy

Our testing approach focuses on:

1. **Boundary Testing**: Testing the inputs and outputs of methods rather than the internal API calls
2. **Exception Handling**: Verifying that exceptions from the Graph API are handled correctly
3. **Error Paths**: Testing error scenarios like authentication failures or service unavailability

For complete testing coverage, consider:
- Using abstraction layers over Graph API for better testability (e.g., service interfaces)
- Integration tests with a mock Graph API server
- End-to-end tests with Playwright to validate full user flows

## Running Tests
cd Contoso.Mail.Web.Tests
dotnet test
## Structure

- **Controllers/** - Tests for controller classes
- **Models/** - Tests for model validation and behavior
- **Helpers/** - Test helpers and utilities

## Test Categories

- **Unit Tests** - Tests that focus on individual components
- **Integration Tests** - Tests that verify interactions between components
- **Functional Tests** - Tests that verify functionality from a user perspective

## Best Practices

1. Follow the Arrange-Act-Assert pattern
2. Keep tests independent
3. Use descriptive test names
4. Mock external dependencies
5. Focus on testing behavior, not implementation