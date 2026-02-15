using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Ews.Analyzer;
using Ews.Analyzer.McpService;

namespace Ews.Analyzer.McpService.Tests;

public class ConversionOrchestratorTests
{
    [Fact]
    public async Task ConvertSnippet_NoEws_ReturnsOriginalCode()
    {
        var analysis = new AnalysisService();
        var navigator = new EwsMigrationNavigator();
        var orchestrator = new ConversionOrchestrator(analysis, navigator);

        var code = "class C { void M() { System.Console.WriteLine(1); } }";
        var result = await orchestrator.ConvertSnippetAsync(code);

        result.Should().NotBeNull();
        result.ConvertedCode.Should().Be(code);
        result.Tier.Should().Be(0);
        result.Confidence.Should().Be("high");
    }

    [Fact]
    public async Task ConvertSnippet_UnknownEws_ReturnsGuidanceResult()
    {
        var analysis = new AnalysisService();
        var navigator = new EwsMigrationNavigator();
        var orchestrator = new ConversionOrchestrator(analysis, navigator);

        // Code with an unknown EWS usage that won't match Tier 1
        var code = "class C { void M() { var x = new Microsoft.Exchange.WebServices.Data.Legacy.LegacyCall(); } }";
        var result = await orchestrator.ConvertSnippetAsync(code);

        // Should return a guidance result (not crash or hang on LLM call)
        result.Should().NotBeNull();
    }

    [Fact]
    public void Validator_IsExposedForStandaloneTool()
    {
        var analysis = new AnalysisService();
        var navigator = new EwsMigrationNavigator();
        var orchestrator = new ConversionOrchestrator(analysis, navigator);

        orchestrator.Validator.Should().NotBeNull();
    }

    [Fact]
    public void Transformer_IsExposedForStandaloneTool()
    {
        var analysis = new AnalysisService();
        var navigator = new EwsMigrationNavigator();
        var orchestrator = new ConversionOrchestrator(analysis, navigator);

        orchestrator.Transformer.Should().NotBeNull();
    }
}
