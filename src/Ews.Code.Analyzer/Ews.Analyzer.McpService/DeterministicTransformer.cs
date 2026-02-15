using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Ews.Analyzer;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Ews.Analyzer.McpService;

/// <summary>
/// Tier 1: Deterministic Roslyn-based transformer that converts EWS SDK calls
/// to Microsoft Graph SDK equivalents using templates from the roadmap.
/// </summary>
internal sealed class DeterministicTransformer
{
    private readonly EwsMigrationNavigator _navigator;

    // Maps EWS SDK method patterns to Graph SDK code generators.
    // Each generator receives the invocation context and returns the replacement code, or null if it can't handle it.
    private readonly Dictionary<string, Func<InvocationContext, string?>> _transforms;

    public DeterministicTransformer(EwsMigrationNavigator navigator)
    {
        _navigator = navigator;
        _transforms = BuildTransformTable();
    }

    /// <summary>
    /// Attempt a Tier 1 deterministic transform of the given EWS invocation.
    /// Returns null if the invocation can't be handled deterministically (fall through to Tier 2).
    /// </summary>
    public ConversionResult? Transform(string code, string ewsQualifiedName, int line, string? filePath = null)
    {
        var roadmap = _navigator.GetMapByEwsSdkQualifiedName(ewsQualifiedName);
        if (roadmap == null || !roadmap.GraphApiHasParity)
            return null;

        // If roadmap has an explicit template, use template-based transform
        if (!string.IsNullOrWhiteSpace(roadmap.GraphCodeTemplate))
        {
            return TransformWithTemplate(code, roadmap, line, filePath);
        }

        // Try built-in pattern transforms
        var key = NormalizeKey(ewsQualifiedName);
        if (_transforms.TryGetValue(key, out var generator))
        {
            var ctx = new InvocationContext
            {
                Code = code,
                QualifiedName = ewsQualifiedName,
                Roadmap = roadmap,
                Line = line,
                FilePath = filePath
            };

            var converted = generator(ctx);
            if (converted != null)
            {
                return new ConversionResult
                {
                    Tier = 1,
                    Confidence = "high",
                    OriginalCode = code,
                    ConvertedCode = converted,
                    RequiredUsings = "using Microsoft.Graph;\nusing Microsoft.Graph.Models;",
                    RequiredPackage = "Microsoft.Graph",
                    FilePath = filePath,
                    StartLine = line,
                    EndLine = line,
                    EwsQualifiedName = ewsQualifiedName,
                    GraphApiName = roadmap.GraphApiDisplayName
                };
            }
        }

        return null;
    }

    /// <summary>
    /// Generate a conversion using the roadmap's GraphCodeTemplate with placeholder substitution.
    /// </summary>
    private ConversionResult TransformWithTemplate(string code, EwsMigrationRoadmap roadmap, int line, string? filePath)
    {
        var template = roadmap.GraphCodeTemplate!;

        // Extract variable names from the code using simple heuristics
        var placeholders = ExtractPlaceholders(code, roadmap);
        var converted = template;
        foreach (var kv in placeholders)
        {
            converted = converted.Replace($"{{{{{kv.Key}}}}}", kv.Value);
        }

        return new ConversionResult
        {
            Tier = 1,
            Confidence = "high",
            OriginalCode = code,
            ConvertedCode = converted,
            RequiredUsings = roadmap.GraphRequiredUsings ?? "using Microsoft.Graph;\nusing Microsoft.Graph.Models;",
            RequiredPackage = roadmap.GraphRequiredPackage ?? "Microsoft.Graph",
            FilePath = filePath,
            StartLine = line,
            EndLine = line,
            EwsQualifiedName = roadmap.EwsSdkQualifiedName,
            GraphApiName = roadmap.GraphApiDisplayName
        };
    }

