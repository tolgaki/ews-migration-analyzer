using Contoso.Mail.Models;
using Contoso.Mail.Web.Tests.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Exchange.WebServices.Data;
using NSubstitute;
using Task = System.Threading.Tasks.Task;

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
        var model = Assert.IsType<List<EmailMessage>>(viewResult.Model);
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
    public async Task Index_WithMicrosoftIdentityWebChallengeException_ReturnsChallenge()
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
    public async Task Reply_WithValidEmailId_ReturnsReplyView()
    {
        // Arrange
        var helper = new MailControllerTestHelper();
        var emailId = "test-email-id";
        var replyModel = MailControllerTestHelper.CreateValidEmailReplyModel();
        helper.SetupSuccessfulReplyModelCreation(replyModel);

        // Act
        var result = await helper.Controller.Reply(emailId);

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Same(replyModel, viewResult.Model);
        helper.VerifyCreateReplyModelCalled(emailId, "test@contoso.com");
    }

    [Fact]
    public async Task Reply_WithEmailNotFound_RedirectsToIndexWithError()
    {
        // Arrange
        var helper = new MailControllerTestHelper();
        var emailId = "non-existent-email-id";
        helper.SetupEmailNotFound();

        // Act
        var result = await helper.Controller.Reply(emailId);

        // Assert
        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirectResult.ActionName);
        Assert.Equal("Email not found. It may have been moved or deleted.", helper.Controller.TempData["ErrorMessage"]);
    }

    [Fact]
    public async Task Reply_WithEmptyId_ReturnsBadRequest()
    {
        // Arrange
        var helper = new MailControllerTestHelper();

        // Act
        var result = await helper.Controller.Reply("" );

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Email ID is required", badRequestResult.Value);
    }

    [Fact]
    public async Task Reply_WithNullId_ReturnsBadRequest()
    {
        // Arrange
        var helper = new MailControllerTestHelper();

        // Act
        var result = await helper.Controller.Reply(null!);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Email ID is required", badRequestResult.Value);
    }

    [Fact]
    public async Task Reply_WithMsalException_ReturnsChallenge()
    {
        // Arrange
        var helper = new MailControllerTestHelper();
        var emailId = "test-email-id";
        helper.SetupEmailServiceMsalException();

        // Act
        var result = await helper.Controller.Reply(emailId);

        // Assert
        Assert.IsType<ChallengeResult>(result);
    }

    [Fact]
    public async Task Reply_WithMicrosoftIdentityWebChallengeException_ReturnsChallenge()
    {
        // Arrange
        var helper = new MailControllerTestHelper();
        var emailId = "test-email-id";
        helper.SetupEmailServiceChallenge();

        // Act
        var result = await helper.Controller.Reply(emailId);

        // Assert
        Assert.IsType<ChallengeResult>(result);
    }

    [Fact]
    public async Task SendReply_WithValidModel_RedirectsToIndexWithSuccess()
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
    }

    [Fact]
    public async Task SendReply_WithMsalException_ReturnsChallenge()
    {
        // Arrange
        var helper = new MailControllerTestHelper();
        var replyModel = MailControllerTestHelper.CreateValidEmailReplyModel();
        helper.SetupEmailServiceMsalException();

        // Act
        var result = await helper.Controller.SendReply(replyModel);

        // Assert
        Assert.IsType<ChallengeResult>(result);
    }

    [Fact]
    public async Task SendReply_WithMicrosoftIdentityWebChallengeException_ReturnsChallenge()
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

    [Fact]
    public async Task Index_WithUnauthenticatedUser_ReturnsUnauthorized()
    {
        // Arrange
        var helper = new MailControllerTestHelper();
        helper.SetupUnauthenticatedUser();

        // Act
        var result = await helper.Controller.Index();

        // Assert
        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task Reply_WithUnauthenticatedUser_ReturnsUnauthorized()
    {
        // Arrange
        var helper = new MailControllerTestHelper();
        helper.SetupUnauthenticatedUser();

        // Act
        var result = await helper.Controller.Reply("test-id");

        // Assert
        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task SendReply_WithUnauthenticatedUser_ReturnsUnauthorized()
    {
        // Arrange
        var helper = new MailControllerTestHelper();
        helper.SetupUnauthenticatedUser();
        var replyModel = MailControllerTestHelper.CreateValidEmailReplyModel();

        // Act
        var result = await helper.Controller.SendReply(replyModel);

        // Assert
        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public void Error_ReturnsErrorViewWithRequestId()
    {
        // Arrange
        var helper = new MailControllerTestHelper();

        // Act
        var result = helper.Controller.Error();

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ErrorViewModel>(viewResult.Model);
        Assert.NotNull(model.RequestId);
    }
}
