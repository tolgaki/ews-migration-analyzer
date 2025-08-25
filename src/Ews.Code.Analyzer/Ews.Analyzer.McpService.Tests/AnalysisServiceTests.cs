using System.Threading.Tasks;
using Xunit;
using FluentAssertions;

namespace Ews.Analyzer.McpService.Tests;

public class AnalysisServiceTests
{
    [Fact]
    public async Task AnalyzeSnippet_NoEws_NoEwsUsage()
    {
        var svc = new AnalysisService();
        var code = "class C { void M() { System.Console.WriteLine(1); } }";
        var result = await svc.AnalyzeSnippetAsync(code);
        result.Diagnostics.Should().NotBeNull();
        result.Diagnostics.Should().OnlyContain(d => d.EwsUsage == null);
    }
}
