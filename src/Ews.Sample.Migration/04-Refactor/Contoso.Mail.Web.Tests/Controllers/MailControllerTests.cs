using System.Security.Claims;
using Contoso.Mail.Controllers;
using Contoso.Mail.Models;
using Contoso.Mail.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Exchange.WebServices.Data;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using Microsoft.Identity.Web;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Task = System.Threading.Tasks.Task;

namespace Contoso.Mail.Web.Tests.Controllers;

public class MailControllerTests
{
    private readonly IEmailService _emailService;
    private readonly ILogger<MailController> _logger;
    private readonly MailController _controller;
    private readonly ClaimsPrincipal _user;

    public MailControllerTests()
    {
        _emailService = Substitute.For<IEmailService>();
        _logger = Substitute.For<ILogger<MailController>>();
        
        _controller = new MailController(_emailService, _logger);
        
        // Setup a mock user with claims
        var claims = new List<Claim>
        {
            new(ClaimTypes.Upn, "test@contoso.com"),
            new("preferred_username", "test@contoso.com"),
            new("name", "Test User")
        };
        var identity = new ClaimsIdentity(claims, "test");
        _user = new ClaimsPrincipal(identity);
        
        // Setup controller context with the mock user and TempData
        var httpContext = new DefaultHttpContext
        {
            User = _user
        };
        
        var tempDataProvider = Substitute.For<ITempDataProvider>();
        var tempData = new TempDataDictionary(httpContext, tempDataProvider);
        
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
        _controller.TempData = tempData;
    }

    [Fact]
    public async Task Index_WithValidUser_ReturnsViewWithEmails()
    {
        // Arrange
        var mockEmails = new List<EmailMessage>(); // Empty list is fine for this test
        _emailService.GetInboxEmailsAsync("test@contoso.com", 10)
            .Returns(mockEmails);

        // Act
        var result = await _controller.Index();

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.NotNull(viewResult);
        Assert.Same(mockEmails, viewResult.Model);
        
        // Verify ViewBag properties are set
        Assert.Equal("Test User", _controller.ViewBag.DisplayName);
        Assert.Equal("test@contoso.com", _controller.ViewBag.Email);
        
        // Verify email service was called
        await _emailService.Received(1).GetInboxEmailsAsync("test@contoso.com", 10);
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
        await _emailService.DidNotReceive().GetInboxEmailsAsync(Arg.Any<string>(), Arg.Any<int>());
    }

