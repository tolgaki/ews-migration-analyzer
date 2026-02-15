# Add Deterministic Transform

Add a new Tier 1 deterministic EWS→Graph transform to DeterministicTransformer.cs.

## Instructions

1. Parse the input: $ARGUMENTS
   - Expected format: "EWS.Operation → Graph.Equivalent" or a description of the transform
2. Read `src/Ews.Code.Analyzer/Ews.Analyzer.McpService/DeterministicTransformer.cs`
3. Identify the pattern to follow from existing transforms in the `_transforms` dictionary
4. Add the new transform:
   - Key: lowercase EWS SDK qualified name (e.g., "microsoft.exchange.webservices.data.emailmessage.bind")
   - Value: Lambda function `(InvocationContext ctx) => string?` that returns the Graph SDK code
   - Include proper `using` statements in the result
   - Include the required NuGet package
5. Add a corresponding test in `src/Ews.Code.Analyzer/Ews.Analyzer.McpService.Tests/DeterministicTransformerTests.cs`
6. Build and run the tests to verify
7. Show the diff of changes made

## Example

Input: "ExchangeService.ResolveName → graphClient.Me.People.GetAsync"

Would add to the transforms dictionary:
```csharp
["microsoft.exchange.webservices.data.exchangeservice.resolvename"] = ctx =>
    "// Graph SDK: Resolve name using People API\n" +
    "var people = await graphClient.Me.People.GetAsync(config =>\n" +
    "{\n" +
    "    config.QueryParameters.Search = \"searchTerm\";\n" +
    "    config.QueryParameters.Top = 10;\n" +
    "});"
```
