using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Ews.Analyzer
{
    /// <summary>
    /// Provides a read-only repository over tracking-list-baseline-full.json that maps EWS SOAP operations to their Microsoft Graph API equivalents.
    /// </summary>
    public class EwsMappingRepository
    {
        private readonly Dictionary<string, List<EwsToGraphMapping>> _mappings;

        /// <summary>
        /// Represents a mapping between an EWS SOAP operation and its Graph API equivalent
        /// </summary>
        public class EwsToGraphMapping
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
            /// The URL to EWS documentation
            /// </summary>
            public string EWSDocumentationUrl { get; set; }

            /// <summary>
            /// Flag indicating if the EWS link is dead
            /// </summary>
            public bool HasDeadEwsLink { get; set; }

            /// <summary>
            /// The URL to Graph API documentation
            /// </summary>
            public string GraphDocumentationUrl { get; set; }

            /// <summary>
            /// Flag indicating if the Graph link is dead
            /// </summary>
            public bool HasDeadGraphLink { get; set; }

            /// <summary>
            /// The display name of the Graph API
            /// </summary>
            public string GraphDisplayName { get; set; }

            /// <summary>
            /// Flag indicating if the operation is in the EWS WSDL
            /// </summary>
            public bool IsInEwsWsdl { get; set; }

            /// <summary>
            /// Flag indicating if the operation is in use
            /// </summary>
            public bool IsInUse { get; set; }

            /// <summary>
            /// Flag indicating if there is a gap in the mapping
            /// </summary>
            public bool HasGap { get; set; }

            /// <summary>
            /// The status of the Graph API equivalent
            /// </summary>
            public string GraphApiStatus { get; set; }

            /// <summary>
            /// The confidence level of the mapping (0-100)
            /// </summary>
            public int MappingConfidence { get; set; }
        }

        /// <summary>
        /// Initializes a new instance of the EwsMappingRepository class.
        /// </summary>
        /// <param name="jsonFilePath">Optional path to the tracking-list-baseline-full.json file.
        /// If not provided, will look in the executing assembly's directory.</param>
        public EwsMappingRepository(string jsonFilePath = null)
        {
            string filePath = jsonFilePath ?? Path.Combine(
                Path.GetDirectoryName(typeof(EwsMappingRepository).Assembly.Location),
                "tracking-list-baseline-full.json");

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"The EWS to Graph API mapping file was not found at: {filePath}");
            }

            string jsonContent = File.ReadAllText(filePath);
            _mappings = LoadMappingsFromJson(jsonContent);
        }

        /// <summary>
        /// Initializes a new instance of the EwsMappingRepository class from a JSON string.
        /// </summary>
        /// <param name="jsonContent">The JSON content as a string</param>
        public EwsMappingRepository(Stream jsonStream)
        {
            using (var reader = new StreamReader(jsonStream))
            {
                string jsonContent = reader.ReadToEnd();
                _mappings = LoadMappingsFromJson(jsonContent);
            }
        }

        /// <summary>
        /// Loads mappings from JSON content into a dictionary.
        /// </summary>
        /// <param name="jsonContent">The JSON content to parse</param>
        /// <returns>A dictionary mapping EWS operation names to their Graph API mappings</returns>
        private Dictionary<string, List<EwsToGraphMapping>> LoadMappingsFromJson(string jsonContent)
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var mappings = JsonSerializer.Deserialize<List<EwsToGraphMapping>>(jsonContent, options);

                // Group by EwsSoapOperation since one EWS operation might have multiple Graph equivalents
                return mappings.GroupBy(m => m.EwsSoapOperation)
                               .ToDictionary(g => g.Key, g => g.ToList());
            }
            catch (JsonException ex)
            {
                throw new FormatException($"Failed to parse the EWS to Graph API mapping JSON: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Finds the Graph API mapping for a given EWS SOAP operation name.
        /// </summary>
        /// <param name="ewsSoapOperationName">The name of the EWS SOAP operation</param>
        /// <returns>A list of corresponding Graph API mappings if found; otherwise empty list</returns>
        public List<EwsToGraphMapping> FindGraphApiMapping(string ewsSoapOperationName)
        {
            if (string.IsNullOrEmpty(ewsSoapOperationName))
            {
                throw new ArgumentNullException(nameof(ewsSoapOperationName));
            }

            return _mappings.TryGetValue(ewsSoapOperationName, out var mappings)
                ? mappings
                : new List<EwsToGraphMapping>();
        }

        /// <summary>
        /// Finds the best Graph API mapping for a given EWS SOAP operation name based on confidence level.
        /// </summary>
        /// <param name="ewsSoapOperationName">The name of the EWS SOAP operation</param>
        /// <returns>The highest confidence mapping if found; otherwise null</returns>
        public EwsToGraphMapping FindBestGraphApiMapping(string ewsSoapOperationName)
        {
            if (string.IsNullOrEmpty(ewsSoapOperationName))
            {
                throw new ArgumentNullException(nameof(ewsSoapOperationName));
            }

            if (_mappings.TryGetValue(ewsSoapOperationName, out var mappings) && mappings.Any())
            {
                // Return the mapping with highest confidence that doesn't have a gap
                var bestMapping = mappings.Where(m => !m.HasGap)
                                         .OrderByDescending(m => m.MappingConfidence)
                                         .FirstOrDefault();

                // If all have gaps, return the highest confidence one anyway
                return bestMapping ?? mappings.OrderByDescending(m => m.MappingConfidence).First();
            }

            return null;
        }

        /// <summary>
        /// Gets all available EWS SOAP operation names in the repository.
        /// </summary>
        /// <returns>A collection of all EWS SOAP operation names</returns>
        public IEnumerable<string> GetAllEwsOperationNames()
        {
            return _mappings.Keys;
        }

        /// <summary>
        /// Checks whether an EWS SOAP operation has a Graph API mapping.
        /// </summary>
        /// <param name="ewsSoapOperationName">The name of the EWS SOAP operation</param>
        /// <returns>True if a mapping exists; otherwise false</returns>
        public bool HasGraphApiMapping(string ewsSoapOperationName)
        {
            return !string.IsNullOrEmpty(ewsSoapOperationName) &&
                   _mappings.TryGetValue(ewsSoapOperationName, out var mappings) &&
                   mappings.Any(m => !m.HasGap);
        }

        /// <summary>
        /// Gets all mappings grouped by functional area
        /// </summary>
        /// <returns>A dictionary with functional areas as keys and lists of mappings as values</returns>
        public Dictionary<string, List<EwsToGraphMapping>> GetMappingsByFunctionalArea()
        {
            return _mappings.Values
                .SelectMany(m => m)
                .GroupBy(m => m.FunctionalArea)
                .ToDictionary(g => g.Key, g => g.ToList());
        }
    }
}
