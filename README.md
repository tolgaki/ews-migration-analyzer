# EWS Migration Tools

## Overview

Exchange Web Services (EWS) is a SOAP-based API that is used to access Exchange Online and Exchange Server. Microsoft has announced the deprecation of EWS for Exchange Online in favor of Microsoft Graph API, which provides access to many Microsoft 365 workloads including Exchange Online.

This repo contains tools to help you identify apps within your tenant that are using EWS and help you migrate them to Microsoft Graph API.

## EWS Usage Reporting

M365 provides [reports of EWS usage](https://admin.cloud.microsoft/?#/reportsUsage/EWSWeeklyUsage) in the Microsoft 365 admin center. This your first port of call to identify apps using EWS. The reports are available in the Microsoft 365 admin center under Reports > Usage > Exchange Web Services (EWS) Weekly Usage for tenants in the worldwide cloud.

If your tenant is in an isolated cloud (e.g. government or sovereign cloud), these reports are not available in the admin center at this time. 

The tools in this repo can be used to identify EWS usage in all clouds with access to the Microsoft Graph AuditLog API.

The EWS Usage Reporting tools are located in folder `/src/Ews.AppUsage`.

## EWS Code Analyzer

Once you have identified applications in your tenant using EWS, you can use the EWS Code Analyzer to identify all EWS references in your code and get suggestions for migration to equivalent Microsoft Graph APIs.

The EWS Code Analyzer is located in folder `/src/Ews.CodeAnalyzer`.

## Contributing

This project welcomes contributions and suggestions.  Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit https://cla.opensource.microsoft.com.

When you submit a pull request, a CLA bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., status check, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.

## Trademarks

This project may contain trademarks or logos for projects, products, or services. Authorized use of Microsoft 
trademarks or logos is subject to and must follow 
[Microsoft's Trademark & Brand Guidelines](https://www.microsoft.com/en-us/legal/intellectualproperty/trademarks/usage/general).
Use of Microsoft trademarks or logos in modified versions of this project must not cause confusion or imply Microsoft sponsorship.
Any use of third-party trademarks or logos are subject to those third-party's policies.
