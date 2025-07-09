# Requirements Document for Contoso Mail Web Application

## Technology Stack
- **Language:** C#
- **Framework:** .NET 9
- **Web Framework:** ASP.NET Core Razor Pages
- **Email API:** Exchange Web Services (EWS) Managed API
- **Authentication:** Azure Active Directory (Azure AD) using Microsoft.Identity.Web

## Authentication Mechanism
- The application uses Azure AD for user authentication.
- Authentication is implemented via the Microsoft.Identity.Web library, leveraging OAuth2 and OpenID Connect protocols.
- Users must sign in with their organizational (work or school) account to access any mailbox features.
- Access tokens are acquired for EWS API calls on behalf of the signed-in user.

## Important Dependencies
- `Microsoft.Identity.Web` (for Azure AD authentication and token acquisition)
- `Microsoft.Exchange.WebServices` (for EWS API integration)
- `Microsoft.Extensions.Logging` (for logging)
- `Microsoft.AspNetCore.Mvc` (for MVC and Razor Pages support)

## Use Cases Implemented

### 1. View Mailbox (Inbox)
- **Description:**
  - Authenticated users can view the first 10 emails in their Inbox.
  - The application fetches emails using EWS and displays sender, subject, and other details.
  - User's display name and email are shown in the UI.
- **Error Handling:**
  - If authentication fails or the mailbox cannot be accessed, the user is challenged to re-authenticate or shown an error message.

### 2. Reply to Email
- **Description:**
  - Users can select an email to reply to.
  - The reply form is pre-filled with the recipient, subject (prefixed with "RE:"), and a quoted version of the original message.
  - The user can edit the reply before sending.
- **Error Handling:**
  - If the email cannot be found (e.g., deleted or moved), the user is redirected to the mailbox with an error message.
  - Authentication errors prompt the user to re-authenticate.

### 3. Send Reply
- **Description:**
  - When the reply form is submitted, the application sends the reply via EWS and saves a copy in the Sent Items folder.
  - Success and error messages are displayed to the user.
- **Error Handling:**
  - EWS or authentication errors are logged and shown to the user in the UI.

### 4. Error Handling
- **Description:**
  - A generic error page is available to display request details and troubleshooting information if an unhandled exception occurs.

## Notes
- All mailbox operations are performed using the signed-in user's identity and permissions.
- The application is designed for organizational use with Microsoft 365 mailboxes.
