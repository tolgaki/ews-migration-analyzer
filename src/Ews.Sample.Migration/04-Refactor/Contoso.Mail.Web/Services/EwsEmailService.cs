using Contoso.Mail.Models;
using Microsoft.Exchange.WebServices.Data;
using Task = System.Threading.Tasks.Task;

namespace Contoso.Mail.Web.Services;

/// <summary>
/// Service for handling email operations using Exchange Web Services.
/// Provides methods for retrieving emails, creating replies, and sending messages.
/// </summary>
public class EwsEmailService : IEmailService
{
    private readonly IExchangeServiceFactory _exchangeServiceFactory;
    private readonly ILogger<EwsEmailService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="EwsEmailService"/> class.
    /// </summary>
    /// <param name="exchangeServiceFactory">The factory for creating Exchange service instances.</param>
    /// <param name="logger">The logger instance.</param>
    public EwsEmailService(IExchangeServiceFactory exchangeServiceFactory, ILogger<EwsEmailService> logger)
    {
        _exchangeServiceFactory = exchangeServiceFactory;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves a list of emails from the user's inbox.
    /// </summary>
    /// <param name="userEmail">The email address of the user.</param>
    /// <param name="count">The maximum number of emails to retrieve.</param>
    /// <returns>A list of email messages.</returns>
    public async Task<IList<EmailMessage>> GetInboxEmailsAsync(string userEmail, int count = 10)
    {
        _logger.LogInformation("Retrieving {Count} emails from inbox for user: {UserEmail}", count, userEmail);

        var service = await _exchangeServiceFactory.CreateServiceAsync(userEmail);

        // Find mail items in Inbox
        var findResults = await Task.Run(() => service.FindItems(WellKnownFolderName.Inbox, new ItemView(count)));
        var mailItems = findResults.Items.OfType<EmailMessage>().ToList();

        // Add debug logging for message IDs
        foreach (var mail in mailItems)
        {
            _logger.LogDebug("Email ID: {EmailId}, UniqueId: {UniqueId}, ChangeKey: {ChangeKey}, Subject: {Subject}",
                mail.Id, mail.Id.UniqueId, mail.Id.ChangeKey, mail.Subject);
        }

        _logger.LogInformation("Retrieved {ActualCount} emails from inbox for user: {UserEmail}", mailItems.Count, userEmail);

        return mailItems;
    }

    /// <summary>
    /// Retrieves a specific email by its unique identifier.
    /// </summary>
    /// <param name="emailId">The unique identifier of the email.</param>
    /// <returns>The email message if found, null otherwise.</returns>
    public Task<EmailMessage?> GetEmailByIdAsync(string emailId)
    {
        _logger.LogInformation("Retrieving email with ID: {EmailId}", emailId);

        if (string.IsNullOrWhiteSpace(emailId))
        {
            _logger.LogWarning("Email ID is null or empty");
            return Task.FromResult<EmailMessage?>(null);
        }

        // Note: We need a user email to create the service, but this method doesn't have it.
        // This is a limitation of the current design that should be addressed in a future refactor.
        // For now, we'll throw an exception to indicate this limitation.
        throw new InvalidOperationException("GetEmailByIdAsync requires user context. Use GetEmailByIdAsync(string emailId, string userEmail) instead.");
    }

    /// <summary>
    /// Retrieves a specific email by its unique identifier for a specific user.
    /// </summary>
    /// <param name="emailId">The unique identifier of the email.</param>
    /// <param name="userEmail">The email address of the user.</param>
    /// <returns>The email message if found, null otherwise.</returns>
    public async Task<EmailMessage?> GetEmailByIdAsync(string emailId, string userEmail)
    {
        _logger.LogInformation("Retrieving email with ID: {EmailId} for user: {UserEmail}", emailId, userEmail);

        if (string.IsNullOrWhiteSpace(emailId))
        {
            _logger.LogWarning("Email ID is null or empty");
            return null;
        }

        var service = await _exchangeServiceFactory.CreateServiceAsync(userEmail);

        // URL decode the ID before using it in the filter
        string decodedId = Uri.UnescapeDataString(emailId);
        _logger.LogDebug("Searching for email with decoded UniqueId: {UniqueId}", decodedId);

        // Create a search filter
        var filter = new SearchFilter.IsEqualTo(ItemSchema.Id, decodedId);
        var view = new ItemView(1);

        // Find the email
        var findResults = await Task.Run(() => service.FindItems(WellKnownFolderName.Inbox, filter, view));

        if (findResults.Items.Count == 0)
        {
            _logger.LogWarning("Email with UniqueId {UniqueId} not found", decodedId);
            return null;
        }

        // Bind to the email with full details
        var emailItemId = findResults.Items[0].Id;
        _logger.LogDebug("Found email with ID: {EmailId}, binding with full properties", emailItemId);

        var propertySet = new PropertySet(BasePropertySet.FirstClassProperties)
        {
            RequestedBodyType = BodyType.Text
        };

        var email = await Task.Run(() => EmailMessage.Bind(service, emailItemId, propertySet));
        _logger.LogDebug("Successfully bound to email: {Subject}", email.Subject);

        return email;
    }

    /// <summary>
    /// Creates a reply model for a specific email.
    /// </summary>
    /// <param name="emailId">The unique identifier of the email to reply to.</param>
    /// <returns>A reply model pre-populated with email details.</returns>
    public Task<EmailReplyModel?> CreateReplyModelAsync(string emailId)
    {
        // This method has the same limitation as GetEmailByIdAsync - it needs user context
        throw new InvalidOperationException("CreateReplyModelAsync requires user context. Use CreateReplyModelAsync(string emailId, string userEmail) instead.");
    }

    /// <summary>
    /// Creates a reply model for a specific email for a specific user.
    /// </summary>
    /// <param name="emailId">The unique identifier of the email to reply to.</param>
    /// <param name="userEmail">The email address of the user.</param>
    /// <returns>A reply model pre-populated with email details.</returns>
    public async Task<EmailReplyModel?> CreateReplyModelAsync(string emailId, string userEmail)
    {
        _logger.LogInformation("Creating reply model for email ID: {EmailId} for user: {UserEmail}", emailId, userEmail);

        var email = await GetEmailByIdAsync(emailId, userEmail);
        if (email == null)
        {
            _logger.LogWarning("Could not find email with ID: {EmailId} for reply", emailId);
            return null;
        }

        var replyModel = new EmailReplyModel
        {
            Id = email.Id.UniqueId, // Store the UniqueId
            Subject = $"RE: {email.Subject}",
            To = email.From.Address,
            Body = $"Hello from EWS!\n\n----------\nFrom: {email.From.Name} ({email.From.Address})\nSent: {email.DateTimeSent:g}\nSubject: {email.Subject}\n\n{email.Body}"
        };

        _logger.LogDebug("Reply model created for email: {Subject}", email.Subject);
        return replyModel;
    }

    /// <summary>
    /// Sends a reply to an email.
    /// </summary>
    /// <param name="replyModel">The reply model containing the reply details.</param>
    /// <returns>True if the reply was sent successfully, false otherwise.</returns>
    public Task<bool> SendReplyAsync(EmailReplyModel replyModel)
    {
        // This method has the same limitation - it needs user context
        throw new InvalidOperationException("SendReplyAsync requires user context. Use SendReplyAsync(EmailReplyModel replyModel, string userEmail) instead.");
    }

    /// <summary>
    /// Sends a reply to an email for a specific user.
    /// </summary>
    /// <param name="replyModel">The reply model containing the reply details.</param>
    /// <param name="userEmail">The email address of the user.</param>
    /// <returns>True if the reply was sent successfully, false otherwise.</returns>
    public async Task<bool> SendReplyAsync(EmailReplyModel replyModel, string userEmail)
    {
        _logger.LogInformation("Sending reply for email ID: {EmailId} from user: {UserEmail}", replyModel.Id, userEmail);

        var service = await _exchangeServiceFactory.CreateServiceAsync(userEmail);

        // URL decode the ID before using it in the filter
        string decodedId = Uri.UnescapeDataString(replyModel.Id);
        _logger.LogDebug("Searching for email with decoded UniqueId: {UniqueId}", decodedId);

        // Create a search filter
        var filter = new SearchFilter.IsEqualTo(ItemSchema.Id, decodedId);
        var view = new ItemView(1);

        // Find the email
        var findResults = await Task.Run(() => service.FindItems(WellKnownFolderName.Inbox, filter, view));

        if (findResults.Items.Count == 0)
        {
            _logger.LogWarning("Email with UniqueId {UniqueId} not found for reply", decodedId);
            return false;
        }

        // Get the email ID
        var emailId = findResults.Items[0].Id;

        // Bind to the email with full details
        var propertySet = new PropertySet(BasePropertySet.FirstClassProperties);
        var originalEmail = await Task.Run(() => EmailMessage.Bind(service, emailId, propertySet));

        // Create a reply message
        _logger.LogDebug("Creating reply to email: {Subject}", originalEmail.Subject);
        var reply = await Task.Run(() => originalEmail.CreateReply(false));

        // Set the body of the reply
        reply.Body = replyModel.Body;

        // Send the reply
        _logger.LogDebug("Sending reply...");
        await Task.Run(() => reply.SendAndSaveCopy());
        _logger.LogInformation("Reply sent successfully for email: {Subject}", originalEmail.Subject);

        return true;
    }
}