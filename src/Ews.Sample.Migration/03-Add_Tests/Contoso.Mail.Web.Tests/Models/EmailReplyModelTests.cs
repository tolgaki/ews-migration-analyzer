using System.ComponentModel.DataAnnotations;
using Contoso.Mail.Models;

namespace Contoso.Mail.Web.Tests.Models;

/// <summary>
/// Unit tests for the EmailReplyModel class to verify validation attributes and behavior.
/// </summary>
public class EmailReplyModelTests
{
    [Fact]
    public void EmailReplyModel_AllPropertiesInitialized_DefaultsToEmptyStrings()
    {
        // Act
        var model = new EmailReplyModel();

        // Assert
        Assert.Equal(string.Empty, model.Id);
        Assert.Equal(string.Empty, model.Subject);
        Assert.Equal(string.Empty, model.To);
        Assert.Equal(string.Empty, model.Body);
    }

    [Fact]
    public void EmailReplyModel_SetAllProperties_PropertiesSetCorrectly()
    {
        // Arrange
        var model = new EmailReplyModel();
        var id = "test-id-123";
        var subject = "Test Subject";
        var to = "test@example.com";
        var body = "Test email body content";

        // Act
        model.Id = id;
        model.Subject = subject;
        model.To = to;
        model.Body = body;

        // Assert
        Assert.Equal(id, model.Id);
        Assert.Equal(subject, model.Subject);
        Assert.Equal(to, model.To);
        Assert.Equal(body, model.Body);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void EmailReplyModel_IdValidation_RequiredAttribute(string? invalidId)
    {
        // Arrange
        var model = new EmailReplyModel
        {
            Id = invalidId!,
            Subject = "Valid Subject",
            To = "valid@example.com",
            Body = "Valid body"
        };

        // Act
        var validationResults = ValidateModel(model);

        // Assert
        var idValidationError = validationResults.FirstOrDefault(v => v.MemberNames.Contains("Id"));
        Assert.NotNull(idValidationError);
        Assert.Contains("required", idValidationError.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void EmailReplyModel_SubjectValidation_RequiredAttribute(string? invalidSubject)
    {
        // Arrange
        var model = new EmailReplyModel
        {
            Id = "valid-id",
            Subject = invalidSubject!,
            To = "valid@example.com",
            Body = "Valid body"
        };

        // Act
        var validationResults = ValidateModel(model);

        // Assert
        var subjectValidationError = validationResults.FirstOrDefault(v => v.MemberNames.Contains("Subject"));
        Assert.NotNull(subjectValidationError);
        Assert.Contains("required", subjectValidationError.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void EmailReplyModel_ToValidation_RequiredAttribute(string? invalidTo)
    {
        // Arrange
        var model = new EmailReplyModel
        {
            Id = "valid-id",
            Subject = "Valid Subject",
            To = invalidTo!,
            Body = "Valid body"
        };

        // Act
        var validationResults = ValidateModel(model);

        // Assert
        var toValidationError = validationResults.FirstOrDefault(v => v.MemberNames.Contains("To"));
        Assert.NotNull(toValidationError);
        Assert.Contains("required", toValidationError.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void EmailReplyModel_BodyValidation_RequiredAttribute(string? invalidBody)
    {
        // Arrange
        var model = new EmailReplyModel
        {
            Id = "valid-id",
            Subject = "Valid Subject",
            To = "valid@example.com",
            Body = invalidBody!
        };

        // Act
        var validationResults = ValidateModel(model);

        // Assert
        var bodyValidationError = validationResults.FirstOrDefault(v => v.MemberNames.Contains("Body"));
        Assert.NotNull(bodyValidationError);
        Assert.Contains("required", bodyValidationError.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EmailReplyModel_BodyDataType_IsMultilineText()
    {
        // Arrange
        var property = typeof(EmailReplyModel).GetProperty(nameof(EmailReplyModel.Body));

        // Act
        var dataTypeAttribute = property?.GetCustomAttributes(typeof(DataTypeAttribute), false)
            .Cast<DataTypeAttribute>()
            .FirstOrDefault();

        // Assert
        Assert.NotNull(dataTypeAttribute);
        Assert.Equal(DataType.MultilineText, dataTypeAttribute.DataType);
    }

    [Fact]
    public void EmailReplyModel_AllPropertiesHaveRequiredAttribute()
    {
        // Arrange
        var properties = typeof(EmailReplyModel).GetProperties();

        // Act & Assert
        foreach (var property in properties)
        {
            var requiredAttribute = property.GetCustomAttributes(typeof(RequiredAttribute), false)
                .Cast<RequiredAttribute>()
                .FirstOrDefault();
            
            Assert.NotNull(requiredAttribute);
        }
    }

    [Fact]
    public void EmailReplyModel_ValidModel_PassesValidation()
    {
        // Arrange
        var model = new EmailReplyModel
        {
            Id = "valid-id-123",
            Subject = "Valid Subject",
            To = "valid@example.com",
            Body = "This is a valid email body with sufficient content."
        };

        // Act
        var validationResults = ValidateModel(model);

        // Assert
        Assert.Empty(validationResults);
    }

    [Fact]
    public void EmailReplyModel_LongContent_PassesValidation()
    {
        // Arrange
        var longSubject = new string('A', 1000);
        var longBody = new string('B', 10000);
        var model = new EmailReplyModel
        {
            Id = "long-content-id",
            Subject = longSubject,
            To = "long@example.com",
            Body = longBody
        };

        // Act
        var validationResults = ValidateModel(model);

        // Assert
        Assert.Empty(validationResults);
        Assert.Equal(longSubject, model.Subject);
        Assert.Equal(longBody, model.Body);
    }

    [Fact]
    public void EmailReplyModel_SpecialCharacters_PassesValidation()
    {
        // Arrange
        var model = new EmailReplyModel
        {
            Id = "special-chars-id-!@#$%^&*()",
            Subject = "Special chars: àáâãäåæçèéêë ñòóôõö øùúûüý",
            To = "special+chars@example-domain.com",
            Body = "Body with special characters: 中文, العربية, русский, 日本語"
        };

        // Act
        var validationResults = ValidateModel(model);

        // Assert
        Assert.Empty(validationResults);
    }

    [Theory]
    [InlineData("  valid-id  ", "  Valid Subject  ", "  valid@example.com  ", "  Valid body  ")]
    public void EmailReplyModel_WhitespaceContent_PassesValidation(string id, string subject, string to, string body)
    {
        // Arrange
        var model = new EmailReplyModel
        {
            Id = id,
            Subject = subject,
            To = to,
            Body = body
        };

        // Act
        var validationResults = ValidateModel(model);

        // Assert
        Assert.Empty(validationResults);
        // Verify whitespace is preserved
        Assert.Equal(id, model.Id);
        Assert.Equal(subject, model.Subject);
        Assert.Equal(to, model.To);
        Assert.Equal(body, model.Body);
    }

    /// <summary>
    /// Helper method to validate a model using the .NET validation framework.
    /// </summary>
    private static List<ValidationResult> ValidateModel(object model)
    {
        var validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(model, null, null);
        Validator.TryValidateObject(model, validationContext, validationResults, true);
        return validationResults;
    }
}
