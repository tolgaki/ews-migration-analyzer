# 02-Add Instrumentation

## Overview

The solution in this folder adds instrumentation using Aspire and UI tests using Playwright to the Contoso Mail sample application. Aspire adds value even though this application is not very complex because it improves the inner loop for developers and observability of the application backend where all of the code and configuration changes will occur. Aspire also provide a smooth path to an improved deployment story and support for additional services if the application is extended now or in the future.

The other improvement we should make now before we start changing the code to replace EWS is to add automated tests to the application to ensure the user experience is not impacted by the changes and we detect any regressions as early as possible. Playwright is a great tool for testing the user experience. We'll also look for opportunities to add unit tests for some of the business logic that does not need to reside with the EWS code.

## Step-by-Step Guide

### Instrumentation with Aspire

### UI Tests with Playwright

## Next Steps

1. Refactor application for modularity
1. Implement EWS components with Graph API