    /// <summary>
    /// Extract placeholder values from the EWS code for template substitution.
    /// </summary>
    private static Dictionary<string, string> ExtractPlaceholders(string code, EwsMigrationRoadmap roadmap)
    {
        var placeholders = new Dictionary<string, string>
        {
            ["graphClient"] = "graphClient"
        };

        // Try to extract the variable/object the method is called on
        var dotIndex = code.LastIndexOf('.');
        if (dotIndex > 0)
        {
            var beforeDot = code.Substring(0, dotIndex).Trim();
            // If it's something like "service.FindItems(...)", extract "service"
            var match = Regex.Match(beforeDot, @"(\w+)\s*$");
            if (match.Success)
            {
                placeholders["service"] = match.Groups[1].Value;
            }
        }

        // Try to extract arguments from the invocation
        var argsMatch = Regex.Match(code, @"\((.+)\)\s*;?\s*$", RegexOptions.Singleline);
        if (argsMatch.Success)
        {
            var argsStr = argsMatch.Groups[1].Value;
            var args = SplitArguments(argsStr);
            if (args.Count > 0) placeholders["arg0"] = args[0].Trim();
            if (args.Count > 1) placeholders["arg1"] = args[1].Trim();
            if (args.Count > 2) placeholders["arg2"] = args[2].Trim();

            // Named placeholders based on functional area
            switch (roadmap.FunctionalArea)
            {
                case "Mail":
                    if (args.Count > 0) placeholders["folder"] = args[0].Trim();
                    if (args.Count > 1) placeholders["view"] = args[1].Trim();
                    break;
                case "Calendar":
                    if (args.Count > 0) placeholders["calendarId"] = args[0].Trim();
                    if (args.Count > 1) placeholders["calendarView"] = args[1].Trim();
                    break;
                case "Contacts":
                    if (args.Count > 0) placeholders["contactFolder"] = args[0].Trim();
                    break;
            }
        }

        return placeholders;
    }

    /// <summary>
    /// Split comma-separated arguments respecting parentheses nesting.
    /// </summary>
    private static List<string> SplitArguments(string argsStr)
    {
        var result = new List<string>();
        var depth = 0;
        var current = new StringBuilder();
        foreach (var ch in argsStr)
        {
            if (ch == '(' || ch == '[' || ch == '{') depth++;
            else if (ch == ')' || ch == ']' || ch == '}') depth--;
            else if (ch == ',' && depth == 0)
            {
                result.Add(current.ToString());
                current.Clear();
                continue;
            }
            current.Append(ch);
        }
        if (current.Length > 0) result.Add(current.ToString());
        return result;
    }

    private static string NormalizeKey(string qualifiedName) =>
        qualifiedName.ToLowerInvariant();

