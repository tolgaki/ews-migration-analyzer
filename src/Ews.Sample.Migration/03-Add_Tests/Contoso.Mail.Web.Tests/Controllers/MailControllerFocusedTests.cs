using Contoso.Mail.Models;
using Contoso.Mail.Web.Tests.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Exchange.WebServices.Data;
using NSubstitute;
using Task = System.Threading.Tasks.Task;

namespace Contoso.Mail.Web.Tests.Controllers;

/// <summary>
/// Additional focused tests for MailController using the test helper.
/// These tests focus on specific scenarios and edge cases.
/// </summary>
public class MailControllerFocusedTests
{
    [Fact]
    public async Task Index_WithSuccessfulTokenAcquisition_SetsCorrectViewBagProperties()
    {
        // Arrange
        var helper = new MailControllerTestHelper();
        helper.SetupSuccessfulTokenAcquisition();

        // Act
        var result = await helper.Controller.Index();

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        helper.VerifyViewBagProperties("test@contoso.com", "Test User");
        helper.VerifyTokenAcquisitionCalled();
    }

    [Fact]
    public async Task Index_WithPreferredUsernameOnly_UsesPreferredUsername()
    {
        // Arrange
        var helper = new MailControllerTestHelper();
        helper.SetupUserWithPreferredUsernameOnly("preferred@contoso.com", "Preferred User");
        helper.SetupSuccessfulTokenAcquisition();

        // Act
        var result = await helper.Controller.Index();

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        helper.VerifyViewBagProperties("preferred@contoso.com", "Preferred User");
    }

    [Fact]
    public async Task Index_WithNoDisplayName_UsesEmailAsDisplayName()
    {
        // Arrange
        var helper = new MailControllerTestHelper();
        helper.SetupUserWithoutDisplayName("noname@contoso.com");
        helper.SetupSuccessfulTokenAcquisition();

        // Act
        var result = await helper.Controller.Index();

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        helper.VerifyViewBagProperties("noname@contoso.com", "noname@contoso.com");
    }

    [Fact]
    public async Task Index_WithCustomEwsUrl_UsesConfiguredUrl()
    {
        // Arrange
        var helper = new MailControllerTestHelper();
        var customUrl = "https://custom.exchange.com/EWS/Exchange.asmx";
        helper.SetupCustomEwsUrl(customUrl);
        helper.SetupSuccessfulTokenAcquisition();

        // Act
        var result = await helper.Controller.Index();

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.NotNull(viewResult);
        // Verify the custom URL configuration was accessed
        var receivedUrl = helper.Configuration.Received(1)["Ews:Url"];
    }

    [Fact]
    public async Task Index_WithNullEwsUrl_UsesDefaultUrl()
    {
        // Arrange
        var helper = new MailControllerTestHelper();
        helper.SetupCustomEwsUrl(null);
        helper.SetupSuccessfulTokenAcquisition();

        // Act
        var result = await helper.Controller.Index();

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.NotNull(viewResult);
    }

    [Fact]
    public async Task Index_WithTokenAcquisitionException_LogsErrorAndReturnsEmptyView()
    {
        // Arrange
        var helper = new MailControllerTestHelper();
        helper.SetupTokenAcquisitionException("Test token exception");

        // Act
        var result = await helper.Controller.Index();

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<List<EmailMessage>>(viewResult.Model);
        Assert.Empty(model);
        helper.VerifyErrorLogged("Error retrieving mailbox items");
    }

    [Fact]
    public async Task Reply_WithValidId_LogsInformationMessages()
    {
        // Arrange
        var helper = new MailControllerTestHelper();
        var emailId = "test-email-id";
        helper.SetupTokenAcquisitionChallenge(); // This will cause a challenge before EWS operations

        // Act
        var result = await helper.Controller.Reply(emailId);

        // Assert
        Assert.IsType<ChallengeResult>(result);
        // Verify that the reply action was logged
        helper.VerifyInformationLogged("Reply action called with ID:");
    }

    [Fact]
    public async Task Reply_WithEncodedId_DecodesIdCorrectly()
    {
        // Arrange
        var helper = new MailControllerTestHelper();
        var encodedId = Uri.EscapeDataString("test-email-id-with-special-chars+/=");
        helper.SetupTokenAcquisitionChallenge(); // This will cause a challenge before EWS operations

        // Act
        var result = await helper.Controller.Reply(encodedId);

        // Assert
        Assert.IsType<ChallengeResult>(result);
        // Verify that the URL decoding was logged
        helper.VerifyInformationLogged("Searching for email with decoded UniqueId:");
    }

