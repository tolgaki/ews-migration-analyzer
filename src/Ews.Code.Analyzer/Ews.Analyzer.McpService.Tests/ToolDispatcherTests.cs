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
}
