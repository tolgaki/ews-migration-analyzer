using Microsoft.Exchange.WebServices.Data;
using Microsoft.Identity.Web;

namespace Contoso.Mail.Web.Services;

/// <summary>
/// Factory for creating and configuring Exchange Web Services instances.
/// Handles authentication token acquisition and service configuration.
/// </summary>
public class ExchangeServiceFactory : IExchangeServiceFactory
{
    private readonly IConfiguration _config;
    private readonly ITokenAcquisition _tokenAcquisition;
    private readonly ILogger<ExchangeServiceFactory> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExchangeServiceFactory"/> class.
    /// </summary>
    /// <param name="config">The application configuration.</param>
    /// <param name="tokenAcquisition">The token acquisition service for authentication.</param>
    /// <param name="logger">The logger instance.</param>
    public ExchangeServiceFactory(IConfiguration config, ITokenAcquisition tokenAcquisition, ILogger<ExchangeServiceFactory> logger)
    {
        _config = config;
        _tokenAcquisition = tokenAcquisition;
        _logger = logger;
    }

    /// <summary>
    /// Creates and configures an ExchangeService instance for the specified user.
    /// </summary>
    /// <param name="userEmail">The email address of the user.</param>
    /// <returns>A configured ExchangeService instance.</returns>
    public async Task<ExchangeService> CreateServiceAsync(string userEmail)
    {
        var ewsUrl = _config["Ews:Url"] ?? "https://outlook.office365.com/EWS/Exchange.asmx";
        var service = new ExchangeService(ExchangeVersion.Exchange2013_SP1)
        {
            Url = new Uri(ewsUrl)
        };

        _logger.LogDebug("Creating Exchange service for user: {UserEmail} with URL: {EwsUrl}", userEmail, ewsUrl);

        // Acquire token for EWS
        var accessToken = await _tokenAcquisition.GetAccessTokenForUserAsync(new[] { "https://outlook.office365.com/.default" });
        service.Credentials = new OAuthCredentials(accessToken);

        _logger.LogDebug("Exchange service created and authenticated for user: {UserEmail}", userEmail);

        return service;
    }
}