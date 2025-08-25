using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Ews.Analyzer;

namespace Ews.Analyzer.McpService
{
    /// <summary>
    /// JSON-RPC 2.0 compliant MCP service for the EWS analyzer
    /// </summary>
    class Program
    {
        static async Task Main()
        {
            try
            {
                string? line;
                while ((line = Console.ReadLine()) != null)
                {
                    await ProcessRequestAsync(line);
                }
            }
            catch (Exception ex)
            {
                // Log critical errors to stderr to avoid interfering with JSON-RPC protocol
                await Console.Error.WriteLineAsync($"Critical error in MCP service: {ex.Message}");
                Environment.Exit(1);
            }
        }

        private static async Task ProcessRequestAsync(string jsonLine)
        {
            JsonRpcResponse response;
            object? requestId = null;
            bool isNotification = false;

            try
            {
                if (string.IsNullOrWhiteSpace(jsonLine))
                {
                    // Skip empty lines - this is normal for newline-delimited JSON
                    return;
                }

                using var doc = JsonDocument.Parse(jsonLine);
                var root = doc.RootElement;
                
                // Check if this is a notification (no id field)
                if (!root.TryGetProperty("id", out var idElement))
                {
                    isNotification = true;
                }
                else
                {
                    requestId = ParseRequestId(idElement);
                }

                // Validate JSON-RPC version
                if (!root.TryGetProperty("jsonrpc", out var versionElement) || 
                    versionElement.GetString() != "2.0")
                {
                    response = CreateErrorResponse(requestId, -32600, "Invalid Request", "Missing or invalid 'jsonrpc' version");
                    await WriteResponseAsync(response);
                    return;
                }

                if (!root.TryGetProperty("method", out var methodElement))
                {
                    response = CreateErrorResponse(requestId, -32600, "Invalid Request", "Missing 'method' field");
                    await WriteResponseAsync(response);
                    return;
                }

                var method = methodElement.GetString();
                if (string.IsNullOrEmpty(method))
                {
                    response = CreateErrorResponse(requestId, -32600, "Invalid Request", "Invalid 'method' field");
                    await WriteResponseAsync(response);
                    return;
                }

                object? result = await HandleMethodAsync(method, root);
                
                // Don't send response for notifications
                if (!isNotification)
                {
                    response = CreateSuccessResponse(requestId, result);
                    await WriteResponseAsync(response);
                }

                // Handle shutdown for both requests and notifications
                if (method == "shutdown")
                {
                    Environment.Exit(0);
                }
            }
            catch (JsonException ex)
            {
                response = CreateErrorResponse(requestId, -32700, "Parse error", ex.Message);
                await WriteResponseAsync(response);
            }
            catch (Exception ex)
            {
                response = CreateErrorResponse(requestId, -32603, "Internal error", ex.Message);
                await WriteResponseAsync(response);
            }
        }

        private static object? ParseRequestId(JsonElement idElement)
        {
            return idElement.ValueKind switch
            {
                JsonValueKind.Number => idElement.TryGetInt32(out var intValue) ? intValue : idElement.GetDouble(),
                JsonValueKind.String => idElement.GetString(),
                JsonValueKind.Null => null,
                _ => throw new JsonException("Invalid id type")
            };
        }

        private static async Task<object?> HandleMethodAsync(string method, JsonElement root)
        {
            return method switch
            {
                "initialize" => new { capabilities = new { } },
                "tools/list" => GetToolsList(),
                "tools/call" => await HandleToolCallAsync(root.GetProperty("params")),
                "shutdown" => null,
                _ => throw new InvalidOperationException($"Unknown method: {method}")
            };
        }

        private static object GetToolsList()
        {
            return new
            {
                tools = new object[]
                {
                    new
                    {
                        name = "analyzeCode",
                        description = "Run EWS analyzer on provided C# source code",
                        inputSchema = new
                        {
                            type = "object",
                            properties = new
                            {
                                code = new
                                {
                                    type = "string",
                                    description = "C# source code to analyze"
                                }
                            },
                            required = new[] { "code" }
                        }
                    },
                    new
                    {
                        name = "getRoadmap",
                        description = "Return migration roadmap for an EWS operation",
                        inputSchema = new
                        {
                            type = "object",
                            properties = new
                            {
                                operation = new
                                {
                                    type = "string",
                                    description = "EWS operation name"
                                }
                            },
                            required = new[] { "operation" }
                        }
                    }
                }
            };
        }

        private static async Task<object> HandleToolCallAsync(JsonElement element)
        {
            if (!element.TryGetProperty("name", out var nameElement))
            {
                throw new ArgumentException("Missing 'name' field in tool call");
            }

            var name = nameElement.GetString();
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("Invalid 'name' field in tool call");
            }

            if (!element.TryGetProperty("arguments", out var args))
            {
                throw new ArgumentException("Missing 'arguments' field in tool call");
            }

            return name switch
            {
                "analyzeCode" => await HandleAnalyzeCodeAsync(args),
                "getRoadmap" => HandleGetRoadmapAsync(args),
                _ => throw new ArgumentException($"Unknown tool: {name}")
            };
        }

