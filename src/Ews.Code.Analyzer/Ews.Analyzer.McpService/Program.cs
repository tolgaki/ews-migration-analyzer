using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Ews.Analyzer;

class Program
{
    static async Task Main()
    {
        string? line;
        while ((line = Console.ReadLine()) != null)
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            var idElement = root.GetProperty("id");
            var method = root.GetProperty("method").GetString();
            object result;

            switch (method)
            {
                case "initialize":
                    result = new { capabilities = new { } };
                    break;
                case "tools/list":
                    result = new
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
                    break;
                case "tools/call":
                    result = await HandleToolCallAsync(root.GetProperty("params"));
                    break;
                case "shutdown":
                    result = null;
                    break;
                default:
                    result = new
                    {
                        error = $"Unknown method {method}"
                    };
                    break;
            }

            object id = idElement.ValueKind switch
            {
                JsonValueKind.Number => idElement.GetInt32(),
                JsonValueKind.String => idElement.GetString()!,
                _ => null!
            };

            var response = new
            {
                jsonrpc = "2.0",
                id,
                result
            };

            Console.WriteLine(JsonSerializer.Serialize(response));

            if (method == "shutdown")
            {
                break;
            }
        }
    }

    private static async Task<object> HandleToolCallAsync(JsonElement element)
    {
        var name = element.GetProperty("name").GetString();
        var args = element.GetProperty("arguments");

        switch (name)
        {
            case "analyzeCode":
                var code = args.GetProperty("code").GetString() ?? string.Empty;
                var diagnostics = await AnalyzeAsync(code);
                return new
                {
                    content = new object[]
                    {
                        new
                        {
                            type = "text",
                            text = JsonSerializer.Serialize(diagnostics)
                        }
                    }
                };
            case "getRoadmap":
                var operation = args.GetProperty("operation").GetString() ?? string.Empty;
                var roadmap = GetRoadmap(operation);
                return new
                {
                    content = new object[]
                    {
                        new
                        {
                            type = "text",
                            text = JsonSerializer.Serialize(roadmap)
                        }
                    }
                };
            default:
                return new
                {
                    content = new object[]
                    {
                        new
                        {
                            type = "text",
                            text = $"Unknown tool {name}"
                        }
                    }
                };
        }
    }

    private static async Task<IEnumerable<object>> AnalyzeAsync(string code)
    {
        var tree = CSharpSyntaxTree.ParseText(code);
        var compilation = CSharpCompilation.Create(
            "Analysis",
            new[] { tree },
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var analyzer = new EwsAnalyzer();
        var compilationWithAnalyzers = new CompilationWithAnalyzers(
            compilation,
            ImmutableArray.Create<DiagnosticAnalyzer>(analyzer));

        var diagnostics = await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
        var list = new List<object>();
        foreach (var d in diagnostics)
        {
            list.Add(new
            {
                id = d.Id,
                message = d.GetMessage(),
                severity = d.Severity.ToString(),
                location = d.Location.GetLineSpan().StartLinePosition.Line + 1
            });
        }
        return list;
    }

    private static EwsMigrationRoadmap GetRoadmap(string operation)
    {
        var navigator = new EwsMigrationNavigator();
        return navigator.GetMapByEwsOperation(operation);
    }
}
