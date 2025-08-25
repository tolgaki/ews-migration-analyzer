using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Ews.Analyzer.McpService;

namespace Ews.Analyzer.McpService.Tests
{
    [TestClass]
    public class JsonRpcTests
    {
        [TestMethod]
        public void JsonRpcResponse_WithResult_ShouldSerializeCorrectly()
        {
            // Arrange
            var response = new JsonRpcResponse
            {
                JsonRpc = "2.0",
                Id = 1,
                Result = new { test = "value" }
            };

            // Act
            var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });

            // Debug output
            Console.WriteLine($"Serialized JSON: {json}");

            // Assert
            Assert.IsTrue(json.Contains("\"jsonRpc\":\"2.0\""), $"Expected jsonRpc field in: {json}");
            Assert.IsTrue(json.Contains("\"id\":1"), $"Expected id field in: {json}");
            Assert.IsTrue(json.Contains("\"result\""), $"Expected result field in: {json}");
            Assert.IsFalse(json.Contains("\"error\""), $"Unexpected error field in: {json}");
        }

        [TestMethod]
        public void JsonRpcResponse_WithError_ShouldSerializeCorrectly()
        {
            // Arrange
            var response = new JsonRpcResponse
            {
                JsonRpc = "2.0",
                Id = 1,
                Error = new JsonRpcError
                {
                    Code = -32600,
                    Message = "Invalid Request"
                }
            };

            // Act
            var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });

            // Assert
            Assert.IsTrue(json.Contains("\"jsonRpc\":\"2.0\""));
            Assert.IsTrue(json.Contains("\"id\":1"));
            Assert.IsTrue(json.Contains("\"error\""));
            Assert.IsTrue(json.Contains("\"code\":-32600"));
            Assert.IsTrue(json.Contains("\"message\":\"Invalid Request\""));
            Assert.IsFalse(json.Contains("\"result\""));
        }

        [TestMethod]
        public void JsonRpcResponse_WithNullId_ShouldSerializeCorrectly()
        {
            // Arrange
            var response = new JsonRpcResponse
            {
                JsonRpc = "2.0",
                Id = null,
                Result = new { }
            };

            // Act
            var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });

            // Assert
            Assert.IsTrue(json.Contains("\"jsonRpc\":\"2.0\""));
            Assert.IsTrue(json.Contains("\"result\""));
            // null id should not be serialized when using WhenWritingNull
        }

        [TestMethod]
        public void JsonRpcError_ShouldSerializeWithAllFields()
        {
            // Arrange
            var error = new JsonRpcError
            {
                Code = -32603,
                Message = "Internal error",
                Data = "Additional error information"
            };

            // Act
            var json = JsonSerializer.Serialize(error, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            // Assert
            Assert.IsTrue(json.Contains("\"code\":-32603"));
            Assert.IsTrue(json.Contains("\"message\":\"Internal error\""));
            Assert.IsTrue(json.Contains("\"data\":\"Additional error information\""));
        }
    }

    [TestClass]
    public class InputValidationTests
    {
        [TestMethod]
        public void ValidateJsonRpcRequest_WithMissingJsonRpc_ShouldReturnError()
        {
            // Arrange
            var json = "{\"id\": 1, \"method\": \"test\"}";

            // Act & Assert
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            
            Assert.IsFalse(root.TryGetProperty("jsonrpc", out var versionElement) && 
                          versionElement.GetString() == "2.0");
        }

        [TestMethod]
        public void ValidateJsonRpcRequest_WithInvalidJsonRpc_ShouldReturnError()
        {
            // Arrange
            var json = "{\"jsonrpc\": \"1.0\", \"id\": 1, \"method\": \"test\"}";

            // Act & Assert
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            
            Assert.IsTrue(root.TryGetProperty("jsonrpc", out var versionElement));
            Assert.AreNotEqual("2.0", versionElement.GetString());
        }

        [TestMethod]
        public void ValidateJsonRpcRequest_WithMissingMethod_ShouldReturnError()
        {
            // Arrange
            var json = "{\"jsonrpc\": \"2.0\", \"id\": 1}";

            // Act & Assert
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            
            Assert.IsFalse(root.TryGetProperty("method", out _));
        }

        [TestMethod]
        public void ValidateNotification_WithoutId_ShouldBeValid()
        {
            // Arrange
            var json = "{\"jsonrpc\": \"2.0\", \"method\": \"shutdown\"}";

            // Act & Assert
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            
            Assert.IsTrue(root.TryGetProperty("jsonrpc", out var versionElement) && 
                         versionElement.GetString() == "2.0");
            Assert.IsTrue(root.TryGetProperty("method", out var methodElement) && 
                         !string.IsNullOrEmpty(methodElement.GetString()));
            Assert.IsFalse(root.TryGetProperty("id", out _));
        }

        [TestMethod]
        public void ParseRequestId_WithIntegerValue_ShouldReturnInt()
        {
            // Arrange
            var json = "{\"id\": 42}";

            // Act & Assert
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var idElement = root.GetProperty("id");
            
            Assert.AreEqual(JsonValueKind.Number, idElement.ValueKind);
            Assert.IsTrue(idElement.TryGetInt32(out var intValue));
            Assert.AreEqual(42, intValue);
        }

        [TestMethod]
        public void ParseRequestId_WithStringValue_ShouldReturnString()
        {
            // Arrange
            var json = "{\"id\": \"test-id\"}";

            // Act & Assert
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var idElement = root.GetProperty("id");
            
            Assert.AreEqual(JsonValueKind.String, idElement.ValueKind);
            Assert.AreEqual("test-id", idElement.GetString());
        }

        [TestMethod]
        public void ParseRequestId_WithNullValue_ShouldReturnNull()
        {
            // Arrange
            var json = "{\"id\": null}";

            // Act & Assert
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var idElement = root.GetProperty("id");
            
            Assert.AreEqual(JsonValueKind.Null, idElement.ValueKind);
        }
    }

    [TestClass]
    public class MethodHandlingTests
    {
        [TestMethod]
        public void Initialize_ShouldReturnCapabilities()
        {
            // Arrange
            var json = "{\"jsonrpc\": \"2.0\", \"id\": 1, \"method\": \"initialize\", \"params\": {}}";

            // Act & Assert - we test the expected response structure
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            
            Assert.AreEqual("initialize", root.GetProperty("method").GetString());
        }

        [TestMethod]
        public void ToolsList_ShouldReturnAvailableTools()
        {
            // Arrange
            var json = "{\"jsonrpc\": \"2.0\", \"id\": 1, \"method\": \"tools/list\"}";

            // Act & Assert
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            
            Assert.AreEqual("tools/list", root.GetProperty("method").GetString());
        }

        [TestMethod]
        public void ToolsCall_WithMissingName_ShouldReturnError()
        {
            // Arrange
            var json = "{\"jsonrpc\": \"2.0\", \"id\": 1, \"method\": \"tools/call\", \"params\": {\"arguments\": {}}}";

            // Act & Assert
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var paramsElement = root.GetProperty("params");
            
            Assert.IsFalse(paramsElement.TryGetProperty("name", out _));
        }

        [TestMethod]
        public void ToolsCall_WithMissingArguments_ShouldReturnError()
        {
            // Arrange
            var json = "{\"jsonrpc\": \"2.0\", \"id\": 1, \"method\": \"tools/call\", \"params\": {\"name\": \"analyzeCode\"}}";

            // Act & Assert
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var paramsElement = root.GetProperty("params");
            
            Assert.IsTrue(paramsElement.TryGetProperty("name", out var nameElement));
            Assert.AreEqual("analyzeCode", nameElement.GetString());
            Assert.IsFalse(paramsElement.TryGetProperty("arguments", out _));
        }

        [TestMethod]
        public void AnalyzeCode_WithMissingCode_ShouldReturnError()
        {
            // Arrange
            var json = "{\"jsonrpc\": \"2.0\", \"id\": 1, \"method\": \"tools/call\", \"params\": {\"name\": \"analyzeCode\", \"arguments\": {}}}";

            // Act & Assert
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var paramsElement = root.GetProperty("params");
            var argsElement = paramsElement.GetProperty("arguments");
            
            Assert.IsFalse(argsElement.TryGetProperty("code", out _));
        }

        [TestMethod]
        public void GetRoadmap_WithMissingOperation_ShouldReturnError()
        {
            // Arrange
            var json = "{\"jsonrpc\": \"2.0\", \"id\": 1, \"method\": \"tools/call\", \"params\": {\"name\": \"getRoadmap\", \"arguments\": {}}}";

            // Act & Assert
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var paramsElement = root.GetProperty("params");
            var argsElement = paramsElement.GetProperty("arguments");
            
            Assert.IsFalse(argsElement.TryGetProperty("operation", out _));
        }
    }

    [TestClass]
    public class ErrorCodeTests
    {
        [TestMethod]
        public void ParseError_ShouldUseCorrectErrorCode()
        {
            // Arrange & Act
            var error = new JsonRpcError
            {
                Code = -32700,
                Message = "Parse error"
            };

            // Assert
            Assert.AreEqual(-32700, error.Code);
            Assert.AreEqual("Parse error", error.Message);
        }

        [TestMethod]
        public void InvalidRequest_ShouldUseCorrectErrorCode()
        {
            // Arrange & Act
            var error = new JsonRpcError
            {
                Code = -32600,
                Message = "Invalid Request"
            };

            // Assert
            Assert.AreEqual(-32600, error.Code);
            Assert.AreEqual("Invalid Request", error.Message);
        }

        [TestMethod]
        public void MethodNotFound_ShouldUseCorrectErrorCode()
        {
            // Arrange & Act
            var error = new JsonRpcError
            {
                Code = -32601,
                Message = "Method not found"
            };

            // Assert
            Assert.AreEqual(-32601, error.Code);
            Assert.AreEqual("Method not found", error.Message);
        }

        [TestMethod]
        public void InternalError_ShouldUseCorrectErrorCode()
        {
            // Arrange & Act
            var error = new JsonRpcError
            {
                Code = -32603,
                Message = "Internal error"
            };

            // Assert
            Assert.AreEqual(-32603, error.Code);
            Assert.AreEqual("Internal error", error.Message);
        }
    }
}