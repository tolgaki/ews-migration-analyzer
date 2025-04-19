using Microsoft.Exchange.WebServices.Autodiscover;
using Microsoft.Identity.Client;
using Task = System.Threading.Tasks.Task;

namespace EwsTestApp;

public class EwsMetaData
{
    private readonly IPublicClientApplication m365Client;

    public EwsMetaData(IPublicClientApplication m365Client)
    {
        this.m365Client = m365Client;
    }

    private AutodiscoverService? AutoDiscoverClient { get; set; }

    public Uri? ExternalEwsUrl { get; private set; }

    public async Task<GetUserSettingsResponse> GetUserSettings(string emailAddress, int maxHops,
        params UserSettingName[] requestedUserSettings)
    {
        if (AutoDiscoverClient is null) await CreateAutoDiscoverClient();

        for (var attempt = 0; attempt < maxHops; attempt++)
        {

            AutoDiscoverClient!.EnableScpLookup = attempt < 2;

            if (requestedUserSettings.Length == 0) requestedUserSettings = GetDefaultUserSettings();
            try
            {
                var response = AutoDiscoverClient.GetUserSettings(emailAddress, requestedUserSettings);
                switch (response.ErrorCode)
                {
                    case AutodiscoverErrorCode.RedirectAddress:
                    case AutodiscoverErrorCode.RedirectUrl:
                        new Uri(response.RedirectTarget);
                        break;
                    case AutodiscoverErrorCode.InvalidUser:
                        Console.WriteLine("Error: Invalid User");
                        break;
                    case AutodiscoverErrorCode.InvalidRequest:
                        Console.WriteLine("Error: Invalid Invalid Request");
                        break;
                    case AutodiscoverErrorCode.InvalidSetting:
                        Console.WriteLine("Error: Invalid Invalid Settings");
                        break;
                    case AutodiscoverErrorCode.SettingIsNotAvailable:
                        Console.WriteLine("Error: Setting is not available");
                        break;
                    case AutodiscoverErrorCode.ServerBusy:
                        Console.WriteLine("Error: Server is busy");
                        break;
                    case AutodiscoverErrorCode.InvalidDomain:
                        Console.WriteLine("Error: Invalid domain");
                        break;
                    case AutodiscoverErrorCode.NotFederated:
                        Console.WriteLine("Error: Not federated");
                        break;
                    case AutodiscoverErrorCode.InternalServerError:
                        Console.WriteLine("Error: Internal Server Error");
                        break;
                    case AutodiscoverErrorCode.NoError:
                        ExternalEwsUrl = new Uri(response.Settings[UserSettingName.ExternalEwsUrl].ToString() ??
                                                 string.Empty);
                        return response;
                    default:
                        Console.WriteLine(
                            $"Warning: Ended up in default case. This should not be: {response.ErrorCode}");
                        break;
                }

            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        throw new Exception("No suitable Autodiscover endpoint was found.");
    }


    private async Task CreateAutoDiscoverClient()
    {
        var auth = new EwsAuthentication();
        var credentials = await auth.AuthenticateToEws(m365Client);

        AutoDiscoverClient = new AutodiscoverService
        {
            Url = new Uri("https://outlook.office365.com/EWS/Exchange.asmx"),
            Credentials = credentials
        };
    }

    public static UserSettingName[] GetDefaultUserSettings()
    {
        var requestedUserSettings = new[]
        {
            UserSettingName.UserDisplayName,
            UserSettingName.UserDN,
            UserSettingName.InternalEwsUrl,
            UserSettingName.ExternalEwsUrl
        };
        return requestedUserSettings;
    }
}