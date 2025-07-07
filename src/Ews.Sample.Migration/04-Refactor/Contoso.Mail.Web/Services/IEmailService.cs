using Contoso.Mail.Models;
using Microsoft.Exchange.WebServices.Data;

namespace Contoso.Mail.Web.Services;

/// <summary>
/// Interface for email operations such as retrieving mailbox items, getting specific emails, and sending replies.
/// This abstraction allows for easier testing and potential migration from EWS to other email APIs.
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Retrieves a list of emails from the user's inbox.
    /// </summary>
    /// <param name="userEmail">The email address of the user.</param>
    /// <param name="count">The maximum number of emails to retrieve.</param>
    /// <returns>A list of email messages.</returns>
    Task<IList<EmailMessage>> GetInboxEmailsAsync(string userEmail, int count = 10);

    /// <summary>
    /// Retrieves a specific email by its unique identifier.
    /// </summary>
    /// <param name="emailId">The unique identifier of the email.</param>
    /// <returns>The email message if found, null otherwise.</returns>
    Task<EmailMessage?> GetEmailByIdAsync(string emailId);

    /// <summary>
    /// Retrieves a specific email by its unique identifier for a specific user.
    /// </summary>
    /// <param name="emailId">The unique identifier of the email.</param>
    /// <param name="userEmail">The email address of the user.</param>
    /// <returns>The email message if found, null otherwise.</returns>
    Task<EmailMessage?> GetEmailByIdAsync(string emailId, string userEmail);

    /// <summary>
    /// Creates a reply model for a specific email.
    /// </summary>
    /// <param name="emailId">The unique identifier of the email to reply to.</param>
    /// <returns>A reply model pre-populated with email details.</returns>
    Task<EmailReplyModel?> CreateReplyModelAsync(string emailId);

    /// <summary>
    /// Creates a reply model for a specific email for a specific user.
    /// </summary>
    /// <param name="emailId">The unique identifier of the email to reply to.</param>
    /// <param name="userEmail">The email address of the user.</param>
    /// <returns>A reply model pre-populated with email details.</returns>
    Task<EmailReplyModel?> CreateReplyModelAsync(string emailId, string userEmail);

    /// <summary>
    /// Sends a reply to an email.
    /// </summary>
    /// <param name="replyModel">The reply model containing the reply details.</param>
    /// <returns>True if the reply was sent successfully, false otherwise.</returns>
    Task<bool> SendReplyAsync(EmailReplyModel replyModel);

    /// <summary>
    /// Sends a reply to an email for a specific user.
    /// </summary>
    /// <param name="replyModel">The reply model containing the reply details.</param>
    /// <param name="userEmail">The email address of the user.</param>
    /// <returns>True if the reply was sent successfully, false otherwise.</returns>
    Task<bool> SendReplyAsync(EmailReplyModel replyModel, string userEmail);
}