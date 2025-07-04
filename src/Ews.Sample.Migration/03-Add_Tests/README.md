# 03-Add Tests with XUNit and Playwright

## Overview

The solution in this folder adds E2E tests using [Playwright](https://playwright.dev/) to ensure all critical user flows are captured and any changes to the user experience are detected early.

It will also add unit tests using [XUnit](https://xunit.net/) to cover some of the business logic that does not need to reside with the EWS code. This will help ensure that the application remains stable as we refactor and replace EWS components with Microsoft Graph API.

### Why Playwright?

Playwright is a great tool for automating user interface tests across platforms and browsers. It allows you to write tests that simulate user interactions with the application, ensuring that the user experience remains consistent even as the underlying code changes. Playwright executes tests against the running application, so it can cover more ground per test than typical unit and integration tests.

Playwright also supports multiple languages, including C#, TypeScript/JavaScript, and Python, making it flexible for different development environments. However, because new features are first developed on TypeScript and then ported to other languages, the TypeScript version has the most comprehensive feature set. In particular the Playwright UI is only available for TypeScript at this time, which makes it the best choice for this project.

## Step-by-Step Guide

### Adding Copilot Instructions

We are getting to the point in the migration where we need to ensure that the current design is captured in a way that allows GitHub Copilot to make design choices in alignment with the desired patterns.

If your application is following your current coding guidelines already, you can let GitHub Copilot generate the `.github/copilot-instructions.md` file without further instructions.

Try it by executing the following prompt:

```text
Add Copilot instructions in .github/copilot-instructions.md in the root of the solution based on the current design.
```

If you want to change something about the application architecture, you can tweak the file directly or ask GitHub Copilot to make the updates.

We'll use this approach to ensure best practices are followed for the xunit and Playwright tests we are about to create.

The following prompt should have the desired effect:

```text
Add best practices for xunit and Playwright tests to the .github/copilot-instructions.md file.
```

From this point forward, the copilot-instructions.md file will be automatically added to the references for each prompt you run. You can revisit it at any time if you notice patterns you want to change globally. For more information on how to use the copilot-instructions.md file, see the [GitHub Copilot documentation](https://docs.github.com/en/copilot/coding-with-copilot/configuring-copilot-for-your-repository).

### Adding XUnit Tests

To add unit tests simply run the following prompt:

```text
Add unit tests for the business logic in the Contoso.Mail.Web project in a new test project
```

This should result in a new test project using xunit as prescribed in `copilot-instructions.md`, add the project reference to the `Contoso.Mail.Web` project, and add a test class with some sample tests. It will also add the necessary NuGet packages to the test project.

In the build I used, GitHub Copilot got stuck while adding the web project reference to the test project. I stopped the prompt, added the reference manually and then asked Copilot to continue. Sometimes it's easier to make the gesture than trying to get Copilot to fix its mistakes.

After running the the build, I received a number of errors that were easily fixed. One was an amiguous reference between EWS.Task and System.Threading.Tasks.Task. GitHub Copilot suggested the correct fix.





### UI Tests with Playwright

## Next Steps

1. Refactor application for modularity
1. Implement EWS components with Graph API
