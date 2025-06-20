# 00-Baseline for Contoso Mail Sample Application

## Overview

This folder contains a simple mail application built with ASP.NET MVC that uses Exchange Web Services (EWS) to view and reply to recent emails for an authenticated M365 user. It serves as the baseline for the migration.

The application displays the first 10 items of the M365 mailbox for the logged in user using EWS.

![Sample App Mailbox](../../../docs/images/Migration-App-Mailbox.png)

The user can reply to one of the emails, which will open a new window with a form to reply to the email. 

![Sample App Reply](../../../docs/images/Migration-App-Reply.png)

The reply is sent using EWS.

![Sample App Reply](../../../docs/images/Migration-App-SentMail.png)

## Next Steps

1. Improve observability and local dev experience by adding Aspire to the application.
1. Add UI tests using Playwright to ensure changes we make to the applications do not affect functionality and user experience
1. Improve understanding of the codebase by using GitHub Copilot to generate code comments
