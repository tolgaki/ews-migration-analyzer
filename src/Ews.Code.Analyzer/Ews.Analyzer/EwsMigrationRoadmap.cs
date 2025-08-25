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
    public string Title { get; set; }

        /// <summary>
        /// The EWS SOAP operation name
        /// </summary>
    public string EwsSoapOperation { get; set; }

        /// <summary>
        /// The functional area of the operation
        /// </summary>
    public string FunctionalArea { get; set; }

        /// <summary>
        /// Method name in EWS SDK
        /// </summary>
    public string EwsSdkMethodName { get; set; }

        /// <summary>
        /// Fully qualified Name in EWS SDK
        /// </summary>
    public string EwsSdkQualifiedName { get; set; }

        /// <summary>
        /// The URL to EWS documentation
        /// </summary>
    public string EWSDocumentationUrl { get; set; }

        /// <summary>
        /// The URL to Graph API documentation
        /// </summary>
    public string GraphApiDocumentationUrl { get; set; }

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
    public string GraphApiStatus { get; set; }

        /// <summary>
        /// Graph Api ETA for release
        /// </summary>
    public string GraphApiEta { get; set; }

        /// <summary>
        /// Flag indicating whether an equivalent Graph API is available
        /// </summary>
    public bool GraphApiHasParity { get; set; }

        /// <summary>
        /// Plan to fill the gap in Graph API
        /// </summary>
    public string GraphApiGapFillPlan { get; set; }

    }
}