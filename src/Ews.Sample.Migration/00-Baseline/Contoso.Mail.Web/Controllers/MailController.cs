using System.Diagnostics;
using System.Security.Claims;
using Contoso.Mail.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Exchange.WebServices.Data;
using Microsoft.Identity.Web;
using Task = System.Threading.Tasks.Task;

namespace Contoso.Mail.Controllers;

[Authorize]
public class MailController : Controller
{
    private readonly IConfiguration _config;
    private readonly ITokenAcquisition _tokenAcquisition;
    private readonly ILogger<MailController> _logger;

    public MailController(IConfiguration config, ITokenAcquisition tokenAcquisition, ILogger<MailController> logger)
    {
        _config = config;
        _tokenAcquisition = tokenAcquisition;
        _logger = logger;
    }

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

        var ewsUrl = _config["Ews:Url"] ?? "https://outlook.office365.com/EWS/Exchange.asmx";
        var service = new ExchangeService(ExchangeVersion.Exchange2013_SP1)
        {
            Url = new Uri(ewsUrl)
        };

        try
        {
            // Acquire token for EWS
            var accessToken = await _tokenAcquisition.GetAccessTokenForUserAsync(new[] { "https://outlook.office365.com/.default" });
            service.Credentials = new OAuthCredentials(accessToken);

            // Find first 10 mail items in Inbox
            var findResults = await Task.Run(() => service.FindItems(WellKnownFolderName.Inbox, new ItemView(10)));
            var mailItems = findResults.Items.OfType<EmailMessage>().ToList();

            // Add debug logging for message IDs
            foreach (var mail in mailItems)
            {
                _logger.LogInformation("Email ID: {EmailId}, UniqueId: {UniqueId}, ChangeKey: {ChangeKey}, Subject: {Subject}",
                    mail.Id, mail.Id.UniqueId, mail.Id.ChangeKey, mail.Subject);
            }

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
            return View(new List<EmailMessage>());
        }
    }

    [HttpGet]
    public async Task<IActionResult> Reply(string id)
    {
        _logger.LogInformation("Reply action called with ID: {Id}", id);

        if (string.IsNullOrEmpty(id))
        {
            _logger.LogWarning("Email ID is null or empty");
            return BadRequest("Email ID is required");
        }

        var ewsUrl = _config["Ews:Url"] ?? "https://outlook.office365.com/EWS/Exchange.asmx";
        var service = new ExchangeService(ExchangeVersion.Exchange2013_SP1)
        {
            Url = new Uri(ewsUrl)
        };

        try
        {
            // Acquire token for EWS
            var accessToken = await _tokenAcquisition.GetAccessTokenForUserAsync(new[] { "https://outlook.office365.com/.default" });
            service.Credentials = new OAuthCredentials(accessToken);

            // URL decode the ID before using it in the filter
            string decodedId = Uri.UnescapeDataString(id);
            _logger.LogInformation("Searching for email with decoded UniqueId: {UniqueId}", decodedId);

            // Create a search filter
            var filter = new SearchFilter.IsEqualTo(ItemSchema.Id, decodedId);
            var view = new ItemView(1);

            // Find the email
            var findResults = await Task.Run(() => service.FindItems(WellKnownFolderName.Inbox, filter, view));

            if (findResults.Items.Count == 0)
            {
                _logger.LogWarning("Email with UniqueId {UniqueId} not found", decodedId);
                TempData["ErrorMessage"] = "Email not found. It may have been moved or deleted.";
                return RedirectToAction(nameof(Index));
            }

            // Bind to the email with full details
            var emailId = findResults.Items[0].Id;
            _logger.LogInformation("Found email with ID: {EmailId}, binding with full properties", emailId);

            var propertySet = new PropertySet(BasePropertySet.FirstClassProperties)
            {
                RequestedBodyType = BodyType.Text
            };

            var email = await Task.Run(() => EmailMessage.Bind(service, emailId, propertySet));
            _logger.LogInformation("Successfully bound to email: {Subject}", email.Subject);

            // Create the reply model
            var replyModel = new EmailReplyModel
            {
                Id = email.Id.UniqueId, // Store the UniqueId
                Subject = $"RE: {email.Subject}",
                To = email.From.Address,
                Body = $"Hello from EWS!\n\n----------\nFrom: {email.From.Name} ({email.From.Address})\nSent: {email.DateTimeSent:g}\nSubject: {email.Subject}\n\n{email.Body}"
            };

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

        var ewsUrl = _config["Ews:Url"] ?? "https://outlook.office365.com/EWS/Exchange.asmx";
        var service = new ExchangeService(ExchangeVersion.Exchange2013_SP1)
        {
            Url = new Uri(ewsUrl)
        };

        try
        {
            // Acquire token for EWS
            var accessToken = await _tokenAcquisition.GetAccessTokenForUserAsync(new[] { "https://outlook.office365.com/.default" });
            service.Credentials = new OAuthCredentials(accessToken);

            // URL decode the ID before using it in the filter
            string decodedId = Uri.UnescapeDataString(model.Id);
            _logger.LogInformation("Searching for email with decoded UniqueId: {UniqueId}", decodedId);

            // Create a search filter
            var filter = new SearchFilter.IsEqualTo(ItemSchema.Id, decodedId);
            var view = new ItemView(1);

            // Find the email
            var findResults = await Task.Run(() => service.FindItems(WellKnownFolderName.Inbox, filter, view));

            if (findResults.Items.Count == 0)
            {
                _logger.LogWarning("Email with UniqueId {UniqueId} not found for reply", decodedId);
                ModelState.AddModelError(string.Empty, "Email not found. It may have been moved or deleted.");
                return View("Reply", model);
            }

            // Get the email ID
            var emailId = findResults.Items[0].Id;

            // Bind to the email with full details
            var propertySet = new PropertySet(BasePropertySet.FirstClassProperties);
            var originalEmail = await Task.Run(() => EmailMessage.Bind(service, emailId, propertySet));

            // Create a reply message
            _logger.LogInformation("Creating reply to email: {Subject}", originalEmail.Subject);
            var reply = await Task.Run(() => originalEmail.CreateReply(false));

            // Set the body of the reply
            reply.Body = model.Body;

            // Send the reply
            _logger.LogInformation("Sending reply...");
            await Task.Run(() => reply.SendAndSaveCopy());
            _logger.LogInformation("Reply sent successfully");

            TempData["SuccessMessage"] = "Reply sent successfully!";
            return RedirectToAction(nameof(Index));
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

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
