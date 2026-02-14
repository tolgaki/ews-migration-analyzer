using System.Text.Json;
using Xunit;
using FluentAssertions;
using Ews.Analyzer;
using Ews.Analyzer.McpService;

namespace Ews.Analyzer.McpService.Tests;

public class DeterministicTransformerTests
{
    private readonly DeterministicTransformer _transformer;

    public DeterministicTransformerTests()
    {
        var navigator = new EwsMigrationNavigator();
        _transformer = new DeterministicTransformer(navigator);
    }

    [Fact]
    public void Transform_FindItems_ReturnsGraphListMessages()
    {
        var code = "service.FindItems(WellKnownFolderName.Inbox, view)";
        var result = _transformer.Transform(
            code,
            "Microsoft.Exchange.WebServices.Data.ExchangeService.FindItems",
            10);

        result.Should().NotBeNull();
        result!.Tier.Should().Be(1);
        result.Confidence.Should().Be("high");
        result.ConvertedCode.Should().Contain("graphClient.Me.Messages.GetAsync");
        result.RequiredUsings.Should().Contain("Microsoft.Graph");
        result.GraphApiName.Should().Be("List messages");
    }

    [Fact]
    public void Transform_EmailMessageSend_ReturnsGraphSendMail()
    {
        var code = "email.Send()";
        var result = _transformer.Transform(
            code,
            "Microsoft.Exchange.WebServices.Data.EmailMessage.Send",
            5);

        result.Should().NotBeNull();
        result!.ConvertedCode.Should().Contain("SendMail");
        result.ConvertedCode.Should().Contain("PostAsync");
    }

    [Fact]
    public void Transform_EmailMessageDelete_ReturnsGraphDeleteMessage()
    {
        var code = "message.Delete(DeleteMode.MoveToDeletedItems)";
        var result = _transformer.Transform(
            code,
            "Microsoft.Exchange.WebServices.Data.EmailMessage.Delete",
            15);

        result.Should().NotBeNull();
        result!.ConvertedCode.Should().Contain("DeleteAsync");
        result.ConvertedCode.Should().Contain("Messages");
    }

    [Fact]
    public void Transform_FindAppointments_ReturnsGraphListEvents()
    {
        var code = "service.FindAppointments(calendarId, view)";
        var result = _transformer.Transform(
            code,
            "Microsoft.Exchange.WebServices.Data.ExchangeService.FindAppointments",
            20);

        result.Should().NotBeNull();
        result!.ConvertedCode.Should().Contain("Events.GetAsync");
    }

    [Fact]
    public void Transform_AppointmentSave_ReturnsGraphCreateEvent()
    {
        var code = "appointment.Save(SendInvitationsMode.SendToNone)";
        var result = _transformer.Transform(
            code,
            "Microsoft.Exchange.WebServices.Data.Appointment.Save",
            8);

        result.Should().NotBeNull();
        result!.ConvertedCode.Should().Contain("Events.PostAsync");
    }

    [Fact]
    public void Transform_ContactBind_ReturnsGraphGetContact()
    {
        var code = "Contact.Bind(service, contactId)";
        var result = _transformer.Transform(
            code,
            "Microsoft.Exchange.WebServices.Data.Contact.Bind",
            12);

        result.Should().NotBeNull();
        result!.ConvertedCode.Should().Contain("Contacts");
        result.ConvertedCode.Should().Contain("GetAsync");
    }

    [Fact]
    public void Transform_EmailMessageMove_ReturnsGraphMoveMessage()
    {
        var code = "email.Move(folderId)";
        var result = _transformer.Transform(
            code,
            "Microsoft.Exchange.WebServices.Data.EmailMessage.Move",
            7);

        result.Should().NotBeNull();
        result!.ConvertedCode.Should().Contain("Move.PostAsync");
    }

    [Fact]
    public void Transform_EmailMessageCopy_ReturnsGraphCopyMessage()
    {
        var code = "email.Copy(folderId)";
        var result = _transformer.Transform(
            code,
            "Microsoft.Exchange.WebServices.Data.EmailMessage.Copy",
            9);

        result.Should().NotBeNull();
        result!.ConvertedCode.Should().Contain("Copy.PostAsync");
    }

