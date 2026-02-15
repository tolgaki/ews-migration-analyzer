using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;

namespace Ews.Analyzer.McpService.Tests;

public class ToolDispatcherTests
{
    [Fact]
    public async Task ToolsList_IncludesAnalyzeSnippet()
    {
        var dispatcher = new ToolDispatcher();
        var tools = dispatcher.ListTools();
        tools.Should().Contain(t => JsonSerializer.Serialize(t).Contains("analyzeSnippet"));
    }

    [Fact]
    public void ToolsList_IncludesConvertToGraph()
    {
        var dispatcher = new ToolDispatcher();
        var tools = dispatcher.ListTools();
        tools.Should().Contain(t => JsonSerializer.Serialize(t).Contains("convertToGraph"));
    }

    [Fact]
    public void ToolsList_IncludesApplyConversion()
    {
        var dispatcher = new ToolDispatcher();
        var tools = dispatcher.ListTools();
        tools.Should().Contain(t => JsonSerializer.Serialize(t).Contains("applyConversion"));
    }

    [Fact]
    public void ToolsList_IncludesConvertAuth()
    {
        var dispatcher = new ToolDispatcher();
        var tools = dispatcher.ListTools();
        tools.Should().Contain(t => JsonSerializer.Serialize(t).Contains("convertAuth"));
    }

    [Fact]
    public void ToolsList_IncludesNewAiDrivenTools()
    {
        var dispatcher = new ToolDispatcher();
        var tools = dispatcher.ListTools();
        var json = JsonSerializer.Serialize(tools);
        json.Should().Contain("getConversionContext");
        json.Should().Contain("getDeterministicConversion");
        json.Should().Contain("validateCode");
    }

    [Fact]
    public async Task ConvertAuth_WithExchangeService_ReturnsConversion()
    {
        var dispatcher = new ToolDispatcher();
        var argsJson = JsonSerializer.Serialize(new
        {
            code = "var service = new ExchangeService(); service.Credentials = new WebCredentials(\"user\", \"pass\");",
            authMethod = "clientCredential"
        });
        var args = JsonDocument.Parse(argsJson).RootElement;
        var result = await dispatcher.CallToolAsync("convertAuth", args);
        var json = JsonSerializer.Serialize(result);
        json.Should().Contain("GraphServiceClient");
    }

    [Fact]
    public async Task GetConversionContext_ReturnsMigrationContext()
    {
        var dispatcher = new ToolDispatcher();
        var argsJson = JsonSerializer.Serialize(new
        {
            sdkQualifiedName = "Microsoft.Exchange.WebServices.Data.ExchangeService.FindItems"
        });
        var args = JsonDocument.Parse(argsJson).RootElement;
        var result = await dispatcher.CallToolAsync("getConversionContext", args);
        var json = JsonSerializer.Serialize(result);

        // Should include Graph equivalent info
        json.Should().Contain("List messages");
        json.Should().Contain("graphApiHttpRequest");
        json.Should().Contain("copilotPromptTemplate");
        json.Should().Contain("hasDeterministicTransform");
        json.Should().Contain("conversionGuidance");
    }

    [Fact]
    public async Task GetDeterministicConversion_KnownOperation_ReturnsConversion()
    {
        var dispatcher = new ToolDispatcher();
        var argsJson = JsonSerializer.Serialize(new
        {
            code = "service.FindItems(WellKnownFolderName.Inbox, view)",
            ewsQualifiedName = "Microsoft.Exchange.WebServices.Data.ExchangeService.FindItems"
        });
        var args = JsonDocument.Parse(argsJson).RootElement;
        var result = await dispatcher.CallToolAsync("getDeterministicConversion", args);
        var json = JsonSerializer.Serialize(result);

        json.Should().Contain("\"available\":true");
        json.Should().Contain("graphClient.Me.Messages.GetAsync");
    }

    [Fact]
    public async Task GetDeterministicConversion_UnknownOperation_ReturnsUnavailable()
    {
        var dispatcher = new ToolDispatcher();
        var argsJson = JsonSerializer.Serialize(new
        {
            code = "service.SomethingUnknown()",
            ewsQualifiedName = "Microsoft.Exchange.WebServices.Data.ExchangeService.SomethingUnknown"
        });
        var args = JsonDocument.Parse(argsJson).RootElement;
        var result = await dispatcher.CallToolAsync("getDeterministicConversion", args);
        var json = JsonSerializer.Serialize(result);

        json.Should().Contain("\"available\":false");
    }

    [Fact]
    public async Task ValidateCode_ValidCode_ReturnsValid()
    {
        var dispatcher = new ToolDispatcher();
        var argsJson = JsonSerializer.Serialize(new
        {
            code = "var x = 42;",
            requiredUsings = "using Microsoft.Graph;",
            tier = 2
        });
        var args = JsonDocument.Parse(argsJson).RootElement;
        var result = await dispatcher.CallToolAsync("validateCode", args);
        var json = JsonSerializer.Serialize(result);

        json.Should().Contain("\"isValid\":true");
    }

    [Fact]
    public async Task ValidateCode_InvalidCode_ReturnsErrors()
    {
        var dispatcher = new ToolDispatcher();
        var argsJson = JsonSerializer.Serialize(new
        {
            code = "var x = ;; invalid code {{{}"
        });
        var args = JsonDocument.Parse(argsJson).RootElement;
        var result = await dispatcher.CallToolAsync("validateCode", args);
        var json = JsonSerializer.Serialize(result);

        json.Should().Contain("\"isValid\":false");
        json.Should().Contain("errors");
    }
}
