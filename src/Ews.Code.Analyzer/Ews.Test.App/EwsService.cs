using Microsoft.Exchange.WebServices.Data;
using Microsoft.Identity.Client;
using Task = System.Threading.Tasks.Task;

namespace EwsTestApp
{
    public class EwsService(IPublicClientApplication m365Client)
    {
        private readonly IPublicClientApplication m365Client = m365Client;
        private ExchangeService? ewsClient;

        private async Task CreateEwsClient()
        {
            var auth = new EwsAuthentication();

            var credentials = await auth.AuthenticateToEws(m365Client);

            ewsClient = new ExchangeService
            {
                Url = new Uri("https://outlook.office365.com/EWS/Exchange.asmx"),
                Credentials = credentials
            };
        }

        public async Task<FindFoldersResults> GetFolders()
        {
            if (ewsClient is null)
            {
                await CreateEwsClient();
            }

            var folders = ewsClient!.FindFolders(WellKnownFolderName.MsgFolderRoot, new FolderView(10));

            return folders;
        }

        public async Task SendEmail(string toRecipient, string ccRecipient,
            string subject, string body)
        {
            if (ewsClient?.Url is null) await CreateEwsClient();

            var email = new EmailMessage(ewsClient)
            {
                Subject = subject,
                Body = new MessageBody(body)
            };

            email.ToRecipients.Add(toRecipient);
            email.CcRecipients.Add(ccRecipient);

            email.SendAndSaveCopy();
        }

        public async Task ExportMIMEEmail()
        {
            Folder inbox = Folder.Bind(ewsClient, WellKnownFolderName.Inbox);
            ItemView view = new ItemView(1)
            {
                PropertySet = new PropertySet(BasePropertySet.IdOnly)
            };
            // This results in a FindItem call to EWS.
            FindItemsResults<Item> results = inbox.FindItems(view);
            foreach (var item in results)
            {
                PropertySet props = new PropertySet(EmailMessageSchema.MimeContent);
                // This results in a GetItem call to EWS.
                var email = EmailMessage.Bind(ewsClient, item.Id, props);

                string emlFileName = @"email.eml";
                string mhtFileName = @"email.mht";
                // Save as .eml.
                using (FileStream fs = new FileStream(emlFileName, FileMode.Create, FileAccess.Write))
                {
                    await fs.WriteAsync(email.MimeContent.Content.AsMemory(0, email.MimeContent.Content.Length));
                }
                // Save as .mht.
                using (FileStream fs = new FileStream(mhtFileName, FileMode.Create, FileAccess.Write))
                {
                    await fs.WriteAsync(email.MimeContent.Content.AsMemory(0, email.MimeContent.Content.Length));
                }
            }
        }

        static void EnableFolderPermissions(ExchangeService service)
        {
            // Create a property set to use for folder binding.
            PropertySet propSet = new PropertySet(BasePropertySet.IdOnly, FolderSchema.Permissions);
            // Specify the SMTP address of the new user and the folder permissions level.
            FolderPermission fldperm = new FolderPermission("sadie@contoso.com", FolderPermissionLevel.Editor);

            // Bind to the folder and get the current permissions.
            // This call results in a GetFolder call to EWS.
            Folder sentItemsFolder = Folder.Bind(service, WellKnownFolderName.SentItems, propSet);

            // Add the permissions for the new user to the Sent Items DACL.
            sentItemsFolder.Permissions.Add(fldperm);
            // This call results in a UpdateFolder call to EWS.
            sentItemsFolder.Update();
        }

    }
}