    [Fact]
    public void Transform_GetInboxRules_ReturnsGraphMessageRules()
    {
        var code = "service.GetInboxRules()";
        var result = _transformer.Transform(
            code,
            "Microsoft.Exchange.WebServices.Data.ExchangeService.GetInboxRules",
            3);

        result.Should().NotBeNull();
        result!.ConvertedCode.Should().Contain("MessageRules.GetAsync");
    }

    [Fact]
    public void Transform_SyncFolderItems_ReturnsGraphDelta()
    {
        var code = "service.SyncFolderItems(folderId, null, 512, SyncFolderItemsScope.NormalItems, syncState)";
        var result = _transformer.Transform(
            code,
            "Microsoft.Exchange.WebServices.Data.ExchangeService.SyncFolderItems",
            42);

        result.Should().NotBeNull();
        result!.ConvertedCode.Should().Contain("Delta");
    }

    [Fact]
    public void Transform_UnknownOperation_ReturnsNull()
    {
        var code = "service.SomethingUnknown()";
        var result = _transformer.Transform(
            code,
            "Microsoft.Exchange.WebServices.Data.ExchangeService.SomethingUnknown",
            1);

        result.Should().BeNull();
    }

    [Fact]
    public void Transform_UnsupportedExample_ReturnsNull()
    {
        var code = "service.LegacyCall()";
        var result = _transformer.Transform(
            code,
            "Microsoft.Exchange.WebServices.Data.Legacy.LegacyCall",
            1);

        // Gap operation with no parity should return null
        result.Should().BeNull();
    }

    [Fact]
    public void TransformAuth_ExchangeService_ReturnsGraphServiceClient()
    {
        var code = @"var service = new ExchangeService(ExchangeVersion.Exchange2013);
service.Credentials = new WebCredentials(""user@example.com"", ""password"");";

        var result = DeterministicTransformer.TransformAuth(code, "clientCredential");

        result.Should().NotBeNull();
        result!.ConvertedCode.Should().Contain("ClientSecretCredential");
        result.ConvertedCode.Should().Contain("GraphServiceClient");
        result.RequiredUsings.Should().Contain("Azure.Identity");
    }

    [Fact]
    public void TransformAuth_Interactive_ReturnsInteractiveBrowser()
    {
        var code = "var service = new ExchangeService();";
        var result = DeterministicTransformer.TransformAuth(code, "interactive");

        result.Should().NotBeNull();
        result!.ConvertedCode.Should().Contain("InteractiveBrowserCredential");
    }

    [Fact]
    public void TransformAuth_ManagedIdentity_ReturnsManagedIdentityCredential()
    {
        var code = "var service = new ExchangeService();";
        var result = DeterministicTransformer.TransformAuth(code, "managedIdentity");

        result.Should().NotBeNull();
        result!.ConvertedCode.Should().Contain("ManagedIdentityCredential");
    }

    [Fact]
    public void TransformAuth_NoExchangeService_ReturnsNull()
    {
        var code = "var client = new HttpClient();";
        var result = DeterministicTransformer.TransformAuth(code);

        result.Should().BeNull();
    }

    [Fact]
    public void Transform_TaskBind_ReturnsGraphGetTask()
    {
        var code = "Task.Bind(service, taskId)";
        var result = _transformer.Transform(
            code,
            "Microsoft.Exchange.WebServices.Data.Task.Bind",
            5);

        result.Should().NotBeNull();
        result!.ConvertedCode.Should().Contain("Todo");
        result.ConvertedCode.Should().Contain("GetAsync");
    }

    [Fact]
    public void Transform_SubscribeToStreamingNotifications_ReturnsGraphSubscription()
    {
        var code = "service.SubscribeToStreamingNotifications(new FolderId[] { WellKnownFolderName.Inbox }, EventType.NewMail)";
        var result = _transformer.Transform(
            code,
            "Microsoft.Exchange.WebServices.Data.ExchangeService.SubscribeToStreamingNotifications",
            25);

        result.Should().NotBeNull();
        result!.ConvertedCode.Should().Contain("Subscriptions.PostAsync");
    }
}
