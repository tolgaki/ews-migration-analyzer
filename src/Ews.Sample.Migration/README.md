# Tutorial - Migrating from EWS to Microsoft Graph API with GitHub Copilot

## Overview

EWS has been identified as a security vulnerability and will be disabled in October 2026. This means all applications using EWS must either be sunset or migrated to a supported API, such as Microsoft Graph API.

The many applications using EWS have been built a over the past 20 years reflect a variety of design patterns and coding practices. It is highly likely that the applications weren't written by the teams now responsible for modernizing them. Documentation and automated tests may be lacking or out of date. All of these factors can make getting started a challenge and upgrading the applications a chore.

Fortunately, there are tools and techniques that can help build the understanding of legacy applications in your team's portfolio and accelerate the migration of those applications to a supported platform.

This folder contains a simple mail application built with ASP.NET MVC that uses Exchange Web Services (EWS) to view and reply to recent emails for an authenticated M365 user. It serves as the baseline for the migration.

While all the migration steps described in this sample can be performed manually, we'll make heavy use of GitHub Copilot to accelerate the process and improve the code quality as we go.

We hope that learning about using AI tools like GitHub Copilot on what would otherwise be a tedious process will benefit your teams twofold by eliminating a dangerous security vulnerability and set you up for success with AI tools on future projects.

## Getting Started

### Prerequisites

1. Ensure you have the prerequisites installed:
   - .NET SDK (version 9.0 or later)
   - Visual Studio 2022 or later
   - Microsoft Exchange Online account for testing
   - Access to Microsoft Entra ID (Azure AD) for app registration
1. Create an app registration in Entra ID with the following settings:
    - Delegated Graph API permissions (grant admin consent if you can):
       - `EWS.AccessAsUser.All`
       - `User.Read`
    - Redirect URIs for platform Web:
       - `http://localhost:5024/signin-oidc` (port may vary based on your setup)
       - `https://localhost:7020/signin-oidc` (port may vary based on your setup)
1. Take note of the following values from the app registration:
   - Application (client) ID
   - Directory (tenant) ID
