using Contoso.Mail.Models;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Identity.Web;
using Microsoft.Identity.Client;
using Task = System.Threading.Tasks.Task;

namespace Contoso.Mail.Web.Services;

/// <summary>
/// Service for handling email operations using Microsoft Graph API.
/// Provides methods for retrieving emails, creating replies, and sending messages.
/// </summary>
public class GraphEmailService : IEmailService
{
    private readonly GraphServiceClient _graphServiceClient;
    private readonly ILogger<GraphEmailService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphEmailService"/> class.
    /// </summary>
    /// <param name="graphServiceClient">The Graph service client for making API calls.</param>
    /// <param name="logger">The logger instance.</param>
    public GraphEmailService(GraphServiceClient graphServiceClient, ILogger<GraphEmailService> logger)
    {
        _graphServiceClient = graphServiceClient;
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

        try
        {
            var messages = await _graphServiceClient.Me.Messages
                .GetAsync(requestConfiguration =>
                {
                    requestConfiguration.QueryParameters.Select = ["id", "subject", "from", "receivedDateTime", "bodyPreview", "sender", "toRecipients", "hasAttachments", "importance", "isRead", "sentDateTime"];
                    requestConfiguration.QueryParameters.Top = count;
                    requestConfiguration.QueryParameters.Orderby = ["receivedDateTime desc"];
                });

            _logger.LogInformation("Successfully retrieved {Count} emails", messages?.Value?.Count ?? 0);

            // Convert Graph messages to domain EmailMessage objects
            var emailMessages = new List<EmailMessage>();
            
            if (messages?.Value != null)
            {
                foreach (var message in messages.Value)
                {
                    var emailMessage = ConvertToDomainEmailMessage(message);
                    emailMessages.Add(emailMessage);
                }
            }

            return emailMessages;
        }
        catch (ServiceException ex)
        {
            // Check if it's a 404 Not Found - just return empty list
            if (ex.ResponseStatusCode == 404)
            {
                _logger.LogWarning("No emails found for user {UserEmail}", userEmail);
                return new List<EmailMessage>();
            }
            throw;
        }
        catch (MsalUiRequiredException)
        {
            _logger.LogWarning("Authentication required for user {UserEmail}", userEmail);
            throw;
        }
        catch (MicrosoftIdentityWebChallengeUserException)
        {
            _logger.LogWarning("User challenge required for user {UserEmail}", userEmail);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve emails for user {UserEmail}", userEmail);
            throw;
        }
    }

    /// <summary>
    /// Retrieves a specific email by its unique identifier.
    /// </summary>
    /// <param name="emailId">The unique identifier of the email.</param>
    /// <returns>The email message if found, null otherwise.</returns>
    public Task<EmailMessage?> GetEmailByIdAsync(string emailId)
    {
        _logger.LogInformation("GetEmailByIdAsync called without user context for ID: {EmailId}", emailId);

        // This method requires user context but doesn't have it in the signature
        // For Graph API, we always need user context, so we'll throw an exception
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

        try
        {
            // URL decode the ID if needed
            string decodedId = Uri.UnescapeDataString(emailId);
            
            var message = await _graphServiceClient.Me.Messages[decodedId]
                .GetAsync(requestConfiguration =>
                {
                    requestConfiguration.QueryParameters.Select = ["id", "subject", "from", "receivedDateTime", "body", "sender", "toRecipients", "hasAttachments", "importance", "isRead", "sentDateTime"];
                });

            if (message == null)
            {
                _logger.LogWarning("Email with ID {EmailId} not found", emailId);
                return null;
            }

            _logger.LogDebug("Successfully retrieved email: {Subject}", message.Subject);
            return ConvertToDomainEmailMessage(message);
        }
        catch (ServiceException ex)
        {
            // Check if it's a 404 Not Found
            if (ex.ResponseStatusCode == 404)
            {
                _logger.LogWarning("Email with ID {EmailId} not found", emailId);
                return null;
            }
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve email with ID {EmailId}", emailId);
            throw;
        }
    }

