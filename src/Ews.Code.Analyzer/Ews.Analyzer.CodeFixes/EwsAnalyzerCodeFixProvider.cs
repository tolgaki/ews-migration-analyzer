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
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ews.Analyzer
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(EwsAnalyzerCodeFixProvider)), Shared]
    public class EwsAnalyzerCodeFixProvider : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(EwsAnalyzer.graphAvailableDiagnosticId,
                                               EwsAnalyzer.graphInPreviewDiagnosticId,
                                               EwsAnalyzer.graphUnavailableDiagnosticId); }
        }

        public sealed override FixAllProvider GetFixAllProvider()
        {
            // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            // TODO: Replace the following code with your own analysis, generating a CodeAction for each fix to suggest
            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            // Find the type declaration identified by the diagnostic.
            var declaration = root.FindToken(diagnosticSpan.Start).Parent.AncestorsAndSelf().OfType<TypeDeclarationSyntax>().First();
            var expression = root.FindNode(diagnostic.Location.SourceSpan).DescendantNodesAndSelf().OfType<ExpressionSyntax>()
                .First();

            // Register a code action that will invoke the fix.
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: CodeFixResources.CodeFixTitle,
                    createChangedSolution: c => InsertReferenceToGraphApi(context.Document, expression, c),
                    equivalenceKey: nameof(CodeFixResources.CodeFixTitle)),
                diagnostic);
        }

        private async Task<Solution> InsertReferenceToGraphApi(Document document, ExpressionSyntax expression, CancellationToken cancellationToken)
        {

            var originalSolution = document.Project.Solution;
            var optionSet = originalSolution.Workspace.Options;

            // Get the syntax root of the document.
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

            // Get the semantic model.
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

            // Find the containing class declaration.
            var containingClass = expression.AncestorsAndSelf().OfType<ClassDeclarationSyntax>().FirstOrDefault();

            // Get the symbol for the expression.
            var symbolInfo = semanticModel.GetSymbolInfo(expression, cancellationToken);
            var symbol = symbolInfo.Symbol ?? symbolInfo.CandidateSymbols.FirstOrDefault();

            // Construct the fully qualified name.
            // var fullyQualifiedName = symbol?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var fullyQualifiedName = symbol?.ToString();

            // Find the end of the line after the selected expression
            var nextTrivia = root.FindTrivia(expression.Span.End + 1);

            var navigator = new EwsMigrationNavigator();
            var fqnKey = fullyQualifiedName.Replace("()", "");
            var map = navigator.GetMapByEwsSdkQualifiedName(fqnKey);

            SyntaxTrivia copilotInstructionsLine3;
            SyntaxTrivia copilotInstructionsLine4;

            // Create the copilot instructions to safely replace the deprecated code
            var copilotInstructionsLine1 = SyntaxFactory.Comment("// TODO ADDRESS EWS SECURITY RISK: refactor with Graph API equivalent. See https://aka.ms/mblizz for details.");
            var copilotInstructionsLine2 = SyntaxFactory.Comment($"/generate \nFollow these instructions to replace deprecated Exchange Web Services code with Graph API equivalents:\n1. Extract an interface for class {containingClass.Identifier.Text}");

            if (string.IsNullOrEmpty(map?.GraphApiHttpRequest))
            {
                copilotInstructionsLine3 = SyntaxFactory.Comment($"2. Implement a class using Graph API equivalent of {expression} ({fullyQualifiedName})");
            }
            else
            {
                copilotInstructionsLine3 = SyntaxFactory.Comment($"2. Implement a class using Graph API equivalent of {expression} ({fullyQualifiedName}): {map.GraphApiHttpRequest}");
            }
            if (string.IsNullOrEmpty(map?.GraphApiDocumentationUrl))
            {
                copilotInstructionsLine4 = SyntaxFactory.Comment($"3. Reference sample code at https://aka.ms/EWSMigrationSamples");
            }
            else
            {
                copilotInstructionsLine4 = SyntaxFactory.Comment($"3. Reference {map.GraphApiDocumentationUrl}");
            }


            var newLine = SyntaxFactory.EndOfLine(Environment.NewLine);
            var openMultiLineComment = SyntaxFactory.Comment("/*");
            var closeMultiLineComment = SyntaxFactory.Comment("*/");

            var newTriviaList = SyntaxFactory.TriviaList(
                copilotInstructionsLine1,
                newLine,
                openMultiLineComment,
                newLine,
                copilotInstructionsLine2,
                newLine,
                copilotInstructionsLine3,
                newLine,
                copilotInstructionsLine4,
                newLine,
                closeMultiLineComment,
                newLine);

            // Insert the instructions after the selected expression
            var newRoot = root.InsertTriviaAfter(nextTrivia, newTriviaList);

            // Create a new document with the updated syntax root.
            var newDocument = document.WithSyntaxRoot(newRoot);

            // Return the new solution with the updated document.
            return newDocument.Project.Solution;

        }
    }
}
