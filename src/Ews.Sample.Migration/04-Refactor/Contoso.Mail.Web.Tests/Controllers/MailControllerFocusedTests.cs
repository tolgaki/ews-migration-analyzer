using Contoso.Mail.Models;
using Contoso.Mail.Web.Tests.Helpers;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Task = System.Threading.Tasks.Task;
using DomainEmailMessage = Contoso.Mail.Models.EmailMessage;

namespace Contoso.Mail.Web.Tests.Controllers;

/// <summary>
/// Additional focused tests for MailController using the test helper.
/// These tests focus on specific scenarios and edge cases with the new service layer.
/// </summary>
public class MailControllerFocusedTests
{
    [Fact]
    public async Task Index_WithSuccessfulEmailRetrieval_SetsCorrectViewBagProperties()
    {
        // Arrange
        var helper = new MailControllerTestHelper();
        helper.SetupSuccessfulEmailRetrieval();

        // Act
        var result = await helper.Controller.Index();

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        helper.VerifyViewBagProperties("test@contoso.com", "Test User");
        helper.VerifyGetInboxEmailsCalled("test@contoso.com");
    }

    [Fact]
    public async Task Index_WithPreferredUsernameOnly_UsesPreferredUsername()
    {
        // Arrange
        var helper = new MailControllerTestHelper();
        helper.SetupUserWithPreferredUsernameOnly("preferred@contoso.com", "Preferred User");
        helper.SetupSuccessfulEmailRetrieval();

        // Act
        var result = await helper.Controller.Index();

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        helper.VerifyViewBagProperties("preferred@contoso.com", "Preferred User");
        helper.VerifyGetInboxEmailsCalled("preferred@contoso.com");
    }

    [Fact]
    public async Task Index_WithNoDisplayName_UsesEmailAsDisplayName()
    {
        // Arrange
        var helper = new MailControllerTestHelper();
        helper.SetupUserWithoutDisplayName("noname@contoso.com");
        helper.SetupSuccessfulEmailRetrieval();

        // Act
        var result = await helper.Controller.Index();

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        helper.VerifyViewBagProperties("noname@contoso.com", "noname@contoso.com");
        helper.VerifyGetInboxEmailsCalled("noname@contoso.com");
    }

    [Fact]
    public async Task Index_WithEmailServiceException_ReturnsViewWithEmptyList()
    {
        // Arrange
        var helper = new MailControllerTestHelper();
        helper.SetupEmailServiceException("Service unavailable");

        // Act
        var result = await helper.Controller.Index();

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<List<DomainEmailMessage>>(viewResult.Model);
        Assert.Empty(model);
        helper.VerifyErrorLogged();
    }

    [Fact]
    public async Task Index_WithMsalException_ReturnsChallenge()
    {
        // Arrange
        var helper = new MailControllerTestHelper();
        helper.SetupEmailServiceMsalException();

        // Act
        var result = await helper.Controller.Index();

        // Assert
        Assert.IsType<ChallengeResult>(result);
    }

    [Fact]
    public async Task Index_WithChallengeException_ReturnsChallenge()
    {
        // Arrange
        var helper = new MailControllerTestHelper();
        helper.SetupEmailServiceChallenge();

        // Act
        var result = await helper.Controller.Index();

        // Assert
        Assert.IsType<ChallengeResult>(result);
    }

    [Fact]
    public async Task Reply_WithSuccessfulReplyModel_ReturnsReplyView()
    {
        // Arrange
        var helper = new MailControllerTestHelper();
        var replyModel = MailControllerTestHelper.CreateValidEmailReplyModel();
        helper.SetupSuccessfulReplyModelCreation(replyModel);

        // Act
        var result = await helper.Controller.Reply("test-email-id");

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Same(replyModel, viewResult.Model);
        helper.VerifyCreateReplyModelCalled("test-email-id", "test@contoso.com");
    }

    [Fact]
    public async Task Reply_WithEmailNotFound_RedirectsToIndexWithError()
    {
        // Arrange
        var helper = new MailControllerTestHelper();
        helper.SetupEmailNotFound();

        // Act
        var result = await helper.Controller.Reply("non-existent-id");

        // Assert
        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirectResult.ActionName);
        Assert.Equal("Email not found. It may have been moved or deleted.", helper.Controller.TempData["ErrorMessage"]);
    }

    [Fact]
    public async Task SendReply_WithSuccessfulSend_RedirectsToIndexWithSuccess()
    {
        // Arrange
        var helper = new MailControllerTestHelper();
        var replyModel = MailControllerTestHelper.CreateValidEmailReplyModel();
        helper.SetupSuccessfulReplySending(true);

        // Act
        var result = await helper.Controller.SendReply(replyModel);

        // Assert
        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirectResult.ActionName);
        Assert.Equal("Reply sent successfully!", helper.Controller.TempData["SuccessMessage"]);
        helper.VerifySendReplyCalled(replyModel, "test@contoso.com");
    }

    [Fact]
    public async Task SendReply_WithFailedSend_ReturnsReplyViewWithError()
    {
        // Arrange
        var helper = new MailControllerTestHelper();
        var replyModel = MailControllerTestHelper.CreateValidEmailReplyModel();
        helper.SetupSuccessfulReplySending(false);

        // Act
        var result = await helper.Controller.SendReply(replyModel);

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("Reply", viewResult.ViewName);
        Assert.Same(replyModel, viewResult.Model);
        Assert.True(helper.Controller.ModelState.ContainsKey(string.Empty));
    }

    [Fact]
    public async Task SendReply_WithInvalidModel_ReturnsReplyView()
    {
        // Arrange
        var helper = new MailControllerTestHelper();
        var invalidModel = MailControllerTestHelper.CreateInvalidEmailReplyModel();
        helper.Controller.ModelState.AddModelError("Subject", "Subject is required");

        // Act
        var result = await helper.Controller.SendReply(invalidModel);

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("Reply", viewResult.ViewName);
        Assert.Same(invalidModel, viewResult.Model);
        // Verify that the email service was not called when model is invalid
        await helper.EmailService.DidNotReceive().SendReplyAsync(Arg.Any<EmailReplyModel>(), Arg.Any<string>());
    }

    [Fact]
    public async Task Reply_WithAuthenticationException_ReturnsChallenge()
    {
        // Arrange
        var helper = new MailControllerTestHelper();
        helper.SetupEmailServiceChallenge();

        // Act
        var result = await helper.Controller.Reply("test-email-id");

        // Assert
        Assert.IsType<ChallengeResult>(result);
    }

    [Fact]
    public async Task SendReply_WithAuthenticationException_ReturnsChallenge()
    {
        // Arrange
        var helper = new MailControllerTestHelper();
        var replyModel = MailControllerTestHelper.CreateValidEmailReplyModel();
        helper.SetupEmailServiceChallenge();

        // Act
        var result = await helper.Controller.SendReply(replyModel);

        // Assert
        Assert.IsType<ChallengeResult>(result);
    }
}
