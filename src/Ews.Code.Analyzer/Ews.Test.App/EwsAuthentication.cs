using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Exchange.WebServices.Data;
using Microsoft.Identity.Client;

namespace EwsTestApp
{
    public class EwsAuthentication
    {
        public async Task<OAuthCredentials> AuthenticateToEws(IPublicClientApplication client)
        {
            var ewsScopes = new[] { EwsConstants.ScopeAccessAsUserAll };
            try
            {
                var authResult = await client.AcquireTokenInteractive(ewsScopes)
                    .WithUseEmbeddedWebView(false)
                    .ExecuteAsync();

                return new OAuthCredentials(authResult.AccessToken);

            }
            catch (MsalException ex)
            {
                Console.WriteLine($"Error acquiring access token: {ex}");
                throw;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

    }
}
