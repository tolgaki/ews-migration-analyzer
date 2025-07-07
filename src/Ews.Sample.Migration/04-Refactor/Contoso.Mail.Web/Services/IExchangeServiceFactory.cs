using Microsoft.Exchange.WebServices.Data;

namespace Contoso.Mail.Web.Services;

/// <summary>
/// Interface for creating and configuring Exchange Web Services instances.
/// This abstraction allows for easier testing and configuration management.
/// </summary>
public interface IExchangeServiceFactory
{
    /// <summary>
    /// Creates and configures an ExchangeService instance for the specified user.
    /// </summary>
    /// <param name="userEmail">The email address of the user.</param>
    /// <returns>A configured ExchangeService instance.</returns>
    Task<ExchangeService> CreateServiceAsync(string userEmail);
}