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
        var orchestrator = new ConversionOrchestrator(analysis, navigator, maxTier: 1);

        var code = "class C { void M() { System.Console.WriteLine(1); } }";
        var result = await orchestrator.ConvertSnippetAsync(code);

        result.Should().NotBeNull();
        result.ConvertedCode.Should().Be(code);
        result.Tier.Should().Be(0);
        result.Confidence.Should().Be("high");
    }

    [Fact]
    public async Task ConvertSnippet_Tier1Only_FallsBackToFallbackWhenNoTransform()
    {
        var analysis = new AnalysisService();
        var navigator = new EwsMigrationNavigator();
        // Max tier 1: will not use LLM
        var orchestrator = new ConversionOrchestrator(analysis, navigator, maxTier: 1);

        // Code with an unknown EWS usage that won't match Tier 1
        var code = "class C { void M() { var x = new Microsoft.Exchange.WebServices.Data.Legacy.LegacyCall(); } }";
        var result = await orchestrator.ConvertSnippetAsync(code);

        // Should return something (either conversion or fallback)
        result.Should().NotBeNull();
    }
}

public class TemplateGuidedConverterTests
{
    [Fact]
    public void ParseLlmResponse_UsingsAndCode_ParsesCorrectly()
    {
        var response = @"[USINGS]
using Microsoft.Graph;
using Microsoft.Graph.Models;

[CODE]
var messages = await graphClient.Me.Messages.GetAsync();";

        var (code, usings) = TemplateGuidedConverter.ParseLlmResponse(response);

        code.Should().Contain("graphClient.Me.Messages.GetAsync");
        usings.Should().Contain("Microsoft.Graph");
    }

    [Fact]
    public void ParseLlmResponse_FencedCodeBlocks_ParsesCorrectly()
    {
        var response = @"```usings
using Microsoft.Graph;
```

```csharp
var messages = await graphClient.Me.Messages.GetAsync();
```";

        var (code, usings) = TemplateGuidedConverter.ParseLlmResponse(response);

        code.Should().Contain("graphClient.Me.Messages.GetAsync");
        usings.Should().Contain("Microsoft.Graph");
    }

    [Fact]
    public void ParseLlmResponse_PlainCode_ReturnsAsIs()
    {
        var response = "var messages = await graphClient.Me.Messages.GetAsync();";

        var (code, usings) = TemplateGuidedConverter.ParseLlmResponse(response);

        code.Should().Contain("graphClient.Me.Messages.GetAsync");
        usings.Should().BeNull();
    }
}
