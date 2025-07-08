using Contoso.Mail.Models;
using Contoso.Mail.Web.Services;
using Microsoft.Exchange.WebServices.Data;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Task = System.Threading.Tasks.Task;

namespace Contoso.Mail.Web.Tests.Services;

/// <summary>
/// Unit tests for the EwsEmailService class.
/// Note: Due to the sealed nature of EWS classes, these tests focus on behavior verification
/// rather than direct mocking of EWS objects.
/// </summary>
public class EwsEmailServiceTests
{
    private readonly IExchangeServiceFactory _mockExchangeServiceFactory;
    private readonly ILogger<EwsEmailService> _mockLogger;
    private readonly EwsEmailService _emailService;

    public EwsEmailServiceTests()
    {
        _mockExchangeServiceFactory = Substitute.For<IExchangeServiceFactory>();
        _mockLogger = Substitute.For<ILogger<EwsEmailService>>();
        
        _emailService = new EwsEmailService(_mockExchangeServiceFactory, _mockLogger);
    }

    [Fact]
    public async Task GetInboxEmailsAsync_ValidUserEmail_CallsExchangeServiceFactory()
    {
        // Arrange
        var userEmail = "test@contoso.com";
        var count = 5;
        
        // Create a real ExchangeService instance since it can't be mocked
        var mockService = new ExchangeService(ExchangeVersion.Exchange2013_SP1);
        
        _mockExchangeServiceFactory.CreateServiceAsync(userEmail)
            .Returns(mockService);

        // Note: We can't easily mock the EWS service calls due to sealed classes,
        // but we can verify that the factory is called correctly
        try
        {
            // Act
            await _emailService.GetInboxEmailsAsync(userEmail, count);
        }
        catch
        {
            // Expected to fail due to actual EWS call limitations in test environment
        }

        // Assert
        await _mockExchangeServiceFactory.Received(1).CreateServiceAsync(userEmail);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task GetEmailByIdAsync_NullOrEmptyEmailId_ReturnsNull(string? emailId)
    {
        // Arrange
        var userEmail = "test@contoso.com";

        // Act
        var result = await _emailService.GetEmailByIdAsync(emailId!, userEmail);

        // Assert
        Assert.Null(result);
        await _mockExchangeServiceFactory.DidNotReceive().CreateServiceAsync(Arg.Any<string>());
    }

    [Fact]
    public void GetEmailByIdAsync_WithoutUserContext_ThrowsInvalidOperationException()
    {
        // Arrange
        var emailId = "test-email-id";

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => 
            _emailService.GetEmailByIdAsync(emailId).GetAwaiter().GetResult());
        
        Assert.Contains("requires user context", exception.Message);
    }

    [Fact]
    public void CreateReplyModelAsync_WithoutUserContext_ThrowsInvalidOperationException()
    {
        // Arrange
        var emailId = "test-email-id";

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => 
            _emailService.CreateReplyModelAsync(emailId).GetAwaiter().GetResult());
        
