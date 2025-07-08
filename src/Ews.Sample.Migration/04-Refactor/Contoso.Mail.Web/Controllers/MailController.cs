using System.Diagnostics;
using System.Security.Claims;
using Contoso.Mail.Models;
using Contoso.Mail.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Exchange.WebServices.Data;
using Microsoft.Identity.Web;
using Task = System.Threading.Tasks.Task;
using DomainEmailMessage = Contoso.Mail.Models.EmailMessage;

namespace Contoso.Mail.Controllers;

/// <summary>
/// Controller for handling mailbox operations such as viewing, replying, and sending replies to emails using the email service layer.
/// </summary>
[Authorize]
public class MailController : Controller
{
    private readonly IEmailService _emailService;
    private readonly ILogger<MailController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MailController"/> class.
    /// </summary>
    /// <param name="emailService">The email service for handling email operations.</param>
    /// <param name="logger">The logger instance.</param>
    public MailController(IEmailService emailService, ILogger<MailController> logger)
    {
        _emailService = emailService;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves and displays the first 10 emails from the user's mailbox inbox.
    /// </summary>
    /// <returns>An <see cref="IActionResult"/> that renders the mailbox view with a list of recent emails, or an error view if retrieval fails.</returns>
    public async Task<IActionResult> Index()
    {
        var user = User as ClaimsPrincipal;
        var email = user?.FindFirst(ClaimTypes.Upn)?.Value ?? user?.FindFirst("preferred_username")?.Value;
        if (string.IsNullOrEmpty(email))
        {
            return Unauthorized();
        }

        var displayName = user?.FindFirst("name")?.Value ?? email;
        ViewBag.DisplayName = displayName;
        ViewBag.Email = email;

        try
        {
            var mailItems = await _emailService.GetInboxEmailsAsync(email, 10);
            return View(mailItems);
        }
        catch (MicrosoftIdentityWebChallengeUserException)
        {
            // Challenge the user interactively if consent or login is required
            return Challenge();
        }
        catch (Microsoft.Identity.Client.MsalUiRequiredException)
        {
            // Challenge the user interactively if consent or login is required
            return Challenge();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving mailbox items");
            ModelState.AddModelError("", "Error retrieving mailbox items: " + ex.Message);
            return View(new List<DomainEmailMessage>());
        }
    }

    /// <summary>
    /// Displays a reply form for a specific email, pre-filling the recipient, subject, and quoted message.
    /// </summary>
    /// <param name="id">The unique identifier of the email to reply to (EWS UniqueId).</param>
    /// <returns>An <see cref="IActionResult"/> that renders the reply view with the email pre-filled for response, or redirects to the mailbox if not found.</returns>
    [HttpGet]
    public async Task<IActionResult> Reply(string id)
    {
        _logger.LogInformation("Reply action called with ID: {Id}", id);

        if (string.IsNullOrWhiteSpace(id))
        {
            _logger.LogWarning("Email ID is null or empty");
            return BadRequest("Email ID is required");
        }

        var user = User as ClaimsPrincipal;
        var email = user?.FindFirst(ClaimTypes.Upn)?.Value ?? user?.FindFirst("preferred_username")?.Value;
        if (string.IsNullOrEmpty(email))
        {
            return Unauthorized();
        }

        try
        {
            var replyModel = await _emailService.CreateReplyModelAsync(id, email);
            if (replyModel == null)
            {
                _logger.LogWarning("Email with ID {EmailId} not found", id);
                TempData["ErrorMessage"] = "Email not found. It may have been moved or deleted.";
                return RedirectToAction(nameof(Index));
            }

            _logger.LogInformation("Reply model created, returning view");
            return View(replyModel);
        }
        catch (MicrosoftIdentityWebChallengeUserException ex)
        {
            _logger.LogWarning(ex, "Authentication challenge required");
            return Challenge();
        }
        catch (Microsoft.Identity.Client.MsalUiRequiredException ex)
        {
            _logger.LogWarning(ex, "Authentication challenge required for MSAL");
            return Challenge();
        }
        catch (ServiceResponseException ex)
        {
            _logger.LogError(ex, "EWS service response error");
            ModelState.AddModelError(string.Empty, $"EWS service error: {ex.Message}");
            TempData["ErrorMessage"] = $"Could not retrieve email: {ex.Message}";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in Reply action");
            ModelState.AddModelError(string.Empty, $"Error retrieving email: {ex.Message}");
            TempData["ErrorMessage"] = $"Error: {ex.Message}";
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// Sends the user's reply to the selected email.
    /// </summary>
    /// <param name="model">The <see cref="EmailReplyModel"/> containing reply details including recipient, subject, and message body.</param>
    /// <returns>An <see cref="IActionResult"/> that redirects to the mailbox on success, or returns the reply view with errors on failure.</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendReply(EmailReplyModel model)
    {
        _logger.LogInformation("SendReply action called for ID: {Id}", model.Id);

        if (!ModelState.IsValid)
        {
            _logger.LogWarning("Model state is invalid");
            return View("Reply", model);
        }

        var user = User as ClaimsPrincipal;
        var email = user?.FindFirst(ClaimTypes.Upn)?.Value ?? user?.FindFirst("preferred_username")?.Value;
        if (string.IsNullOrEmpty(email))
        {
            return Unauthorized();
        }

        try
        {
            var success = await _emailService.SendReplyAsync(model, email);
            if (success)
            {
                TempData["SuccessMessage"] = "Reply sent successfully!";
                return RedirectToAction(nameof(Index));
            }
            else
            {
                ModelState.AddModelError(string.Empty, "Email not found. It may have been moved or deleted.");
                return View("Reply", model);
            }
        }
        catch (MicrosoftIdentityWebChallengeUserException ex)
        {
            _logger.LogWarning(ex, "Authentication challenge required");
            return Challenge();
        }
        catch (Microsoft.Identity.Client.MsalUiRequiredException ex)
        {
            _logger.LogWarning(ex, "Authentication challenge required for MSAL");
            return Challenge();
        }
        catch (ServiceResponseException ex)
        {
            _logger.LogError(ex, "EWS service response error while sending reply");
            ModelState.AddModelError(string.Empty, $"EWS service error: {ex.Message}");
            return View("Reply", model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending reply");
            ModelState.AddModelError(string.Empty, $"Error sending reply: {ex.Message}");
            return View("Reply", model);
        }
    }

    /// <summary>
    /// Displays a generic error page with request details for troubleshooting.
    /// </summary>
    /// <returns>An <see cref="IActionResult"/> that renders the error view with request information.</returns>
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
