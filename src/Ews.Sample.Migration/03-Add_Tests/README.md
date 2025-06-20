# 03-Add E2E Tests with Playwright

## Overview

The solution in this folder adds E2E tests using [Playwright](https://playwright.dev/) to ensure all critical user flows are captured and any changes to the user experience are detected early.

Playwright is a great tool for automating user interface tests across platforms and browsers. It allows you to write tests that simulate user interactions with the application, ensuring that the user experience remains consistent even as the underlying code changes. Playwright executes tests against the running application, so it can cover more ground per test than typical unit and integration tests.

Playwright also supports multiple languages, including C#, TypeScript/JavaScript, and Python, making it flexible for different development environments. However, because new features are first developed on TypeScript and then ported to other languages, the TypeScript version has the most comprehensive feature set. In particular the Playwright UI is only available for TypeScript at this time, which makes it the best choice for this project.

## Step-by-Step Guide

### UI Tests with Playwright

## Next Steps

1. Refactor application for modularity
1. Implement EWS components with Graph API