    /// <summary>
    /// Build the table of deterministic transforms for common EWS operations.
    /// </summary>
    private Dictionary<string, Func<InvocationContext, string?>> BuildTransformTable()
    {
        var table = new Dictionary<string, Func<InvocationContext, string?>>(StringComparer.OrdinalIgnoreCase);

        // ─── Mail Operations ─────────────────────────────────────────────

        // FindItems → List messages
        table["microsoft.exchange.webservices.data.exchangeservice.finditems"] = ctx =>
        {
            return "var messages = await graphClient.Me.Messages.GetAsync(config =>\n{\n    config.QueryParameters.Top = 50;\n    config.QueryParameters.Orderby = new[] { \"receivedDateTime desc\" };\n});";
        };

        // EmailMessage.Send → Send mail
        table["microsoft.exchange.webservices.data.emailmessage.send"] = ctx =>
        {
            return "await graphClient.Me.SendMail.PostAsync(new Microsoft.Graph.Me.SendMail.SendMailPostRequestBody\n{\n    Message = new Message\n    {\n        Subject = \"Subject\",\n        Body = new ItemBody { ContentType = BodyType.Text, Content = \"Body\" },\n        ToRecipients = new List<Recipient>\n        {\n            new Recipient { EmailAddress = new EmailAddress { Address = \"recipient@example.com\" } }\n        }\n    },\n    SaveToSentItems = true\n});";
        };

        // EmailMessage.Reply → Reply to message
        table["microsoft.exchange.webservices.data.emailmessage.reply"] = ctx =>
        {
            return "await graphClient.Me.Messages[\"{message-id}\"].Reply.PostAsync(\n    new Microsoft.Graph.Me.Messages.Item.Reply.ReplyPostRequestBody\n    {\n        Comment = \"Reply text here\"\n    });";
        };

        // EmailMessage.Bind → Get message
        table["microsoft.exchange.webservices.data.emailmessage.bind"] = ctx =>
        {
            return "var message = await graphClient.Me.Messages[\"{message-id}\"].GetAsync();";
        };

        // EmailMessage.Save → Create draft message
        table["microsoft.exchange.webservices.data.emailmessage.save"] = ctx =>
        {
            return "var draft = await graphClient.Me.Messages.PostAsync(new Message\n{\n    Subject = \"Subject\",\n    Body = new ItemBody { ContentType = BodyType.Text, Content = \"Draft body\" },\n    ToRecipients = new List<Recipient>\n    {\n        new Recipient { EmailAddress = new EmailAddress { Address = \"recipient@example.com\" } }\n    }\n});";
        };

        // EmailMessage.Update → Update message
        table["microsoft.exchange.webservices.data.emailmessage.update"] = ctx =>
        {
            return "var updatedMessage = await graphClient.Me.Messages[\"{message-id}\"].PatchAsync(new Message\n{\n    Subject = \"Updated Subject\"\n});";
        };

        // EmailMessage.Delete → Delete message
        table["microsoft.exchange.webservices.data.emailmessage.delete"] = ctx =>
        {
            return "await graphClient.Me.Messages[\"{message-id}\"].DeleteAsync();";
        };

        // EmailMessage.Move → Move message
        table["microsoft.exchange.webservices.data.emailmessage.move"] = ctx =>
        {
            return "var movedMessage = await graphClient.Me.Messages[\"{message-id}\"].Move.PostAsync(\n    new Microsoft.Graph.Me.Messages.Item.Move.MovePostRequestBody\n    {\n        DestinationId = \"{destination-folder-id}\"\n    });";
        };

        // EmailMessage.Copy → Copy message
        table["microsoft.exchange.webservices.data.emailmessage.copy"] = ctx =>
        {
            return "var copiedMessage = await graphClient.Me.Messages[\"{message-id}\"].Copy.PostAsync(\n    new Microsoft.Graph.Me.Messages.Item.Copy.CopyPostRequestBody\n    {\n        DestinationId = \"{destination-folder-id}\"\n    });";
        };

        // FindFolders → List mail folders
        table["microsoft.exchange.webservices.data.exchangeservice.findfolders"] = ctx =>
        {
            return "var folders = await graphClient.Me.MailFolders.GetAsync();";
        };

        // Folder.Bind → Get mail folder
        table["microsoft.exchange.webservices.data.folder.bind"] = ctx =>
        {
            return "var folder = await graphClient.Me.MailFolders[\"{folder-id}\"].GetAsync();";
        };

        // Folder.Save → Create mail folder
        table["microsoft.exchange.webservices.data.folder.save"] = ctx =>
        {
            return "var newFolder = await graphClient.Me.MailFolders.PostAsync(new MailFolder\n{\n    DisplayName = \"New Folder\"\n});";
        };

        // ─── Calendar Operations ─────────────────────────────────────────

        // FindAppointments → List events
        table["microsoft.exchange.webservices.data.exchangeservice.findappointments"] = ctx =>
        {
            return "var events = await graphClient.Me.Events.GetAsync(config =>\n{\n    config.QueryParameters.Top = 50;\n    config.QueryParameters.Orderby = new[] { \"start/dateTime\" };\n});";
        };

        // Appointment.Save → Create event
        table["microsoft.exchange.webservices.data.appointment.save"] = ctx =>
        {
            return "var newEvent = await graphClient.Me.Events.PostAsync(new Event\n{\n    Subject = \"Meeting\",\n    Start = new DateTimeTimeZone { DateTime = \"2025-01-01T10:00:00\", TimeZone = \"UTC\" },\n    End = new DateTimeTimeZone { DateTime = \"2025-01-01T11:00:00\", TimeZone = \"UTC\" }\n});";
        };

        // Appointment.Update → Update event
        table["microsoft.exchange.webservices.data.appointment.update"] = ctx =>
        {
            return "var updatedEvent = await graphClient.Me.Events[\"{event-id}\"].PatchAsync(new Event\n{\n    Subject = \"Updated Meeting\"\n});";
        };

        // Appointment.Delete → Delete event
        table["microsoft.exchange.webservices.data.appointment.delete"] = ctx =>
        {
            return "await graphClient.Me.Events[\"{event-id}\"].DeleteAsync();";
        };

        // MeetingRequest.Accept → Accept event
        table["microsoft.exchange.webservices.data.meetingrequest.accept"] = ctx =>
        {
            return "await graphClient.Me.Events[\"{event-id}\"].Accept.PostAsync(\n    new Microsoft.Graph.Me.Events.Item.Accept.AcceptPostRequestBody\n    {\n        SendResponse = true\n    });";
        };

        // MeetingRequest.Decline → Decline event
        table["microsoft.exchange.webservices.data.meetingrequest.decline"] = ctx =>
        {
            return "await graphClient.Me.Events[\"{event-id}\"].Decline.PostAsync(\n    new Microsoft.Graph.Me.Events.Item.Decline.DeclinePostRequestBody\n    {\n        SendResponse = true\n    });";
        };

        // ─── Contact Operations ──────────────────────────────────────────

        // Contact.Bind → Get contact
        table["microsoft.exchange.webservices.data.contact.bind"] = ctx =>
        {
            return "var contact = await graphClient.Me.Contacts[\"{contact-id}\"].GetAsync();";
        };

        // Contact.Save → Create contact
        table["microsoft.exchange.webservices.data.contact.save"] = ctx =>
        {
            return "var newContact = await graphClient.Me.Contacts.PostAsync(new Contact\n{\n    GivenName = \"First\",\n    Surname = \"Last\",\n    EmailAddresses = new List<EmailAddress>\n    {\n        new EmailAddress { Address = \"contact@example.com\" }\n    }\n});";
        };

        // Contact.Update → Update contact
        table["microsoft.exchange.webservices.data.contact.update"] = ctx =>
        {
            return "var updatedContact = await graphClient.Me.Contacts[\"{contact-id}\"].PatchAsync(new Contact\n{\n    GivenName = \"UpdatedFirst\"\n});";
        };

        // Contact.Delete → Delete contact
        table["microsoft.exchange.webservices.data.contact.delete"] = ctx =>
        {
            return "await graphClient.Me.Contacts[\"{contact-id}\"].DeleteAsync();";
        };

        // ─── Attachments ─────────────────────────────────────────────────

        // AddFileAttachment (message)
        table["microsoft.exchange.webservices.data.emailmessage.attachments.addfileattachment"] = ctx =>
        {
            return "await graphClient.Me.Messages[\"{message-id}\"].Attachments.PostAsync(new FileAttachment\n{\n    Name = \"file.txt\",\n    ContentBytes = System.IO.File.ReadAllBytes(\"file.txt\")\n});";
        };

        // AddFileAttachment (event)
        table["microsoft.exchange.webservices.data.appointment.attachments.addfileattachment"] = ctx =>
        {
            return "await graphClient.Me.Events[\"{event-id}\"].Attachments.PostAsync(new FileAttachment\n{\n    Name = \"file.txt\",\n    ContentBytes = System.IO.File.ReadAllBytes(\"file.txt\")\n});";
        };

        // Delete attachment
        table["microsoft.exchange.webservices.data.attachment.delete"] = ctx =>
        {
            return "await graphClient.Me.Messages[\"{message-id}\"].Attachments[\"{attachment-id}\"].DeleteAsync();";
        };

        // ─── Sync Operations ─────────────────────────────────────────────

        // SyncFolderItems → Delta messages/events/contacts
        table["microsoft.exchange.webservices.data.exchangeservice.syncfolderitems"] = ctx =>
        {
            return "var deltaMessages = await graphClient.Me.Messages.Delta.GetAsDeltaGetResponseAsync();";
        };

        // ─── Notifications ───────────────────────────────────────────────

        table["microsoft.exchange.webservices.data.exchangeservice.subscribetostreamingnotifications"] = ctx =>
        {
            return "var subscription = await graphClient.Subscriptions.PostAsync(new Subscription\n{\n    ChangeType = \"created,updated\",\n    NotificationUrl = \"https://your-webhook-url.com/api/notifications\",\n    Resource = \"me/messages\",\n    ExpirationDateTime = DateTimeOffset.UtcNow.AddMinutes(4230),\n    ClientState = \"secretClientState\"\n});";
        };

        // ─── Inbox Rules ────────────────────────────────────────────────

        table["microsoft.exchange.webservices.data.exchangeservice.getinboxrules"] = ctx =>
        {
            return "var rules = await graphClient.Me.MailFolders[\"inbox\"].MessageRules.GetAsync();";
        };

        table["microsoft.exchange.webservices.data.exchangeservice.updateinboxrules"] = ctx =>
        {
            return "var newRule = await graphClient.Me.MailFolders[\"inbox\"].MessageRules.PostAsync(new MessageRule\n{\n    DisplayName = \"My Rule\",\n    IsEnabled = true,\n    Sequence = 1\n});";
        };

        // ─── Categories ─────────────────────────────────────────────────

        table["microsoft.exchange.webservices.data.exchangeservice.getuserconfiguration"] = ctx =>
        {
            return "var categories = await graphClient.Me.Outlook.MasterCategories.GetAsync();";
        };

        table["microsoft.exchange.webservices.data.userconfiguration.update"] = ctx =>
        {
            return "var newCategory = await graphClient.Me.Outlook.MasterCategories.PostAsync(new OutlookCategory\n{\n    DisplayName = \"Category Name\",\n    Color = CategoryColor.Preset0\n});";
        };

        // ─── Tasks ──────────────────────────────────────────────────────

        table["microsoft.exchange.webservices.data.task.bind"] = ctx =>
        {
            return "var task = await graphClient.Me.Todo.Lists[\"{list-id}\"].Tasks[\"{task-id}\"].GetAsync();";
        };

        table["microsoft.exchange.webservices.data.task.save"] = ctx =>
        {
            return "var newTask = await graphClient.Me.Todo.Lists[\"{list-id}\"].Tasks.PostAsync(new TodoTask\n{\n    Title = \"Task title\"\n});";
        };

        table["microsoft.exchange.webservices.data.task.update"] = ctx =>
        {
            return "var updatedTask = await graphClient.Me.Todo.Lists[\"{list-id}\"].Tasks[\"{task-id}\"].PatchAsync(new TodoTask\n{\n    Title = \"Updated title\"\n});";
        };

        table["microsoft.exchange.webservices.data.task.delete"] = ctx =>
        {
            return "await graphClient.Me.Todo.Lists[\"{list-id}\"].Tasks[\"{task-id}\"].DeleteAsync();";
        };

        return table;
    }

