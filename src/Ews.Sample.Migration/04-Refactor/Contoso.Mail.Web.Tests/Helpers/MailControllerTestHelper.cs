using System.Security.Claims;
using Contoso.Mail.Controllers;
using Contoso.Mail.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Web;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Contoso.Mail.Web.Tests.Helpers;

/// <summary>
/// Helper class for setting up MailController tests with common configurations.
/// </summary>
public class MailControllerTestHelper
{
    public IConfiguration Configuration { get; }
    public ITokenAcquisition TokenAcquisition { get; }
    public ILogger<MailController> Logger { get; }
    public MailController Controller { get; }

    public MailControllerTestHelper()
    {
        Configuration = Substitute.For<IConfiguration>();
        TokenAcquisition = Substitute.For<ITokenAcquisition>();
        Logger = Substitute.For<ILogger<MailController>>();
        
        Controller = new MailController(Configuration, TokenAcquisition, Logger);
        
        // Setup default configuration
        Configuration["Ews:Url"].Returns("https://outlook.office365.com/EWS/Exchange.asmx");
        
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
    /// Configures the mock token acquisition to return a successful token.
    /// </summary>
    /// <param name="token">The token to return (defaults to fake-access-token)</param>
    public void SetupSuccessfulTokenAcquisition(string token = "fake-access-token")
    {
        TokenAcquisition.GetAccessTokenForUserAsync(Arg.Any<string[]>())
            .Returns(Task.FromResult(token));
    }

    /// <summary>
    /// Configures the mock token acquisition to throw a MicrosoftIdentityWebChallengeUserException.
    /// </summary>
    public void SetupTokenAcquisitionChallenge()
    {
        var msalException = new Microsoft.Identity.Client.MsalUiRequiredException("error", "description");
        TokenAcquisition.GetAccessTokenForUserAsync(Arg.Any<string[]>())
            .ThrowsAsync(new MicrosoftIdentityWebChallengeUserException(msalException, new[] { "scope" }));
    }

    /// <summary>
    /// Configures the mock token acquisition to throw a MsalUiRequiredException.
    /// </summary>
    public void SetupTokenAcquisitionMsalException()
    {
        TokenAcquisition.GetAccessTokenForUserAsync(Arg.Any<string[]>())
            .ThrowsAsync(new Microsoft.Identity.Client.MsalUiRequiredException("error", "description"));
    }

    /// <summary>
    /// Configures the mock token acquisition to throw a general exception.
    /// </summary>
    public void SetupTokenAcquisitionException(string message = "Test exception")
    {
        TokenAcquisition.GetAccessTokenForUserAsync(Arg.Any<string[]>())
            .ThrowsAsync(new Exception(message));
    }

    /// <summary>
    /// Sets up a custom EWS URL configuration.
    /// </summary>
    public void SetupCustomEwsUrl(string? url)
    {
        Configuration["Ews:Url"].Returns(url);
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
    /// Verifies that token acquisition was called with the correct scopes.
    /// </summary>
    public void VerifyTokenAcquisitionCalled()
    {
        TokenAcquisition.Received(1).GetAccessTokenForUserAsync(
            Arg.Is<string[]>(scopes => scopes.Contains("https://outlook.office365.com/.default")));
    }

    /// <summary>
    /// Verifies that an error was logged.
    /// </summary>
    public void VerifyErrorLogged(string expectedMessage)
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
    public void VerifyWarningLogged(string expectedMessage)
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
    public void VerifyInformationLogged(string expectedMessage)
    {
        Logger.Received().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }
}
