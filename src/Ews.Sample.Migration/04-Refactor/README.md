# 04 - Refactor

## Overview

The solution in this folder refactors the code to separate concerns between web request processing, business logic and interaction between the application and the mail APIs. This is a key step in preparing the application for migration from EWS to Microsoft Graph API.

## Step-by-Step Guide

### Refactoring the MailController

The mail controller currently combines multiple responsibilities, including handling HTTP requests and creating the view rendering, processing business logic, and interacting with the EWS API with the ultimate goal of making the mail logic implementation interchangeable between EWS and Graph API.

GitHub Copilot can help with this task as well.

Let's start the process by opening the following files in the IDE:

- `Contoso.Mail.Web/MailController.cs`
- `Contoso.Mail.Web/Program.cs`
- `AppHost/AppHost.cs`

Then run the folllowing prompt:

```prompt
Create a service layer that separates the business logic and email handling into their own service classes. Use interfaces and dependency injection to make the implementations swappable.
```

Copilot created three services to separate concerns between request handling, authentication, email logic and email service interactions.

![Separation of concerns](../../../docs/images/Migration-Refactoring-Services.png)

It also automatically fixed up the unit tests and added several new ones to cover the additional services.

It also updated Program.cs with code to utilize dependency injection to configure the services to be used at runtime.

The tests pass and a quick run of the application shows that the functionality is still intact.

### Implement IEmailService with Graph API

Now that the application is refactored, we can implement the `IEmailService` interface to use Microsoft Graph API instead of EWS.



## Next Steps

1. Refactor application for modularity
1. Implement EWS components with Graph API