    /// <summary>
    /// Generate Graph SDK code for converting ExchangeService authentication to GraphServiceClient.
    /// </summary>
    public static ConversionResult? TransformAuth(string code, string authMethod = "clientCredential")
    {
        // Check if the code contains ExchangeService initialization
        if (!code.Contains("ExchangeService") && !code.Contains("WebCredentials"))
            return null;

        string authCode = authMethod switch
        {
            "interactive" =>
                "var credential = new InteractiveBrowserCredential();\nvar graphClient = new GraphServiceClient(credential);",
            "deviceCode" =>
                "var credential = new DeviceCodeCredential();\nvar graphClient = new GraphServiceClient(credential);",
            "managedIdentity" =>
                "var credential = new ManagedIdentityCredential();\nvar graphClient = new GraphServiceClient(credential);",
            _ => // clientCredential
                "var credential = new ClientSecretCredential(\n    \"{tenant-id}\",\n    \"{client-id}\",\n    \"{client-secret}\");\nvar graphClient = new GraphServiceClient(credential);"
        };

        return new ConversionResult
        {
            Tier = 1,
            Confidence = "high",
            OriginalCode = code,
            ConvertedCode = authCode,
            RequiredUsings = "using Microsoft.Graph;\nusing Azure.Identity;",
            RequiredPackage = "Microsoft.Graph, Azure.Identity",
            EwsQualifiedName = "Microsoft.Exchange.WebServices.Data.ExchangeService",
            GraphApiName = "GraphServiceClient"
        };
    }
}

/// <summary>
/// Context passed to a deterministic transform function.
/// </summary>
internal sealed class InvocationContext
{
    public string Code { get; set; } = string.Empty;
    public string QualifiedName { get; set; } = string.Empty;
    public EwsMigrationRoadmap Roadmap { get; set; } = null!;
    public int Line { get; set; }
    public string? FilePath { get; set; }
}
