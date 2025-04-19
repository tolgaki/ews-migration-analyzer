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
            // Initialize Map with 5 entries
            Map = new List<EwsMigrationRoadmap>
            {
                // TODO Generate the entries
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

            if (result != null)
                return result;

            return defaultRoadmap;
        }

        // Function to retrieve the map for a given EWS SDK operation
        public EwsMigrationRoadmap GetMapByEwsSdkOperation(string operation)
        {
            var result = Map.FirstOrDefault(m => m.EwsSdkMethodName.Equals(operation, StringComparison.OrdinalIgnoreCase));

            if (result != null)
                return result;

            return defaultRoadmap;
        }

        // Function to retrieve the map for a given EWS SDK qualified name
        public EwsMigrationRoadmap GetMapByEwsSdkQualifiedName(string operation)
        {
            var result = Map.FirstOrDefault(m => m.EwsSdkQualifiedName.Equals(operation, StringComparison.OrdinalIgnoreCase));
            if (result != null)
                return result;

            return defaultRoadmap;
        }

    }
}

