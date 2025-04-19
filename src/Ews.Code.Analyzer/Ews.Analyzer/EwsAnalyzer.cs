/*
MIT License

    Copyright (c) Microsoft Corporation.

    Permission is hereby granted, free of charge, to any person obtaining a copy
    of this software and associated documentation files (the "Software"), to deal
    in the Software without restriction, including without limitation the rights
    to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
    copies of the Software, and to permit persons to whom the Software is
    furnished to do so, subject to the following conditions:

    The above copyright notice and this permission notice shall be included in all
    copies or substantial portions of the Software.

    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
    IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
    AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
    OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
    SOFTWARE
*/
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Ews.Analyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class EwsAnalyzer : DiagnosticAnalyzer
    {
        public const string defaultDiagnosticId = "EWS000";
        public const string graphAvailableDiagnosticId = "EWS001";
        public const string graphInPreviewDiagnosticId = "EWS002";
        public const string graphUnavailableDiagnosticId = "EWS003";
        public const string graphReferenceCountDiagnosticId = "EWS004";
        public const string callToActionDiagnosticId = "EWS005";

        // You can change these strings in the Resources.resx file. If you do not want your analyzer to be localize-able, you can use regular strings for Title and MessageFormat.
        // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/Localizing%20Analyzers.md for more on localization

        private const string Category = "EWS Deprecation";

        private static readonly LocalizableString graphAvailableTitle = new LocalizableResourceString(nameof(Resources.DefaultTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString graphAvailableMessageFormat = new LocalizableResourceString(nameof(Resources.GraphAvailableMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString graphAvailableDescription = new LocalizableResourceString(nameof(Resources.DefaultDescription), Resources.ResourceManager, typeof(Resources));
        private static readonly DiagnosticDescriptor graphAvailableRule = new DiagnosticDescriptor(graphAvailableDiagnosticId, graphAvailableTitle, graphAvailableMessageFormat, Category, DiagnosticSeverity.Error, isEnabledByDefault: true, description: graphAvailableDescription);

        private static readonly LocalizableString graphInPreviewTitle = new LocalizableResourceString(nameof(Resources.DefaultTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString graphInPreviewMessageFormat = new LocalizableResourceString(nameof(Resources.GraphInPreviewMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString graphInPreviewDescription = new LocalizableResourceString(nameof(Resources.DefaultDescription), Resources.ResourceManager, typeof(Resources));
        private static readonly DiagnosticDescriptor graphInPreviewRule = new DiagnosticDescriptor(graphInPreviewDiagnosticId, graphInPreviewTitle, graphInPreviewMessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: graphInPreviewDescription);

        private static readonly LocalizableString graphUnavailableTitle = new LocalizableResourceString(nameof(Resources.DefaultTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString graphUnavailableMessageFormat = new LocalizableResourceString(nameof(Resources.GraphUnavailableMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString graphUnavailableDescription = new LocalizableResourceString(nameof(Resources.DefaultDescription), Resources.ResourceManager, typeof(Resources));
        private static readonly DiagnosticDescriptor graphUnavailableRule = new DiagnosticDescriptor(graphUnavailableDiagnosticId, graphUnavailableTitle, graphUnavailableMessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: graphUnavailableDescription);

        private static readonly LocalizableString defaultTitle = new LocalizableResourceString(nameof(Resources.DefaultTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString defaultMessageFormat = new LocalizableResourceString(nameof(Resources.DefaultMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString defaultDescription = new LocalizableResourceString(nameof(Resources.DefaultDescription), Resources.ResourceManager, typeof(Resources));
        private static readonly DiagnosticDescriptor defaultRule = new DiagnosticDescriptor(defaultDiagnosticId, defaultTitle, defaultMessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: defaultDescription);


        private static readonly LocalizableString graphReferenceCountMessageFormat = new LocalizableResourceString(nameof(Resources.ReferenceCountMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly DiagnosticDescriptor referenceCountRule = new DiagnosticDescriptor(graphReferenceCountDiagnosticId, graphUnavailableTitle, graphReferenceCountMessageFormat, Category, DiagnosticSeverity.Info, isEnabledByDefault: true, description: graphUnavailableDescription, customTags: new[] { "CompilationEnd" });

        private static readonly LocalizableString customMessageFormat = new LocalizableResourceString(nameof(Resources.DefaultMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly DiagnosticDescriptor customRule = new DiagnosticDescriptor(callToActionDiagnosticId, graphUnavailableTitle, customMessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: graphUnavailableDescription, customTags: new[] { "CompilationEnd" });


        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(defaultRule, graphAvailableRule, graphInPreviewRule, graphUnavailableRule, referenceCountRule, customRule); } }
        private static int _referenceCount = 0;
        private static int _availableRefCount = 0;
        private static int _inPreviewRefCount = 0;
        private static int _unavailableRefCount = 0;

        private static EwsMigrationNavigator _navigator = new EwsMigrationNavigator();

        public override void Initialize(AnalysisContext context)
        {
            //Interlocked.Exchange(ref _referenceCount, 0);

            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            // TODO: Consider registering other actions that act on syntax instead of or in addition to symbols
            // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/Analyzer%20Actions%20Semantics.md for more information
            //context.RegisterSymbolAction(AnalyzeSymbol, SymbolKind.NamedType);
            context.RegisterCompilationStartAction(ctx =>
            {
                Interlocked.Exchange(ref _referenceCount, 0);
                Interlocked.Exchange(ref _availableRefCount, 0);
                Interlocked.Exchange(ref _inPreviewRefCount, 0);
                Interlocked.Exchange(ref _unavailableRefCount, 0);

                ctx.RegisterSymbolAction(AnalyzeEwsSymbol, SymbolKind.NamedType);
                ctx.RegisterSyntaxNodeAction(AnalyzeEwsReferences, SyntaxKind.InvocationExpression);
                //context.RegisterSemanticModelAction(AnalyzeSyntaxTree);
                ctx.RegisterCompilationEndAction(ReportReferenceCount);

            });
        }

        private static void AnalyzeSymbol(SymbolAnalysisContext context)
        {
            // TODO: Replace the following code with your own analysis, generating Diagnostic objects for any issues you find
            var namedTypeSymbol = (INamedTypeSymbol)context.Symbol;

            // Find just those named type symbols with names containing lowercase letters.
            if (namedTypeSymbol.Name.ToCharArray().Any(char.IsLower))
            {
                // For all such symbols, produce a diagnostic.
                var diagnostic = Diagnostic.Create(graphAvailableRule, namedTypeSymbol.Locations[0], namedTypeSymbol.Name);

                context.ReportDiagnostic(diagnostic);
            }
        }

        public static void AnalyzeEwsSymbol(SymbolAnalysisContext context)
        {
            // TODO: Replace the following code with your own analysis, generating Diagnostic objects for any issues you find
            var namedTypeSymbol = (INamedTypeSymbol)context.Symbol;

            // Find just those named type symbols with names containing lowercase letters.
            if (namedTypeSymbol.ContainingNamespace.Name.Contains("Exchange.WebServices."))
            {
                // For all such symbols, produce a diagnostic.
                var diagnostic = Diagnostic.Create(graphAvailableRule, namedTypeSymbol.Locations[0], namedTypeSymbol.Name);

                context.ReportDiagnostic(diagnostic);
            }

        }
        public static void AnalyzeEwsReferences(SyntaxNodeAnalysisContext context)
        {
            var invocationExpr = (InvocationExpressionSyntax)context.Node;

            var memberAccessExpr = invocationExpr.Expression as MemberAccessExpressionSyntax;
            if (memberAccessExpr == null)
            {
                return;
            }

            var methodName = memberAccessExpr.Name.ToString();
            var containingType = context.SemanticModel.GetTypeInfo(memberAccessExpr.Expression).Type;

            if (containingType == null)
            {
                return;
            }

            if (containingType.ToString().StartsWith("Microsoft.Exchange.WebServices."))
            {
                Interlocked.Increment(ref _referenceCount);

                var sdkOperation = $"{containingType}.{methodName}";

                var mapping = _navigator.GetMapByEwsSdkQualifiedName(sdkOperation);

                if (mapping != null && mapping.GraphApiHasParity)
                {
                    // Graph API equivalent is available
                    Interlocked.Increment(ref _availableRefCount);

                    var diagnostic = Diagnostic.Create(
                        graphAvailableRule,
                        invocationExpr.GetLocation(),
                        sdkOperation,
                        mapping.GraphApiDocumentationUrl
                        );

                    context.ReportDiagnostic(diagnostic);
                }
                else
                {
                    // Mapping exists and future Graph API equivalent is known
                    if (mapping != null && !string.IsNullOrEmpty(mapping.GraphApiDisplayName))
                    {
                        // Graph API is available in Preview
                        Interlocked.Increment(ref _inPreviewRefCount);

                        var diagnostic = Diagnostic.Create(graphInPreviewRule,
                                                           invocationExpr.GetLocation(),
                                                           sdkOperation,
                                                           mapping.GraphApiGapFillPlan,
                                                           mapping.GraphApiEta,
                                                           mapping.GraphApiDocumentationUrl);
                        context.ReportDiagnostic(diagnostic);
                    }
                    else
                    {
                        // Graph API equivalent not available
                        Interlocked.Increment(ref _unavailableRefCount);
                        if (mapping != null)
                        {
                            if (mapping.GraphApiGapFillPlan != "TBD")
                            {
                                // There is a plan
                                var diagnostic = Diagnostic.Create(graphUnavailableRule,
                                                               invocationExpr.GetLocation(),
                                                               sdkOperation,
                                                               mapping.GraphApiGapFillPlan,
                                                               mapping.GraphApiEta,
                                                               mapping.GraphApiDocumentationUrl);
                                context.ReportDiagnostic(diagnostic);

                            }
                            else
                            {
                                // There is no plan available in the analyzer data, so we give default advice
                                var diagnostic = Diagnostic.Create(defaultRule,
                                                               invocationExpr.GetLocation(),
                                                               sdkOperation,
                                                               mapping.GraphApiDocumentationUrl,
                                                               mapping.EWSDocumentationUrl);
                                context.ReportDiagnostic(diagnostic);
                            }
                        }
                        else
                        {
                            // No mapping found, this should not happen but we don't want the analyzer to fail
                            var diagnostic = Diagnostic.Create(graphUnavailableRule,
                                                           invocationExpr.GetLocation(),
                                                           sdkOperation,
                                                           "No mapping found",
                                                           "No ETA",
                                                           "No documentation URL");
                            context.ReportDiagnostic(diagnostic);
                        }
                    }

                }
            }

        }


        private static void AnalyzeSyntaxTree(SemanticModelAnalysisContext context)
        {
            var syntaxTree = context.SemanticModel.SyntaxTree;
            var root = syntaxTree.GetRoot(context.CancellationToken);
            var ewsReferences = root.DescendantNodes().OfType<InvocationExpressionSyntax>()
                .Where(invocation => invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                                    memberAccess.Expression.ToString().StartsWith("Microsoft.Exchange.WebServices."));
            foreach (var reference in ewsReferences)
            {
                Interlocked.Increment(ref _referenceCount);
                var diagnostic = Diagnostic.Create(graphAvailableRule, reference.GetLocation(), reference.ToString());
                context.ReportDiagnostic(diagnostic);
            }
            var totalRefdiagnostic = Diagnostic.Create(graphAvailableRule, Location.None, $" SyntaxTree: Total references to Exchange.WebServices: {_referenceCount}");
            context.ReportDiagnostic(totalRefdiagnostic);
        }

        private static void ReportReferenceCount(CompilationAnalysisContext context)
        {
            var diagnostic = Diagnostic.Create(referenceCountRule, Location.None, _referenceCount, "in total.");
            context.ReportDiagnostic(diagnostic);

            diagnostic = Diagnostic.Create(referenceCountRule, Location.None, _availableRefCount, "with generally available alternative Graph API.");
            context.ReportDiagnostic(diagnostic);

            diagnostic = Diagnostic.Create(referenceCountRule, Location.None, _inPreviewRefCount, "with alternative Graph API in preview");
            context.ReportDiagnostic(diagnostic);

            diagnostic = Diagnostic.Create(referenceCountRule, Location.None, _unavailableRefCount, "without available alternative.");
            context.ReportDiagnostic(diagnostic);

            if (_availableRefCount + _inPreviewRefCount > 0)
            {
                var availablePercent = (decimal)(_availableRefCount + _inPreviewRefCount) / _referenceCount * 100;
                diagnostic = Diagnostic.Create(customRule, Location.None, $"{availablePercent}% of EWS operations used in this code base have Graph equivalents. Microsoft recommends manually disabling EWS as soon as possible to improve tenant security posture (see https://aka.ms/mblizz). EWS will be turned off in October 2026. Please, begin migrating your app as soon as possible even if not all known gaps are filled to ensure there is time to address any issues that may arise. Post questions on https://stackoverflow.com with tag [EWS-Graph-Gap]");
                //diagnostic = Diagnostic.Create(customRule, Location.None, $"{availablePercent}% {_availableRefCount}/{_inPreviewRefCount}/{_unavailableRefCount}/{_referenceCount} of EWS operations used in this code base have Graph equivalents. Microsoft recommends manually disabling EWS as soon as possible to improve tenant security posture (see https://aka.ms/mblizz). EWS will be turned off in October 2026. Please, begin migrating your app as soon as possible even if not all known gaps are filled to ensure there is time to address any issues that may arise. Send feedback to ewsfeedback@microsoft.com");

                context.ReportDiagnostic(diagnostic);
            }
        }


    }
}
