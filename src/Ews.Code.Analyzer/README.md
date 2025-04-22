# EWS Code Analyzer

## Overview

The EWS Code Analyzer helps you identify EWS references in your code and get suggestions for the migration to equivalent Microsoft Graph APIs.

The easiest and most flexible way to use the EWS Code Analyzer is to install it as a NuGet package in your EWS application project.

## Building the EWS Analyzer Nuget Package

1. Create a local NuGet package source in Visual Studio (Tools > NuGet Package Manager > Package Manager Settings) and add a new Package Source with the path to the folder where you want to store the package, for example `C:\NugetPackages`. Alternatively, you can create the package source from the command line with `nuget sources add -Name LocalNuget -Source C:\NugetPackage`
1. Clone this repository to your local machine
1. Open the solution `src/Ews.Code.Analyzer/Ews.Analyzer.sln` in Visual Studio 2022
1. Select the build configuration (Debug or Release) and the target platform (x64 or x86) in the Visual Studio toolbar
1. Build the solution
1. Find the `Ews.Analyzer.Package` project in the solution explorer
1. Go to the ./bin/{configuration}/ folder of the project
1. Copy the `Ews.Analyzer.{version}.nupkg` file to the local NuGet package source folder you created in step 1

## Using the EWS Analyzer NuGet Package

1. Open your EWS application project in Visual Studio 2022
1. Right-click on the project in the solution explorer and select "Manage NuGet Packages"
1. Select the "Browse" tab and select the local NuGet package source you created in the previous section
1. Search for "Ews.Analyzer" and install the package

If your solution has any references to EWS, the EWS Analyzer will automatically detect them and display them in the Error List window. The references in the code will also be highlighted in the code editor.

## Next Steps

The EWS Analyzer will give you a list of EWS references in your code and suggestions for the migration to equivalent Microsoft Graph APIs.

Review the suggestions and the resources provided in the error list. They will include links to the [EWS to Graph API mapping page](https://aka.ms/ewsMapGH) and the [EWS Deprecation page](https://aka.ms/ews1pageGH).

## Future Versions

We are investigating additional features like:

- Include parity roadmap information for EWS APIs that don't have a clear equivalent
- Improved code fixes that generate prompts for GitHub Copilot to generate alterative implementations

Let us know what features you'd like to see by creating an issue in the [GitHub Repository](https://github.com/OfficeDev/ews-migration-analyzer/issues)