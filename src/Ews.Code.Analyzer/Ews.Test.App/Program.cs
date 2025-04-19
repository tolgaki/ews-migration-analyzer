/*
MIT License

    Copyright (c) Microsoft Corporation.

    Permission is hereby granted, free of charge, to any person obtaining a copy
    of this software and associated documentation files (the "Software"), to deal
    in the Software without restriction, including without limitation the rights
    to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
    copies of the Software, and to permit persons to whom the Software is
    furnished to do so, subject to the following conditions:

    The above copyright notice and this permission notice shall be included in all
    copies or substantial portions of the Software.

    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
    IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
    AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
    OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
    SOFTWARE
*/
using EwsTestApp;
using Microsoft.Exchange.WebServices.Autodiscover;
using Microsoft.Exchange.WebServices.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Identity.Client;
using System.Reflection;

IConfiguration config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", true, true)
    .AddJsonFile("appsettings.local.json", false, true)
    .AddUserSecrets(Assembly.GetExecutingAssembly(), true, true)
    .Build();

// Configure the MSAL client to get tokens
var pcaOptions = new PublicClientApplicationOptions
{
    ClientId = config["ApplicationId"] ?? "",
    TenantId = config["TenantId"] ?? ""
};

// Configure Mail Options
var mailOptions = new MailOptions
{
    EmailDomain = config["EmailDomain"] ?? "",
    SenderEmail = config["SenderEmail"] ?? "",
    ToRecipients = config["ToRecipients"] ?? "",
    CcRecipients = config["CcRecipients"] ?? ""
};

var pca = PublicClientApplicationBuilder
    .CreateWithApplicationOptions(pcaOptions)
    .WithRedirectUri("http://localhost")
    .Build();

const bool runSendMail = true;
const bool runGetUserSettings = true;
const bool runExport = true;

try
{

    if (runSendMail)
    {
        // Configure the ExchangeService with the access token
        var ews = new EwsService(pca);

        var folders = await ews.GetFolders();
        DisplayFolders(folders);

        // Send an email
        Console.WriteLine("Sending email...");
        var subject = "Hello, EWS!";
        var body = "This is a test email sent via EWS.";

        await ews.SendEmail(mailOptions.ToRecipients, mailOptions.CcRecipients, subject, body);

        Console.WriteLine();
    }

    if (runExport)
    {
        // Configure the ExchangeService with the access token
        var ews = new EwsService(pca);

        Console.WriteLine("Exporting mail folder");
        await ews.ExportMIMEEmail();
        Console.WriteLine();
    }

    if (runGetUserSettings)
    {
        Console.WriteLine("Getting Endpoint info from Autodiscover Service...");
        var metadataClient = new EwsMetaData(pca);

        var requestedUserSettings = EwsMetaData.GetDefaultUserSettings();

        var userSettings = await metadataClient.GetUserSettings(mailOptions.SenderEmail, 2, requestedUserSettings);
        DisplayUserSettings(userSettings);

        Console.WriteLine($"Endpoint: {metadataClient.ExternalEwsUrl}");
        Console.WriteLine();
    }

}
catch (MsalException ex)
{
    Console.WriteLine($"Error acquiring access token: {ex}");
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex}");
}

if (System.Diagnostics.Debugger.IsAttached)
{
    Console.WriteLine("Hit any key to exit...");
    Console.ReadKey();
}

void DisplayFolders(FindFoldersResults findFoldersResults)
{
    foreach (var folder in findFoldersResults)
    {
        Console.WriteLine($"Folder: {folder.DisplayName}");
    }
}

void DisplayUserSettings(GetUserSettingsResponse getUserSettingsResponse)
{
    // Display each retrieved value. The settings are part of a key/value pair.
    // userresponse is a GetUserSettingsResponse object.
    foreach (var usersetting in getUserSettingsResponse.Settings)
    {
        Console.WriteLine(usersetting.Key.ToString() + ": " + usersetting.Value);
    }

    Console.WriteLine();
}