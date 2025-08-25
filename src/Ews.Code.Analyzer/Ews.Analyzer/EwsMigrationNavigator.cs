using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

/*

/generate
Initialize Map with 5 entries
Add a function to retrieve the map for a given EWS operation
Add a function to retrieve the map for a given EWS SDK operation

*/

namespace Ews.Analyzer
{
    public class EwsMigrationNavigator
    {
        public List<EwsMigrationRoadmap> Map;

        public EwsMigrationNavigator()
        {
            // Initialize Map with sample entries for testing and demonstration
            Map = new List<EwsMigrationRoadmap>
            {
                new EwsMigrationRoadmap
                {
                    Title = "ConvertId",
                    EwsSoapOperation = "ConvertId",
                    FunctionalArea = "Id Conversion",
                    EwsSdkMethodName = "ConvertId",
                    EwsSdkQualifiedName = "Microsoft.Exchange.WebServices.Data.ExchangeService.ConvertId",
                    EWSDocumentationUrl = "https://docs.microsoft.com/en-us/exchange/client-developer/web-service-reference/convertid-operation",
                    GraphApiDocumentationUrl = "https://docs.microsoft.com/en-us/graph/api/resources/directoryobject",
                    GraphApiEta = "Available",
                    GraphApiGapFillPlan = "Use Graph API equivalents",
                    GraphApiHasParity = true,
                    CopilotPromptTemplate = "Convert EWS ConvertId to Graph API equivalent",
                    GraphApiDisplayName = "Graph Directory Object",
                    GraphApiHttpRequest = "GET /directoryObjects/{id}",
                    GraphApiStatus = "Available"
                },
                new EwsMigrationRoadmap
                {
                    Title = "ResolveNames",
                    EwsSoapOperation = "ResolveNames",
                    FunctionalArea = "Name Resolution",
                    EwsSdkMethodName = "ResolveNames",
                    EwsSdkQualifiedName = "Microsoft.Exchange.WebServices.Data.ExchangeService.ResolveNames",
                    EWSDocumentationUrl = "https://docs.microsoft.com/en-us/exchange/client-developer/web-service-reference/resolvenames-operation",
                    GraphApiDocumentationUrl = "https://docs.microsoft.com/en-us/graph/api/user-list",
                    GraphApiEta = "Available",
                    GraphApiGapFillPlan = "Use Graph API user search",
                    GraphApiHasParity = true,
                    CopilotPromptTemplate = "Convert EWS ResolveNames to Graph API user search",
                    GraphApiDisplayName = "Graph User Search",
                    GraphApiHttpRequest = "GET /users?$search=\"displayName:{searchText}\"",
                    GraphApiStatus = "Available"
                },
                new EwsMigrationRoadmap
                {
                    Title = "FindItem",
                    EwsSoapOperation = "FindItem",
                    FunctionalArea = "Item Management",
                    EwsSdkMethodName = "FindItems",
                    EwsSdkQualifiedName = "Microsoft.Exchange.WebServices.Data.Folder.FindItems",
                    EWSDocumentationUrl = "https://docs.microsoft.com/en-us/exchange/client-developer/web-service-reference/finditem-operation",
                    GraphApiDocumentationUrl = "https://docs.microsoft.com/en-us/graph/api/user-list-messages",
                    GraphApiEta = "Available",
                    GraphApiGapFillPlan = "Use Graph API messages endpoint",
                    GraphApiHasParity = true,
                    CopilotPromptTemplate = "Convert EWS FindItems to Graph API messages",
                    GraphApiDisplayName = "Graph Messages",
                    GraphApiHttpRequest = "GET /me/messages",
                    GraphApiStatus = "Available"
                },
                new EwsMigrationRoadmap
                {
                    Title = "CreateItem",
                    EwsSoapOperation = "CreateItem",
                    FunctionalArea = "Item Management",
                    EwsSdkMethodName = "Save",
                    EwsSdkQualifiedName = "Microsoft.Exchange.WebServices.Data.Item.Save",
                    EWSDocumentationUrl = "https://docs.microsoft.com/en-us/exchange/client-developer/web-service-reference/createitem-operation",
                    GraphApiDocumentationUrl = "https://docs.microsoft.com/en-us/graph/api/user-post-messages",
                    GraphApiEta = "Available",
                    GraphApiGapFillPlan = "Use Graph API create message",
                    GraphApiHasParity = true,
                    CopilotPromptTemplate = "Convert EWS Save to Graph API create message",
                    GraphApiDisplayName = "Graph Create Message",
                    GraphApiHttpRequest = "POST /me/messages",
                    GraphApiStatus = "Available"
                },
                new EwsMigrationRoadmap
                {
                    Title = "GetItem",
                    EwsSoapOperation = "GetItem",
                    FunctionalArea = "Item Management",
                    EwsSdkMethodName = "Load",
                    EwsSdkQualifiedName = "Microsoft.Exchange.WebServices.Data.Item.Load",
                    EWSDocumentationUrl = "https://docs.microsoft.com/en-us/exchange/client-developer/web-service-reference/getitem-operation",
                    GraphApiDocumentationUrl = "https://docs.microsoft.com/en-us/graph/api/message-get",
                    GraphApiEta = "Available",
                    GraphApiGapFillPlan = "Use Graph API get message",
                    GraphApiHasParity = false,
                    CopilotPromptTemplate = "Convert EWS Load to Graph API get message",
                    GraphApiDisplayName = "Graph Get Message",
                    GraphApiHttpRequest = "GET /me/messages/{id}",
                    GraphApiStatus = "Available"
                }
            };
        }

        private static EwsMigrationRoadmap defaultRoadmap = new EwsMigrationRoadmap
        {
            Title = "Default",
            EwsSoapOperation = "Default",
            FunctionalArea = "Default",
            EwsSdkMethodName = "Default",
            EwsSdkQualifiedName = "Default",
            EWSDocumentationUrl = "https://aka.ms/ews1pageGH",
            GraphApiDocumentationUrl = "https://aka.ms/ewsMapGH",
            GraphApiEta= "TBD",
            GraphApiGapFillPlan = "TBD",
            GraphApiHasParity = false,
            CopilotPromptTemplate = null,
            GraphApiDisplayName = null,
            GraphApiHttpRequest = null,
        };

        // Function to retrieve the map for a given EWS operation
        public EwsMigrationRoadmap GetMapByEwsOperation(string operation)
        {
            var result = Map.FirstOrDefault(m => m.EwsSoapOperation.Equals(operation, StringComparison.OrdinalIgnoreCase));
            return result; // Return null if not found
        }

        // Function to retrieve the map for a given EWS SDK operation
        public EwsMigrationRoadmap GetMapByEwsSdkOperation(string operation)
        {
            var result = Map.FirstOrDefault(m => m.EwsSdkMethodName.Equals(operation, StringComparison.OrdinalIgnoreCase));
            return result; // Return null if not found
        }

        // Function to retrieve the map for a given EWS SDK qualified name
        public EwsMigrationRoadmap GetMapByEwsSdkQualifiedName(string operation)
        {
            var result = Map.FirstOrDefault(m => m.EwsSdkQualifiedName.Equals(operation, StringComparison.OrdinalIgnoreCase));
            return result; // Return null if not found
        }

    }
}

