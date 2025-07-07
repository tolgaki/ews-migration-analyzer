using Contoso.Mail.Web.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Web;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Contoso.Mail.Web.Tests.Services;

/// <summary>
/// Unit tests for the ExchangeServiceFactory class.
/// </summary>
public class ExchangeServiceFactoryTests
{
    private readonly IConfiguration _mockConfiguration;
    private readonly ITokenAcquisition _mockTokenAcquisition;
    private readonly ILogger<ExchangeServiceFactory> _mockLogger;
    private readonly ExchangeServiceFactory _factory;

    // Use a valid-looking JWT token format to avoid EWS token validation errors
    private const string ValidMockToken = "eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzI1NiIsIng1dCI6Ik5HVEZ2ZUtqV3dObjB1SGpHYkVPYkNHaERCZSIsImtpZCI6Ik5HVEZ2ZUtqV3dObjB1SGpHYkVPYkNHaERCZSJ9.eyJhdWQiOiJodHRwczovL291dGxvb2sub2ZmaWNlMzY1LmNvbSIsImlzcyI6Imh0dHBzOi8vc3RzLndpbmRvd3MubmV0L2ZhNjE1YzI1LWY4YzYtNGM0MC04NzJkLWYzNzg5OTM3YTRhMS8iLCJpYXQiOjE2NzM5NjEwMDAsIm5iZiI6MTY3Mzk2MTAwMCwiZXhwIjoxNjczOTY0OTAwLCJhcHBpZCI6IjAwMDAwMDAyLTAwMDAtMGZmMS1jZTAwLTAwMDAwMDAwMDAwMCIsImFwcGlkYWNyIjoiMCIsImZhbWlseV9uYW1lIjoiVGVzdCIsImdpdmVuX25hbWUiOiJVc2VyIiwiaXBhZGRyIjoiMTkyLjE2OC4xLjEiLCJuYW1lIjoiVGVzdCBVc2VyIiwib2lkIjoiMTIzNDU2NzgtYWJjZC1lZmdoLWlqa2wtbW5vcHFyc3R1dnd4IiwicHVpZCI6IjEwMDMyMDAwOTkzNDUyMzQiLCJzY3AiOiJodHRwczovL291dGxvb2sub2ZmaWNlMzY1LmNvbS8uZGVmYXVsdCIsInN1YiI6IjEyMzQ1Njc4LWFiY2QtZWZnaC1pamtsLW1ub3BxcnN0dXZ3eCIsInRpZCI6ImZhNjE1YzI1LWY4YzYtNGM0MC04NzJkLWYzNzg5OTM3YTRhMSIsInVuaXF1ZV9uYW1lIjoidGVzdEB0ZXN0LmNvbSIsInVwbiI6InRlc3RAdGVzdC5jb20iLCJ2ZXIiOiIxLjAifQ.mock_signature";

    public ExchangeServiceFactoryTests()
    {
        _mockConfiguration = Substitute.For<IConfiguration>();
        _mockTokenAcquisition = Substitute.For<ITokenAcquisition>();
        _mockLogger = Substitute.For<ILogger<ExchangeServiceFactory>>();
        
        _factory = new ExchangeServiceFactory(_mockConfiguration, _mockTokenAcquisition, _mockLogger);
    }

    [Fact]
    public async Task CreateServiceAsync_WithDefaultConfiguration_UsesDefaultEwsUrl()
    {
        // Arrange
        var userEmail = "test@contoso.com";
        
        _mockConfiguration["Ews:Url"].Returns((string?)null);
        _mockTokenAcquisition.GetAccessTokenForUserAsync(Arg.Any<string[]>())
            .Returns(ValidMockToken);

        // Act
        var service = await _factory.CreateServiceAsync(userEmail);

        // Assert
        Assert.NotNull(service);
        Assert.Equal("https://outlook.office365.com/EWS/Exchange.asmx", service.Url.ToString());
        await _mockTokenAcquisition.Received(1).GetAccessTokenForUserAsync(
            Arg.Is<string[]>(scopes => scopes.Contains("https://outlook.office365.com/.default")));
    }

    [Fact]
    public async Task CreateServiceAsync_WithCustomConfiguration_UsesCustomEwsUrl()
    {
        // Arrange
        var userEmail = "test@contoso.com";
        var customUrl = "https://custom.exchange.com/EWS/Exchange.asmx";
        
        _mockConfiguration["Ews:Url"].Returns(customUrl);
        _mockTokenAcquisition.GetAccessTokenForUserAsync(Arg.Any<string[]>())
            .Returns(ValidMockToken);

        // Act
        var service = await _factory.CreateServiceAsync(userEmail);

        // Assert
        Assert.NotNull(service);
        Assert.Equal(customUrl, service.Url.ToString());
    }

    [Fact]
    public async Task CreateServiceAsync_ValidUserEmail_AcquiresToken()
    {
        // Arrange
        var userEmail = "test@contoso.com";
        
        _mockConfiguration["Ews:Url"].Returns("https://outlook.office365.com/EWS/Exchange.asmx");
        _mockTokenAcquisition.GetAccessTokenForUserAsync(Arg.Any<string[]>())
            .Returns(ValidMockToken);

        // Act
        var service = await _factory.CreateServiceAsync(userEmail);

        // Assert
        await _mockTokenAcquisition.Received(1).GetAccessTokenForUserAsync(
            Arg.Is<string[]>(scopes => scopes.Length == 1 && scopes[0] == "https://outlook.office365.com/.default"));
        Assert.NotNull(service.Credentials);
    }

    [Fact]
    public async Task CreateServiceAsync_TokenAcquisitionFails_ThrowsException()
    {
        // Arrange
        var userEmail = "test@contoso.com";
        
        _mockConfiguration["Ews:Url"].Returns("https://outlook.office365.com/EWS/Exchange.asmx");
        _mockTokenAcquisition.GetAccessTokenForUserAsync(Arg.Any<string[]>())
            .ThrowsAsync(new InvalidOperationException("Token acquisition failed"));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => _factory.CreateServiceAsync(userEmail));
    }

    [Fact]
    public async Task CreateServiceAsync_LogsDebugInformation()
    {
        // Arrange
        var userEmail = "test@contoso.com";
        var customUrl = "https://custom.exchange.com/EWS/Exchange.asmx";
        
        _mockConfiguration["Ews:Url"].Returns(customUrl);
        _mockTokenAcquisition.GetAccessTokenForUserAsync(Arg.Any<string[]>())
            .Returns(ValidMockToken);

        // Act
        var service = await _factory.CreateServiceAsync(userEmail);

        // Assert
        Assert.NotNull(service);
        // Verify that logging methods were called (specific log verification would require more complex setup)
        _mockLogger.ReceivedWithAnyArgs().Log(default, default, default!, default, default!);
    }
}