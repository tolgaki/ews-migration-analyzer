# Contoso.Mail.Web Tests

This project contains unit tests for the Contoso.Mail.Web application, with a focus on testing controllers, models, and services.

## Testing Approach

### Challenges with Testing Exchange Web Services (EWS)

Testing code that depends on Exchange Web Services (EWS) presents unique challenges:

1. **Sealed Classes**: Many EWS classes like `ExchangeService` and `EmailMessage` are sealed, making them difficult to mock directly with tools like Moq.
2. **Static Methods**: Some EWS functionality relies on static methods that can't be easily mocked.
3. **Complex Object Structure**: EWS has complex object graphs that are difficult to construct in test environments.

### Testing Strategy

Our testing approach focuses on:

1. **Boundary Testing**: Testing the inputs and outputs of methods rather than the internal EWS calls
2. **Exception Handling**: Verifying that exceptions from EWS are handled correctly
3. **Error Paths**: Testing error scenarios like authentication failures or service unavailability

For complete testing coverage, consider:
- Using abstraction layers over EWS for better testability (e.g., service interfaces)
- Integration tests with a mock EWS server
- End-to-end tests with Playwright to validate full user flows

## Running Tests

```bash
cd Contoso.Mail.Web.Tests
dotnet test
```

## Structure

- **Controllers/** - Tests for controller classes
- **Models/** - Tests for model validation and behavior
- **Helpers/** - Test helpers and utilities
  - **EwsTestHelper.cs** - Utilities for creating EWS test data

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