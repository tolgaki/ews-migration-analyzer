namespace Ews.Analyzer
{
    /// <summary>
    /// Represents a mapping between an EWS SOAP operation and its Graph API equivalent
    /// </summary>
    public class EwsMigrationRoadmap
    {
        /// <summary>
        /// The title of the mapping entry
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// The EWS SOAP operation name
        /// </summary>
        public string EwsSoapOperation { get; set; } = string.Empty;

        /// <summary>
        /// The functional area of the operation
        /// </summary>
        public string FunctionalArea { get; set; } = string.Empty;

        /// <summary>
        /// Method name in EWS SDK
        /// </summary>
        public string EwsSdkMethodName { get; set; } = string.Empty;

        /// <summary>
        /// Fully qualified Name in EWS SDK
        /// </summary>
        public string EwsSdkQualifiedName { get; set; } = string.Empty;

        /// <summary>
        /// The URL to EWS documentation
        /// </summary>
        public string EWSDocumentationUrl { get; set; } = string.Empty;

        /// <summary>
        /// The URL to Graph API documentation
        /// </summary>
        public string GraphApiDocumentationUrl { get; set; } = string.Empty;

        /// <summary>
        /// Prompt template for GitHub Copilot to generate equivalent Graph API code
        /// </summary>
        public string? CopilotPromptTemplate { get; set; }

        /// <summary>
        /// The display name of the Graph API
        /// </summary>
        public string? GraphApiDisplayName { get; set; }

        /// <summary>
        /// Graph API HTTP request syntax
        /// </summary>
        public string? GraphApiHttpRequest { get; set; }

        /// <summary>
        /// Graph API development status
        /// </summary>
        public string GraphApiStatus { get; set; } = string.Empty;

        /// <summary>
        /// Graph Api ETA for release
        /// </summary>
        public string GraphApiEta { get; set; } = string.Empty;

        /// <summary>
        /// Flag indicating whether an equivalent Graph API is available
        /// </summary>
        public bool GraphApiHasParity { get; set; }

        /// <summary>
        /// Plan to fill the gap in Graph API
        /// </summary>
        public string GraphApiGapFillPlan { get; set; } = string.Empty;

        /// <summary>
        /// The conversion tier: 1 = deterministic Roslyn transform, 2 = template-guided LLM, 3 = full-context LLM.
        /// </summary>
        public int ConversionTier { get; set; } = 2;

        /// <summary>
        /// Graph SDK C# code template for Tier 1 deterministic replacement.
        /// Uses placeholders like {{variable}}, {{folder}}, {{top}} extracted from the matched EWS call.
        /// Null means this operation is not eligible for Tier 1.
        /// </summary>
        public string? GraphCodeTemplate { get; set; }

        /// <summary>
        /// Required using statements for the Graph SDK replacement code.
        /// </summary>
        public string? GraphRequiredUsings { get; set; }

        /// <summary>
        /// Required NuGet package for the Graph SDK call (e.g., "Microsoft.Graph").
        /// </summary>
        public string? GraphRequiredPackage { get; set; }
    }
}