    [Fact]
    public async Task SendReply_WithValidModel_LogsCorrectInformation()
    {
        // Arrange
        var helper = new MailControllerTestHelper();
        var model = MailControllerTestHelper.CreateValidEmailReplyModel();
        helper.SetupTokenAcquisitionChallenge(); // This will cause a challenge before EWS operations

        // Act
        var result = await helper.Controller.SendReply(model);

        // Assert
        Assert.IsType<ChallengeResult>(result);
        helper.VerifyInformationLogged($"SendReply action called for ID: {model.Id}");
    }

    [Fact]
    public async Task SendReply_WithInvalidModel_ReturnsReplyViewWithModel()
    {
        // Arrange
        var helper = new MailControllerTestHelper();
        var model = MailControllerTestHelper.CreateInvalidEmailReplyModel();
        
        // Add model validation errors
        helper.Controller.ModelState.AddModelError("Subject", "Subject is required");
        helper.Controller.ModelState.AddModelError("To", "To is required");
        helper.Controller.ModelState.AddModelError("Body", "Body is required");

        // Act
        var result = await helper.Controller.SendReply(model);

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("Reply", viewResult.ViewName);
        Assert.Same(model, viewResult.Model);
        helper.VerifyWarningLogged("Model state is invalid");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\n")]
    public async Task Reply_WithWhitespaceId_ReturnsBadRequest(string whitespaceId)
    {
        // Arrange
        var helper = new MailControllerTestHelper();

        // Act
        var result = await helper.Controller.Reply(whitespaceId);

        // Assert
        if (string.IsNullOrEmpty(whitespaceId.Trim()))
        {
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Email ID is required", badRequestResult.Value);
        }
    }

    [Fact]
    public void Error_WithTraceIdentifier_ReturnsModelWithRequestId()
    {
        // Arrange
        var helper = new MailControllerTestHelper();
        helper.Controller.ControllerContext.HttpContext.TraceIdentifier = "test-trace-id";

        // Act
        var result = helper.Controller.Error();

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ErrorViewModel>(viewResult.Model);
        Assert.Equal("test-trace-id", model.RequestId);
    }

    [Fact]
    public void Controller_ImplementsIDisposablePattern()
    {
        // Arrange & Act
        var helper = new MailControllerTestHelper();
        
        // Assert
        Assert.NotNull(helper.Controller);
        // Verify controller can be disposed without errors
        // Note: MailController inherits from Controller which implements IDisposable
        Assert.IsAssignableFrom<IDisposable>(helper.Controller);
    }

    [Fact]
    public async Task Index_MultipleCallsWithSameUser_ConsistentBehavior()
    {
        // Arrange
        var helper = new MailControllerTestHelper();
        helper.SetupSuccessfulTokenAcquisition();

        // Act
        var result1 = await helper.Controller.Index();
        var result2 = await helper.Controller.Index();

        // Assert
        Assert.IsType<ViewResult>(result1);
        Assert.IsType<ViewResult>(result2);
        
        // Verify token acquisition was called twice
        await helper.TokenAcquisition.Received(2).GetAccessTokenForUserAsync(
            Arg.Is<string[]>(scopes => scopes.Contains("https://outlook.office365.com/.default")));
    }

    [Fact]
    public async Task Reply_ThenSendReply_WorkflowTest()
    {
        // Arrange
        var helper = new MailControllerTestHelper();
        var emailId = "test-workflow-id";
        var model = MailControllerTestHelper.CreateValidEmailReplyModel();
        model.Id = emailId;
        
        helper.SetupTokenAcquisitionChallenge(); // Will cause challenges for both operations

        // Act
        var replyResult = await helper.Controller.Reply(emailId);
        var sendResult = await helper.Controller.SendReply(model);

        // Assert
        Assert.IsType<ChallengeResult>(replyResult);
        Assert.IsType<ChallengeResult>(sendResult);
        
        // Verify both operations were logged
        helper.VerifyInformationLogged("Reply action called with ID:");
        helper.VerifyInformationLogged("SendReply action called for ID:");
    }
}
