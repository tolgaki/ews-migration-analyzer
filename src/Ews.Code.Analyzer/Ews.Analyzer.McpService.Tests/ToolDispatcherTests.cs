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
}