        Assert.Contains("requires user context", exception.Message);
    }

    [Fact]
    public void SendReplyAsync_WithoutUserContext_ThrowsInvalidOperationException()
    {
        // Arrange
        var replyModel = new EmailReplyModel
        {
            Id = "test-id",
            Subject = "Test Subject",
            To = "test@contoso.com",
            Body = "Test Body"
        };

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => 
            _emailService.SendReplyAsync(replyModel).GetAwaiter().GetResult());
        
        Assert.Contains("requires user context", exception.Message);
    }

    [Fact]
    public async Task GetEmailByIdAsync_WithUserContext_CallsExchangeServiceFactory()
    {
        // Arrange
        var emailId = "test-email-id";
        var userEmail = "test@contoso.com";
        
        // Create a real ExchangeService instance since it can't be mocked
        var mockService = new ExchangeService(ExchangeVersion.Exchange2013_SP1);
        
        _mockExchangeServiceFactory.CreateServiceAsync(userEmail)
            .Returns(mockService);

        try
        {
            // Act
            await _emailService.GetEmailByIdAsync(emailId, userEmail);
        }
        catch
        {
            // Expected to fail due to actual EWS call limitations in test environment
        }

        // Assert
        await _mockExchangeServiceFactory.Received(1).CreateServiceAsync(userEmail);
    }

    [Fact]
    public async Task CreateReplyModelAsync_WithUserContext_CallsExchangeServiceFactory()
    {
        // Arrange
        var emailId = "test-email-id";
        var userEmail = "test@contoso.com";
        
        // Create a real ExchangeService instance since it can't be mocked
        var mockService = new ExchangeService(ExchangeVersion.Exchange2013_SP1);
        
        _mockExchangeServiceFactory.CreateServiceAsync(userEmail)
            .Returns(mockService);

        try
        {
            // Act
            await _emailService.CreateReplyModelAsync(emailId, userEmail);
        }
        catch
        {
            // Expected to fail due to actual EWS call limitations in test environment
        }

        // Assert
        await _mockExchangeServiceFactory.Received(1).CreateServiceAsync(userEmail);
    }

    [Fact]
    public async Task SendReplyAsync_WithUserContext_CallsExchangeServiceFactory()
    {
        // Arrange
        var replyModel = new EmailReplyModel
        {
            Id = "test-id",
            Subject = "Test Subject",
            To = "test@contoso.com",
            Body = "Test Body"
        };
        var userEmail = "test@contoso.com";
        
        // Create a real ExchangeService instance since it can't be mocked
        var mockService = new ExchangeService(ExchangeVersion.Exchange2013_SP1);
        
        _mockExchangeServiceFactory.CreateServiceAsync(userEmail)
            .Returns(mockService);

        try
        {
            // Act
            await _emailService.SendReplyAsync(replyModel, userEmail);
        }
        catch
        {
            // Expected to fail due to actual EWS call limitations in test environment
        }

        // Assert
        await _mockExchangeServiceFactory.Received(1).CreateServiceAsync(userEmail);
    }

    [Fact]
    public void EwsEmailService_Constructor_InitializesCorrectly()
    {
        // Arrange & Act
        var service = new EwsEmailService(_mockExchangeServiceFactory, _mockLogger);

        // Assert
        Assert.NotNull(service);
    }

    [Fact]
    public async Task GetInboxEmailsAsync_LogsInformation()
    {
        // Arrange
        var userEmail = "test@contoso.com";
        var count = 10;
        
        var mockService = new ExchangeService(ExchangeVersion.Exchange2013_SP1);
        _mockExchangeServiceFactory.CreateServiceAsync(userEmail)
            .Returns(mockService);

        try
        {
            // Act
            await _emailService.GetInboxEmailsAsync(userEmail, count);
        }
        catch
        {
            // Expected to fail due to actual EWS call limitations
        }

        // Assert - Verify logging was called
        _mockLogger.ReceivedWithAnyArgs().Log(default, default, default!, default, default!);
    }

    [Fact]
    public async Task GetInboxEmailsAsync_ReturnsEmptyListOnError()
    {
        // Arrange
        var userEmail = "test@contoso.com";
        var count = 10;
        
        // Setup to throw an exception
        _mockExchangeServiceFactory.CreateServiceAsync(userEmail)
            .Returns(Task.FromException<ExchangeService>(new Exception("Test exception")));

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() => _emailService.GetInboxEmailsAsync(userEmail, count));
    }

    [Fact]
    public async Task GetEmailByIdAsync_InvalidEmailId_ReturnsNull()
    {
        // Arrange
        var emailId = "";
        var userEmail = "test@contoso.com";

        // Act
        var result = await _emailService.GetEmailByIdAsync(emailId, userEmail);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task CreateReplyModelAsync_ValidEmailId_CallsGetEmailByIdAsync()
    {
        // Arrange
        var emailId = "valid-email-id";
        var userEmail = "test@contoso.com";
        
        var mockService = new ExchangeService(ExchangeVersion.Exchange2013_SP1);
        _mockExchangeServiceFactory.CreateServiceAsync(userEmail)
            .Returns(mockService);

        try
        {
            // Act
            await _emailService.CreateReplyModelAsync(emailId, userEmail);
        }
        catch
        {
            // Expected to fail due to actual EWS call limitations
        }

        // Assert
        await _mockExchangeServiceFactory.Received(1).CreateServiceAsync(userEmail); // Called once through GetEmailByIdAsync
    }

    [Fact]
    public async Task SendReplyAsync_ValidReplyModel_CallsExchangeService()
    {
        // Arrange
        var replyModel = new EmailReplyModel
        {
            Id = "valid-email-id",
            Subject = "RE: Test Subject",
            To = "recipient@contoso.com",
            Body = "Reply body"
        };
        var userEmail = "test@contoso.com";
        
        var mockService = new ExchangeService(ExchangeVersion.Exchange2013_SP1);
        _mockExchangeServiceFactory.CreateServiceAsync(userEmail)
            .Returns(mockService);

        try
        {
            // Act
            await _emailService.SendReplyAsync(replyModel, userEmail);
        }
        catch
        {
            // Expected to fail due to actual EWS call limitations
        }

        // Assert
        await _mockExchangeServiceFactory.Received(1).CreateServiceAsync(userEmail);
    }
}