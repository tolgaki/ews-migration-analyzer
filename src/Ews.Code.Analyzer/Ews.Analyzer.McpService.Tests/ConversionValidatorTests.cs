using System.Collections.Generic;
using Xunit;
using FluentAssertions;
using Ews.Analyzer.McpService;

namespace Ews.Analyzer.McpService.Tests;

public class ConversionValidatorTests
{
    private readonly ConversionValidator _validator = new ConversionValidator();

    [Fact]
    public void Validate_EmptyCode_ReturnsInvalid()
    {
        var result = new ConversionResult
        {
            Tier = 1,
            ConvertedCode = "",
            OriginalCode = "service.FindItems()"
        };

        _validator.Validate(result);

        result.IsValid.Should().BeFalse();
        result.ValidationErrors.Should().Contain("Converted code is empty.");
        result.Confidence.Should().Be("low");
    }

    [Fact]
    public void Validate_ValidSimpleCode_ReturnsValid()
    {
        var result = new ConversionResult
        {
            Tier = 1,
            ConvertedCode = "var x = 1 + 2;",
            OriginalCode = "service.FindItems()"
        };

        _validator.Validate(result);

        result.IsValid.Should().BeTrue();
        result.Confidence.Should().Be("high");
    }

    [Fact]
    public void Validate_SyntaxError_ReturnsInvalid()
    {
        var result = new ConversionResult
        {
            Tier = 2,
            ConvertedCode = "var x = ;; invalid code {{{}",
            OriginalCode = "service.FindItems()"
        };

        _validator.Validate(result);

        result.IsValid.Should().BeFalse();
        result.ValidationErrors.Should().NotBeEmpty();
        result.ValidationErrors.Should().Contain(e => e.StartsWith("Syntax:"));
    }

    [Fact]
    public void Validate_GraphSdkTypes_ToleratesMissingReferences()
    {
        var result = new ConversionResult
        {
            Tier = 1,
            ConvertedCode = "var messages = await graphClient.Me.Messages.GetAsync();",
            OriginalCode = "service.FindItems()",
            RequiredUsings = "using Microsoft.Graph;"
        };

        _validator.Validate(result);

        // Should tolerate missing Graph SDK types
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_Tier1_HighConfidence()
    {
        var result = new ConversionResult
        {
            Tier = 1,
            ConvertedCode = "var x = 42;",
            OriginalCode = "service.FindItems()"
        };

        _validator.Validate(result);

        result.IsValid.Should().BeTrue();
        result.Confidence.Should().Be("high");
    }

    [Fact]
    public void Validate_Tier2_WithNoErrors_HighConfidence()
    {
        var result = new ConversionResult
        {
            Tier = 2,
            ConvertedCode = "var x = 42;",
            OriginalCode = "service.FindItems()"
        };

        _validator.Validate(result);

        result.IsValid.Should().BeTrue();
        result.Confidence.Should().Be("high");
    }

    [Fact]
    public void Validate_Tier3_WithNoErrors_MediumConfidence()
    {
        var result = new ConversionResult
        {
            Tier = 3,
            ConvertedCode = "var x = 42;",
            OriginalCode = "service.FindItems()"
        };

        _validator.Validate(result);

        result.IsValid.Should().BeTrue();
        result.Confidence.Should().Be("medium");
    }

    [Fact]
    public void Validate_RemainingEwsReference_DowngradesConfidence()
    {
        var result = new ConversionResult
        {
            Tier = 1,
            ConvertedCode = "// Still uses Microsoft.Exchange.WebServices somewhere\nvar x = 42;",
            OriginalCode = "service.FindItems()"
        };

        _validator.Validate(result);

        result.IsValid.Should().BeTrue();
        result.ValidationErrors.Should().Contain(e => e.Contains("Microsoft.Exchange.WebServices"));
    }

    [Fact]
    public void Validate_WhitespaceOnly_ReturnsInvalid()
    {
        var result = new ConversionResult
        {
            Tier = 2,
            ConvertedCode = "   \n  \t  ",
            OriginalCode = "service.FindItems()"
        };

        _validator.Validate(result);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_FullClassCode_PassesValidation()
    {
        var result = new ConversionResult
        {
            Tier = 3,
            ConvertedCode = @"
namespace MyApp
{
    class MailService
    {
        public async System.Threading.Tasks.Task GetMessagesAsync()
        {
            var x = 42;
        }
    }
}",
            OriginalCode = "// original"
        };

        _validator.Validate(result);

        result.IsValid.Should().BeTrue();
    }
}