    [Fact]
    public async Task Index_WithMicrosoftIdentityWebChallengeUserException_ReturnsChallenge()
    {
        // Arrange
        var msalException = new MsalUiRequiredException("error", "description");
        _emailService.GetInboxEmailsAsync(Arg.Any<string>(), Arg.Any<int>())
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
        _emailService.GetInboxEmailsAsync(Arg.Any<string>(), Arg.Any<int>())
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
        _emailService.GetInboxEmailsAsync(Arg.Any<string>(), Arg.Any<int>())
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _controller.Index();

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<List<EmailMessage>>(viewResult.Model);
        Assert.Empty(model);
        
        // Verify that the logger was called
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
        await _emailService.DidNotReceive().CreateReplyModelAsync(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task Reply_WithNullId_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.Reply(null!);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Email ID is required", badRequestResult.Value);
        await _emailService.DidNotReceive().CreateReplyModelAsync(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task Reply_WithValidId_ReturnsReplyView()
    {
        // Arrange
        var emailId = "test-email-id";
        var replyModel = new EmailReplyModel
        {
            Id = emailId,
            Subject = "RE: Test Subject",
            To = "sender@contoso.com",
            Body = "Reply body"
        };

        _emailService.CreateReplyModelAsync(emailId, "test@contoso.com")
            .Returns(replyModel);

        // Act
        var result = await _controller.Reply(emailId);

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Same(replyModel, viewResult.Model);
        await _emailService.Received(1).CreateReplyModelAsync(emailId, "test@contoso.com");
    }

    [Fact]
    public async Task Reply_WithEmailNotFound_RedirectsToIndex()
    {
        // Arrange
        var emailId = "non-existent-email-id";
        _emailService.CreateReplyModelAsync(emailId, "test@contoso.com")
            .Returns((EmailReplyModel?)null);

        // Act
        var result = await _controller.Reply(emailId);

        // Assert
        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(MailController.Index), redirectResult.ActionName);
        Assert.Equal("Email not found. It may have been moved or deleted.", _controller.TempData["ErrorMessage"]);
    }

    [Fact]
    public async Task Reply_WithMicrosoftIdentityWebChallengeUserException_ReturnsChallenge()
    {
        // Arrange
        var emailId = "test-email-id";
        var msalException = new MsalUiRequiredException("error", "description");
        _emailService.CreateReplyModelAsync(Arg.Any<string>(), Arg.Any<string>())
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
        _emailService.CreateReplyModelAsync(Arg.Any<string>(), Arg.Any<string>())
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
        await _emailService.DidNotReceive().SendReplyAsync(Arg.Any<EmailReplyModel>(), Arg.Any<string>());
    }

    [Fact]
    public async Task SendReply_WithValidModel_RedirectsToIndex()
    {
        // Arrange
        var model = new EmailReplyModel
        {
            Id = "test-id",
            Subject = "Test Subject",
            To = "test@example.com",
            Body = "Test body"
        };

        _emailService.SendReplyAsync(model, "test@contoso.com")
            .Returns(true);

        // Act
        var result = await _controller.SendReply(model);

        // Assert
        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(MailController.Index), redirectResult.ActionName);
        Assert.Equal("Reply sent successfully!", _controller.TempData["SuccessMessage"]);
        await _emailService.Received(1).SendReplyAsync(model, "test@contoso.com");
    }

    [Fact]
    public async Task SendReply_WithFailedSend_ReturnsReplyViewWithError()
    {
        // Arrange
        var model = new EmailReplyModel
        {
            Id = "test-id",
            Subject = "Test Subject",
            To = "test@example.com",
            Body = "Test body"
        };

        _emailService.SendReplyAsync(model, "test@contoso.com")
            .Returns(false);

        // Act
        var result = await _controller.SendReply(model);

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("Reply", viewResult.ViewName);
        Assert.Same(model, viewResult.Model);
        Assert.True(_controller.ModelState.ContainsKey(string.Empty));
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
        _emailService.SendReplyAsync(Arg.Any<EmailReplyModel>(), Arg.Any<string>())
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
        
        _emailService.SendReplyAsync(Arg.Any<EmailReplyModel>(), Arg.Any<string>())
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
        await _emailService.DidNotReceive().GetInboxEmailsAsync(Arg.Any<string>(), Arg.Any<int>());
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

        var mockEmails = new List<EmailMessage>();
        _emailService.GetInboxEmailsAsync("preferred@contoso.com", 10)
            .Returns(mockEmails);

        // Act
        var result = await _controller.Index();

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("Preferred User", _controller.ViewBag.DisplayName);
        Assert.Equal("preferred@contoso.com", _controller.ViewBag.Email);
        await _emailService.Received(1).GetInboxEmailsAsync("preferred@contoso.com", 10);
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

        var mockEmails = new List<EmailMessage>();
        _emailService.GetInboxEmailsAsync("test@contoso.com", 10)
            .Returns(mockEmails);

        // Act
        var result = await _controller.Index();

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("test@contoso.com", _controller.ViewBag.DisplayName);
        Assert.Equal("test@contoso.com", _controller.ViewBag.Email);
        await _emailService.Received(1).GetInboxEmailsAsync("test@contoso.com", 10);
    }

    [Fact]
    public void Controller_InitializesWithCorrectDependencies()
    {
        // Arrange & Act
        var controller = new MailController(_emailService, _logger);

        // Assert
        Assert.NotNull(controller);
    }
}
