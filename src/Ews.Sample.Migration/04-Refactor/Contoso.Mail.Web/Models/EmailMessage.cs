using System.ComponentModel.DataAnnotations;

namespace Contoso.Mail.Models;

/// <summary>
/// Represents an email message in the application domain.
/// This model abstracts away the underlying email service implementation (EWS vs Graph API).
/// </summary>
public class EmailMessage
{
    /// <summary>
    /// Gets or sets the unique identifier of the email.
    /// </summary>
    [Required]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the subject of the email.
    /// </summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the sender's email address.
    /// </summary>
    public string From { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the sender's display name.
    /// </summary>
    public string FromName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the date and time when the email was received.
    /// </summary>
    public DateTime DateTimeReceived { get; set; }

    /// <summary>
    /// Gets or sets the date and time when the email was sent.
    /// </summary>
    public DateTime? DateTimeSent { get; set; }

    /// <summary>
    /// Gets or sets the body content of the email.
    /// </summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the body preview/snippet of the email.
    /// </summary>
    public string BodyPreview { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the email has attachments.
    /// </summary>
    public bool HasAttachments { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the email has been read.
    /// </summary>
    public bool IsRead { get; set; }

    /// <summary>
    /// Gets or sets the importance level of the email.
    /// </summary>
    public string Importance { get; set; } = "Normal";

    /// <summary>
    /// Gets or sets the list of recipients (To field).
    /// </summary>
    public List<string> ToRecipients { get; set; } = new List<string>();

    /// <summary>
    /// Gets or sets the list of CC recipients.
    /// </summary>
    public List<string> CcRecipients { get; set; } = new List<string>();
}