    /// <summary>
    /// Creates a reply model for a specific email.
    /// </summary>
    /// <param name="emailId">The unique identifier of the email to reply to.</param>
    /// <returns>A reply model pre-populated with email details.</returns>
    public Task<EmailReplyModel?> CreateReplyModelAsync(string emailId)
    {
        _logger.LogInformation("CreateReplyModelAsync called without user context for ID: {EmailId}", emailId);

        // This method requires user context but doesn't have it in the signature
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

        try
        {
            // URL decode the ID if needed
            string decodedId = Uri.UnescapeDataString(emailId);
            
            var message = await _graphServiceClient.Me.Messages[decodedId]
                .GetAsync(requestConfiguration =>
                {
                    requestConfiguration.QueryParameters.Select = ["id", "subject", "from", "receivedDateTime", "body", "sender", "sentDateTime"];
                });

            if (message == null)
            {
                _logger.LogWarning("Could not find email with ID: {EmailId} for reply", emailId);
                return null;
            }

            var fromEmail = message.From?.EmailAddress?.Address ?? message.Sender?.EmailAddress?.Address ?? "unknown";
            var fromName = message.From?.EmailAddress?.Name ?? message.Sender?.EmailAddress?.Name ?? fromEmail;
            var sentDate = message.SentDateTime?.ToString("g") ?? message.ReceivedDateTime?.ToString("g") ?? "unknown";
            var bodyContent = message.Body?.Content ?? "";

            var replyModel = new EmailReplyModel
            {
                Id = message.Id ?? emailId,
                Subject = $"RE: {message.Subject}",
                To = fromEmail,
                Body = $"Hello from Microsoft Graph!\n\n----------\nFrom: {fromName} ({fromEmail})\nSent: {sentDate}\nSubject: {message.Subject}\n\n{bodyContent}"
            };

            _logger.LogDebug("Reply model created for email: {Subject}", message.Subject);
            return replyModel;
        }
        catch (ServiceException ex)
        {
            // Check if it's a 404 Not Found
            if (ex.ResponseStatusCode == 404)
            {
                _logger.LogWarning("Email with ID {EmailId} not found for reply creation", emailId);
                return null;
            }
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create reply model for email ID {EmailId}", emailId);
            throw;
        }
    }

    /// <summary>
    /// Sends a reply to an email.
    /// </summary>
    /// <param name="replyModel">The reply model containing the reply details.</param>
    /// <returns>True if the reply was sent successfully, false otherwise.</returns>
    public Task<bool> SendReplyAsync(EmailReplyModel replyModel)
    {
        _logger.LogInformation("SendReplyAsync called without user context for email ID: {EmailId}", replyModel.Id);

        // This method requires user context but doesn't have it in the signature
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

        try
        {
            // URL decode the ID if needed
            string decodedId = Uri.UnescapeDataString(replyModel.Id);

            // Create the reply message
            var replyMessage = new Message
            {
                Subject = replyModel.Subject,
                Body = new ItemBody
                {
                    ContentType = BodyType.Text,
                    Content = replyModel.Body
                },
                ToRecipients = new List<Recipient>
                {
                    new Recipient
                    {
                        EmailAddress = new EmailAddress
                        {
                            Address = replyModel.To
                        }
                    }
                }
            };

            // Send the reply using Graph API
            await _graphServiceClient.Me.Messages[decodedId].Reply
                .PostAsync(new Microsoft.Graph.Me.Messages.Item.Reply.ReplyPostRequestBody
                {
                    Message = replyMessage
                });

            _logger.LogInformation("Reply sent successfully for email ID: {EmailId}", replyModel.Id);
            return true;
        }
        catch (ServiceException ex)
        {
            // Check if it's a 404 Not Found
            if (ex.ResponseStatusCode == 404)
            {
                _logger.LogWarning("Email with ID {EmailId} not found for reply", replyModel.Id);
                return false;
            }
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send reply for email ID {EmailId}", replyModel.Id);
            return false;
        }
    }

    /// <summary>
    /// Converts a Microsoft Graph Message to a domain EmailMessage.
    /// </summary>
    /// <param name="graphMessage">The Graph message to convert.</param>
    /// <returns>A domain EmailMessage object.</returns>
    private static EmailMessage ConvertToDomainEmailMessage(Message graphMessage)
    {
        var emailMessage = new EmailMessage
        {
            Id = graphMessage.Id ?? string.Empty,
            Subject = graphMessage.Subject ?? string.Empty,
            From = graphMessage.From?.EmailAddress?.Address ?? graphMessage.Sender?.EmailAddress?.Address ?? string.Empty,
            FromName = graphMessage.From?.EmailAddress?.Name ?? graphMessage.Sender?.EmailAddress?.Name ?? string.Empty,
            DateTimeReceived = graphMessage.ReceivedDateTime?.DateTime ?? DateTime.MinValue,
            DateTimeSent = graphMessage.SentDateTime?.DateTime,
            Body = graphMessage.Body?.Content ?? string.Empty,
            BodyPreview = graphMessage.BodyPreview ?? string.Empty,
            HasAttachments = graphMessage.HasAttachments ?? false,
            IsRead = graphMessage.IsRead ?? false,
            Importance = graphMessage.Importance?.ToString() ?? "Normal"
        };

        // Convert recipients
        if (graphMessage.ToRecipients != null)
        {
            emailMessage.ToRecipients = graphMessage.ToRecipients
                .Where(r => r.EmailAddress?.Address != null)
                .Select(r => r.EmailAddress!.Address!)
                .ToList();
        }

        if (graphMessage.CcRecipients != null)
        {
            emailMessage.CcRecipients = graphMessage.CcRecipients
                .Where(r => r.EmailAddress?.Address != null)
                .Select(r => r.EmailAddress!.Address!)
                .ToList();
        }

        return emailMessage;
    }
}