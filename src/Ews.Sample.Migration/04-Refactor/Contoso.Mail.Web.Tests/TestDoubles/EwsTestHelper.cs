using Microsoft.Exchange.WebServices.Data;

namespace Contoso.Mail.Web.Tests.TestDoubles;

/// <summary>
/// Helper class for creating test doubles for Exchange Web Services objects.
/// </summary>
public static class EwsTestHelper
{
    /// <summary>
    /// Creates a mock EmailMessage for testing purposes.
    /// Note: EmailMessage is sealed and cannot be easily mocked, so this helper
    /// provides guidance for working with EWS objects in tests.
    /// </summary>
    public static class MockEmailMessage
    {
        public static string CreateMockEmailId() => "AAMkADY2OWI5ZjI5LWRkYTMtNDI4Yy1hYjU5LTEwZjU5ODBhMzUwZAAGAAAAAABVhZ8SQzByRoOHJn2Wa+CcBwDK_FG_9Fm_QaanNf2GRd39AAAAAAEMAADS_FG_9Fm_QaanNf2GRd39AAABH8U6AAA=";
        
        public static string CreateMockChangeKey() => "CQAAABYAAADo2WP5Z9uQSKO0Q5U2xBdDAAABH8U7";
        
        public static ItemId CreateMockItemId() => new(CreateMockEmailId());
    }
    
    /// <summary>
    /// Creates mock EWS service configuration for testing.
    /// </summary>
    public static class MockExchangeService
    {
        public static string DefaultEwsUrl => "https://outlook.office365.com/EWS/Exchange.asmx";
        
        public static ExchangeVersion DefaultVersion => ExchangeVersion.Exchange2013_SP1;
        
        public static string[] DefaultScopes => new[] { "https://outlook.office365.com/.default" };
    }
    
    /// <summary>
    /// Helper methods for creating test data that matches EWS patterns.
    /// </summary>
    public static class TestData
    {
        public static string CreateMockEmailSubject() => "Test Email Subject";
        
        public static string CreateMockEmailBody() => "This is a test email body content.";
        
        public static string CreateMockEmailAddress() => "sender@contoso.com";
        
        public static string CreateMockDisplayName() => "Test Sender";
        
        public static DateTime CreateMockDateTime() => new(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc);
    }
}
