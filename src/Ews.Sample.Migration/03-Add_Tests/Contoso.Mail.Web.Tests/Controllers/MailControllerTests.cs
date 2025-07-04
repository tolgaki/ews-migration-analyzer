using System.Security.Claims;
using Contoso.Mail.Controllers;
using Contoso.Mail.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Exchange.WebServices.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using Microsoft.Identity.Web;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Task = System.Threading.Tasks.Task;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Contoso.Mail.Web.Tests.Controllers;

public class MailControllerTests
{
    private readonly IConfiguration _configuration;
    private readonly ITokenAcquisition _tokenAcquisition;
    private readonly ILogger<MailController> _logger;
    private readonly MailController _controller;
    private readonly ClaimsPrincipal _user;

    public MailControllerTests()
    {
        _configuration = Substitute.For<IConfiguration>();
        _tokenAcquisition = Substitute.For<ITokenAcquisition>();
        _logger = Substitute.For<ILogger<MailController>>();
        
        _controller = new MailController(_configuration, _tokenAcquisition, _logger);
        
        // Setup a mock user with claims
        var claims = new List<Claim>
        {
            new(ClaimTypes.Upn, "test@contoso.com"),
            new("preferred_username", "test@contoso.com"),
            new("name", "Test User")
        };
        var identity = new ClaimsIdentity(claims, "test");
        _user = new ClaimsPrincipal(identity);
        
        // Setup controller context with the mock user
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = _user
            }
        };
        
        // Setup default configuration values
        _configuration["Ews:Url"].Returns("https://outlook.office365.com/EWS/Exchange.asmx");
    }

    [Fact]
    public async Task Index_WithValidUser_ReturnsViewWithEmails()
    {
        // Arrange
        var accessToken = "fake-access-token";
        _tokenAcquisition.GetAccessTokenForUserAsync(Arg.Any<string[]>())
            .Returns(Task.FromResult(accessToken));

        // Act
        var result = await _controller.Index();

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.NotNull(viewResult);
        
        // Verify ViewBag properties are set
        Assert.Equal("Test User", _controller.ViewBag.DisplayName);
        Assert.Equal("test@contoso.com", _controller.ViewBag.Email);
        
        // Verify token acquisition was called
        await _tokenAcquisition.Received(1).GetAccessTokenForUserAsync(
            Arg.Is<string[]>(scopes => scopes.Contains("https://outlook.office365.com/.default")));
    }

    [Fact]
    public async Task Index_WithNoUserEmail_ReturnsUnauthorized()
    {
        // Arrange
        var emptyUser = new ClaimsPrincipal(new ClaimsIdentity());
        _controller.ControllerContext.HttpContext.User = emptyUser;

        // Act
        var result = await _controller.Index();

        // Assert
        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task Index_WithMicrosoftIdentityWebChallengeUserException_ReturnsChallenge()
    {
        // Arrange
        var msalException = new MsalUiRequiredException("error", "description");
        _tokenAcquisition.GetAccessTokenForUserAsync(Arg.Any<string[]>())
            .ThrowsAsync(new MicrosoftIdentityWebChallengeUserException(msalException, new[] { "scope" }));

        // Act
        var result = await _controller.Index();

        // Assert
        Assert.IsType<ChallengeResult>(result);
    }

    [Fact]
    public async Task Index_WithMsalUiRequiredException_ReturnsChallenge()
    {
        // Arrange
        _tokenAcquisition.GetAccessTokenForUserAsync(Arg.Any<string[]>())
            .ThrowsAsync(new MsalUiRequiredException("error", "description"));

        // Act
        var result = await _controller.Index();

        // Assert
        Assert.IsType<ChallengeResult>(result);
    }

    [Fact]
    public async Task Index_WithGeneralException_ReturnsViewWithEmptyList()
    {
        // Arrange
        _tokenAcquisition.GetAccessTokenForUserAsync(Arg.Any<string[]>())
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _controller.Index();

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<List<EmailMessage>>(viewResult.Model);
        Assert.Empty(model);
        
        // Verify that the logger was called - we just verify the method was called without checking specific parameters
        // due to the complexity of mocking ILogger extension methods with NSubstitute
        _logger.ReceivedWithAnyArgs(1).Log(default, default, default!, default, default!);
    }

    [Fact]
    public async Task Reply_WithEmptyId_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.Reply(string.Empty);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Email ID is required", badRequestResult.Value);
    }

    [Fact]
    public async Task Reply_WithNullId_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.Reply(null!);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Email ID is required", badRequestResult.Value);
    }

    [Fact]
    public async Task Reply_WithMicrosoftIdentityWebChallengeUserException_ReturnsChallenge()
    {
        // Arrange
        var emailId = "test-email-id";
        var msalException = new MsalUiRequiredException("error", "description");
        _tokenAcquisition.GetAccessTokenForUserAsync(Arg.Any<string[]>())
            .ThrowsAsync(new MicrosoftIdentityWebChallengeUserException(msalException, new[] { "scope" }));

        // Act
        var result = await _controller.Reply(emailId);

        // Assert
        Assert.IsType<ChallengeResult>(result);
    }

    [Fact]
    public async Task Reply_WithMsalUiRequiredException_ReturnsChallenge()
    {
        // Arrange
        var emailId = "test-email-id";
        _tokenAcquisition.GetAccessTokenForUserAsync(Arg.Any<string[]>())
            .ThrowsAsync(new MsalUiRequiredException("error", "description"));

        // Act
        var result = await _controller.Reply(emailId);

        // Assert
        Assert.IsType<ChallengeResult>(result);
    }

    [Fact]
    public async Task SendReply_WithInvalidModel_ReturnsReplyView()
    {
        // Arrange
        var model = new EmailReplyModel();
        _controller.ModelState.AddModelError("Subject", "Subject is required");

        // Act
        var result = await _controller.SendReply(model);

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("Reply", viewResult.ViewName);
        Assert.Same(model, viewResult.Model);
    }

    [Fact]
    public async Task SendReply_WithMicrosoftIdentityWebChallengeUserException_ReturnsChallenge()
    {
        // Arrange
        var model = new EmailReplyModel
        {
            Id = "test-id",
            Subject = "Test Subject",
            To = "test@example.com",
            Body = "Test body"
        };
        
        var msalException = new MsalUiRequiredException("error", "description");
        _tokenAcquisition.GetAccessTokenForUserAsync(Arg.Any<string[]>())
            .ThrowsAsync(new MicrosoftIdentityWebChallengeUserException(msalException, new[] { "scope" }));

        // Act
        var result = await _controller.SendReply(model);

        // Assert
        Assert.IsType<ChallengeResult>(result);
    }

    [Fact]
    public async Task SendReply_WithMsalUiRequiredException_ReturnsChallenge()
    {
        // Arrange
        var model = new EmailReplyModel
        {
            Id = "test-id",
            Subject = "Test Subject", 
            To = "test@example.com",
            Body = "Test body"
        };
        
        _tokenAcquisition.GetAccessTokenForUserAsync(Arg.Any<string[]>())
            .ThrowsAsync(new MsalUiRequiredException("error", "description"));

        // Act
        var result = await _controller.SendReply(model);

        // Assert
        Assert.IsType<ChallengeResult>(result);
    }

    [Fact]
    public void Error_ReturnsErrorView()
    {
        // Act
        var result = _controller.Error();

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ErrorViewModel>(viewResult.Model);
        Assert.NotNull(model.RequestId);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task Index_WithMissingOrEmptyUserEmail_ReturnsUnauthorized(string? email)
    {
        // Arrange
        var claims = new List<Claim>();
        if (!string.IsNullOrEmpty(email))
        {
            claims.Add(new Claim(ClaimTypes.Upn, email));
        }
        
        var identity = new ClaimsIdentity(claims, "test");
        var user = new ClaimsPrincipal(identity);
        _controller.ControllerContext.HttpContext.User = user;

        // Act
        var result = await _controller.Index();

        // Assert
        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task Index_UsesPreferredUsernameWhenUpnMissing()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new("preferred_username", "preferred@contoso.com"),
            new("name", "Preferred User")
        };
        var identity = new ClaimsIdentity(claims, "test");
        var user = new ClaimsPrincipal(identity);
        _controller.ControllerContext.HttpContext.User = user;

        var accessToken = "fake-access-token";
        _tokenAcquisition.GetAccessTokenForUserAsync(Arg.Any<string[]>())
            .Returns(Task.FromResult(accessToken));

        // Act
        var result = await _controller.Index();

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("Preferred User", _controller.ViewBag.DisplayName);
        Assert.Equal("preferred@contoso.com", _controller.ViewBag.Email);
    }

    [Fact]
    public async Task Index_UsesEmailAsDisplayNameWhenNameMissing()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new(ClaimTypes.Upn, "test@contoso.com")
        };
        var identity = new ClaimsIdentity(claims, "test");
        var user = new ClaimsPrincipal(identity);
        _controller.ControllerContext.HttpContext.User = user;

        var accessToken = "fake-access-token";
        _tokenAcquisition.GetAccessTokenForUserAsync(Arg.Any<string[]>())
            .Returns(Task.FromResult(accessToken));

        // Act
        var result = await _controller.Index();

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("test@contoso.com", _controller.ViewBag.DisplayName);
        Assert.Equal("test@contoso.com", _controller.ViewBag.Email);
    }

    [Fact]
    public void Controller_InitializesWithCorrectDependencies()
    {
        // Arrange & Act
        var controller = new MailController(_configuration, _tokenAcquisition, _logger);

        // Assert
        Assert.NotNull(controller);
    }

    [Fact]
    public async Task Index_UsesConfiguredEwsUrl()
    {
        // Arrange
        var customUrl = "https://custom.exchange.com/EWS/Exchange.asmx";
        _configuration["Ews:Url"].Returns(customUrl);
        
        var accessToken = "fake-access-token";
        _tokenAcquisition.GetAccessTokenForUserAsync(Arg.Any<string[]>())
            .Returns(Task.FromResult(accessToken));

        // Act
        var result = await _controller.Index();

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.NotNull(viewResult);
        
        // Verify the configuration was accessed
        var received = _configuration.Received(1)["Ews:Url"];
    }

    [Fact]
    public async Task Index_UsesDefaultEwsUrlWhenNotConfigured()
    {
        // Arrange
        _configuration["Ews:Url"].Returns((string?)null);
        
        var accessToken = "fake-access-token";
        _tokenAcquisition.GetAccessTokenForUserAsync(Arg.Any<string[]>())
            .Returns(Task.FromResult(accessToken));

        // Act
        var result = await _controller.Index();

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.NotNull(viewResult);
        
        // Verify the configuration was accessed (even though it returned null)
        var received = _configuration.Received(1)["Ews:Url"];
    }
}
