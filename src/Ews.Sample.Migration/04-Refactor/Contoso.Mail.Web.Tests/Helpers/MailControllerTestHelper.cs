using System.Security.Claims;
using Contoso.Mail.Controllers;
using Contoso.Mail.Models;
using Contoso.Mail.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Contoso.Mail.Web.Tests.Helpers;

/// <summary>
/// Helper class for setting up MailController tests with common configurations.
/// </summary>
public class MailControllerTestHelper
{
    public IEmailService EmailService { get; }
    public ILogger<MailController> Logger { get; }
    public MailController Controller { get; }

    public MailControllerTestHelper()
    {
        EmailService = Substitute.For<IEmailService>();
        Logger = Substitute.For<ILogger<MailController>>();
        
        Controller = new MailController(EmailService, Logger);
        
        SetupControllerContext();
    }

    /// <summary>
    /// Sets up the controller context with a default authenticated user.
    /// </summary>
    /// <param name="email">User email (defaults to test@contoso.com)</param>
    /// <param name="displayName">User display name (defaults to Test User)</param>
    public void SetupControllerContext(string email = "test@contoso.com", string displayName = "Test User")
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Upn, email),
            new("preferred_username", email),
            new("name", displayName)
        };
        var identity = new ClaimsIdentity(claims, "test");
        var user = new ClaimsPrincipal(identity);
        
        var httpContext = new DefaultHttpContext
        {
            User = user
        };
        
        // Setup TempData for testing
        var tempDataProvider = Substitute.For<ITempDataProvider>();
        var tempDataDictionaryFactory = Substitute.For<ITempDataDictionaryFactory>();
        var tempData = new TempDataDictionary(httpContext, tempDataProvider);
        tempDataDictionaryFactory.GetTempData(httpContext).Returns(tempData);
        
        Controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
        Controller.TempData = tempData;
    }

    /// <summary>
    /// Sets up the controller context with an unauthenticated user.
    /// </summary>
    public void SetupUnauthenticatedUser()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity());
        
        Controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = user
            }
        };
    }

    /// <summary>
    /// Sets up the controller context with a user that has only preferred_username claim.
    /// </summary>
    public void SetupUserWithPreferredUsernameOnly(string email = "preferred@contoso.com", string displayName = "Preferred User")
    {
        var claims = new List<Claim>
        {
            new("preferred_username", email),
            new("name", displayName)
        };
        var identity = new ClaimsIdentity(claims, "test");
        var user = new ClaimsPrincipal(identity);
        
        Controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = user
            }
        };
    }

    /// <summary>
    /// Sets up the controller context with a user that has no display name.
    /// </summary>
    public void SetupUserWithoutDisplayName(string email = "noname@contoso.com")
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Upn, email)
        };
        var identity = new ClaimsIdentity(claims, "test");
        var user = new ClaimsPrincipal(identity);
        
        Controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = user
            }
        };
    }

    /// <summary>
    /// Configures the mock email service to return successful email list.
    /// </summary>
    /// <param name="emails">The list of emails to return (defaults to empty list)</param>
    public void SetupSuccessfulEmailRetrieval(IList<Microsoft.Exchange.WebServices.Data.EmailMessage>? emails = null)
    {
        emails ??= new List<Microsoft.Exchange.WebServices.Data.EmailMessage>();
        EmailService.GetInboxEmailsAsync(Arg.Any<string>(), Arg.Any<int>())
            .Returns(emails);
    }

    /// <summary>
    /// Configures the mock email service to return a successful reply model.
    /// </summary>
    /// <param name="replyModel">The reply model to return (defaults to a valid model)</param>
    public void SetupSuccessfulReplyModelCreation(EmailReplyModel? replyModel = null)
    {
        replyModel ??= CreateValidEmailReplyModel();
        EmailService.CreateReplyModelAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(replyModel);
    }

    /// <summary>
    /// Configures the mock email service to return successful reply sending.
    /// </summary>
    /// <param name="success">Whether the reply sending should succeed (defaults to true)</param>
    public void SetupSuccessfulReplySending(bool success = true)
    {
        EmailService.SendReplyAsync(Arg.Any<EmailReplyModel>(), Arg.Any<string>())
            .Returns(success);
    }

    /// <summary>
    /// Configures the mock email service to throw a MicrosoftIdentityWebChallengeUserException.
    /// </summary>
    public void SetupEmailServiceChallenge()
    {
        var msalException = new Microsoft.Identity.Client.MsalUiRequiredException("error", "description");
        var challengeException = new Microsoft.Identity.Web.MicrosoftIdentityWebChallengeUserException(msalException, new[] { "scope" });
        
        EmailService.GetInboxEmailsAsync(Arg.Any<string>(), Arg.Any<int>())
            .ThrowsAsync(challengeException);
        EmailService.CreateReplyModelAsync(Arg.Any<string>(), Arg.Any<string>())
            .ThrowsAsync(challengeException);
        EmailService.SendReplyAsync(Arg.Any<EmailReplyModel>(), Arg.Any<string>())
            .ThrowsAsync(challengeException);
    }

    /// <summary>
    /// Configures the mock email service to throw a MsalUiRequiredException.
    /// </summary>
    public void SetupEmailServiceMsalException()
    {
        var msalException = new Microsoft.Identity.Client.MsalUiRequiredException("error", "description");
        
        EmailService.GetInboxEmailsAsync(Arg.Any<string>(), Arg.Any<int>())
            .ThrowsAsync(msalException);
        EmailService.CreateReplyModelAsync(Arg.Any<string>(), Arg.Any<string>())
            .ThrowsAsync(msalException);
        EmailService.SendReplyAsync(Arg.Any<EmailReplyModel>(), Arg.Any<string>())
            .ThrowsAsync(msalException);
    }

    /// <summary>
    /// Configures the mock email service to throw a general exception.
    /// </summary>
    public void SetupEmailServiceException(string message = "Test exception")
    {
        var exception = new Exception(message);
        
        EmailService.GetInboxEmailsAsync(Arg.Any<string>(), Arg.Any<int>())
            .ThrowsAsync(exception);
        EmailService.CreateReplyModelAsync(Arg.Any<string>(), Arg.Any<string>())
            .ThrowsAsync(exception);
        EmailService.SendReplyAsync(Arg.Any<EmailReplyModel>(), Arg.Any<string>())
            .ThrowsAsync(exception);
    }

    /// <summary>
    /// Configures the email service to return null for reply model creation (email not found).
    /// </summary>
    public void SetupEmailNotFound()
    {
        EmailService.CreateReplyModelAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns((EmailReplyModel?)null);
    }

    /// <summary>
    /// Creates a valid EmailReplyModel for testing.
    /// </summary>
    public static EmailReplyModel CreateValidEmailReplyModel()
    {
        return new EmailReplyModel
        {
            Id = "test-email-id-123",
            Subject = "RE: Test Email Subject",
            To = "recipient@contoso.com",
            Body = "This is a test reply body."
        };
    }

    /// <summary>
    /// Creates an invalid EmailReplyModel for testing validation.
    /// </summary>
    public static EmailReplyModel CreateInvalidEmailReplyModel()
    {
        return new EmailReplyModel
        {
            // Missing required fields
            Id = "",
            Subject = "",
            To = "",
            Body = ""
        };
    }

    /// <summary>
    /// Verifies that the ViewBag properties are set correctly.
    /// </summary>
    public void VerifyViewBagProperties(string expectedEmail, string expectedDisplayName)
    {
        Assert.Equal(expectedDisplayName, Controller.ViewBag.DisplayName);
        Assert.Equal(expectedEmail, Controller.ViewBag.Email);
    }

    /// <summary>
    /// Verifies that the email service was called to get inbox emails.
    /// </summary>
    public void VerifyGetInboxEmailsCalled(string expectedEmail, int expectedCount = 10)
    {
        EmailService.Received(1).GetInboxEmailsAsync(expectedEmail, expectedCount);
    }

    /// <summary>
    /// Verifies that the email service was called to create a reply model.
    /// </summary>
    public void VerifyCreateReplyModelCalled(string expectedEmailId, string expectedUserEmail)
    {
        EmailService.Received(1).CreateReplyModelAsync(expectedEmailId, expectedUserEmail);
    }

    /// <summary>
    /// Verifies that the email service was called to send a reply.
    /// </summary>
    public void VerifySendReplyCalled(EmailReplyModel expectedModel, string expectedUserEmail)
    {
        EmailService.Received(1).SendReplyAsync(expectedModel, expectedUserEmail);
    }

    /// <summary>
    /// Verifies that an error was logged.
    /// </summary>
    public void VerifyErrorLogged()
    {
        Logger.Received().Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    /// <summary>
    /// Verifies that a warning was logged.
    /// </summary>
    public void VerifyWarningLogged()
    {
        Logger.Received().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    /// <summary>
    /// Verifies that information was logged.
    /// </summary>
    public void VerifyInformationLogged()
    {
        Logger.Received().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }
}