        private static async Task<object> HandleAnalyzeCodeAsync(JsonElement args)
        {
            if (!args.TryGetProperty("code", out var codeElement))
            {
                throw new ArgumentException("Missing 'code' argument");
            }

            var code = codeElement.GetString();
            if (string.IsNullOrEmpty(code))
            {
                throw new ArgumentException("Code argument cannot be null or empty");
            }

            var diagnostics = await AnalyzeAsync(code);
            return new
            {
                content = new object[]
                {
                    new
                    {
                        type = "text",
                        text = "Analysis completed",
                        diagnostics = diagnostics
                    }
                }
            };
        }

        private static object HandleGetRoadmapAsync(JsonElement args)
        {
            if (!args.TryGetProperty("operation", out var operationElement))
            {
                throw new ArgumentException("Missing 'operation' argument");
            }

            var operation = operationElement.GetString();
            if (string.IsNullOrEmpty(operation))
            {
                throw new ArgumentException("Operation argument cannot be null or empty");
            }

            var roadmap = GetRoadmap(operation);
            return new
            {
                content = new object[]
                {
                    new
                    {
                        type = "text",
                        text = "Roadmap retrieved",
                        roadmap = roadmap
                    }
                }
            };
        }

        private static async Task<IEnumerable<object>> AnalyzeAsync(string code)
        {
            try
            {
                var tree = CSharpSyntaxTree.ParseText(code);
                
                // Expand metadata references for more realistic analysis accuracy
                var references = GetExpandedMetadataReferences();
                
                var compilation = CSharpCompilation.Create(
                    "Analysis",
                    new[] { tree },
                    references,
                    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

                var analyzer = new EwsAnalyzer();
                var options = new CompilationWithAnalyzersOptions(
                    new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty),
                    onAnalyzerException: null,
                    concurrentAnalysis: true,
                    logAnalyzerExecutionTime: false);
                var compilationWithAnalyzers = compilation.WithAnalyzers(
                    ImmutableArray.Create<DiagnosticAnalyzer>(analyzer),
                    options);

                var diagnostics = await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
                
                // Return structured diagnostic objects instead of JSON stringifying them
                return diagnostics.Select(d => new
                {
                    id = d.Id,
                    message = d.GetMessage(),
                    severity = d.Severity.ToString(),
                    location = new
                    {
                        line = d.Location.GetLineSpan().StartLinePosition.Line + 1,
                        column = d.Location.GetLineSpan().StartLinePosition.Character + 1,
                        endLine = d.Location.GetLineSpan().EndLinePosition.Line + 1,
                        endColumn = d.Location.GetLineSpan().EndLinePosition.Character + 1
                    },
                    descriptor = new
                    {
                        id = d.Descriptor.Id,
                        title = d.Descriptor.Title.ToString(),
                        description = d.Descriptor.Description.ToString(),
                        category = d.Descriptor.Category,
                        helpLinkUri = d.Descriptor.HelpLinkUri
                    }
                });
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Analysis failed: {ex.Message}", ex);
            }
        }

        private static IEnumerable<MetadataReference> GetExpandedMetadataReferences()
        {
            var references = new List<MetadataReference>();
            
            // Add basic runtime references
            references.Add(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
            references.Add(MetadataReference.CreateFromFile(typeof(Console).Assembly.Location));
            references.Add(MetadataReference.CreateFromFile(typeof(IEnumerable<>).Assembly.Location));
            references.Add(MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location));
            
            try
            {
                // Add System.Runtime if available
                var runtimeAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "System.Runtime");
                if (runtimeAssembly != null)
                {
                    references.Add(MetadataReference.CreateFromFile(runtimeAssembly.Location));
                }
                
                // Add System.Collections if available  
                var collectionsAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "System.Collections");
                if (collectionsAssembly != null)
                {
                    references.Add(MetadataReference.CreateFromFile(collectionsAssembly.Location));
                }
            }
            catch
            {
                // If we can't load additional references, continue with basic ones
            }
            
            return references;
        }

        private static EwsMigrationRoadmap GetRoadmap(string operation)
        {
            try
            {
                var navigator = new EwsMigrationNavigator();
                return navigator.GetMapByEwsOperation(operation);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to get roadmap for operation '{operation}': {ex.Message}", ex);
            }
        }

        private static JsonRpcResponse CreateSuccessResponse(object? id, object? result)
        {
            return new JsonRpcResponse
            {
                JsonRpc = "2.0",
                Id = id,
                Result = result
            };
        }

        private static JsonRpcResponse CreateErrorResponse(object? id, int code, string message, string? data = null)
        {
            return new JsonRpcResponse
            {
                JsonRpc = "2.0",
                Id = id,
                Error = new JsonRpcError
                {
                    Code = code,
                    Message = message,
                    Data = data
                }
            };
        }

        private static async Task WriteResponseAsync(JsonRpcResponse response)
        {
            var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });
            await Console.Out.WriteLineAsync(json);
        }
    }

    public class JsonRpcResponse
    {
        public string JsonRpc { get; set; } = "2.0";
        public object? Id { get; set; }
        public object? Result { get; set; }
        public JsonRpcError? Error { get; set; }
    }

    public class JsonRpcError
    {
        public int Code { get; set; }
        public string Message { get; set; } = string.Empty;
        public object? Data { get; set; }
    }
}
