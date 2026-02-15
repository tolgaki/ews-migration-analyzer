# Code Review Fix Plan

## Issues to fix (in new/modified code only, ordered by severity):

### High Priority
1. **ConversionValidator.cs**: `IsExpectedMissingGraphReference` is too permissive — CS0103 and CS1061 return true for ALL errors, masking real compilation bugs
2. **HttpLlmClient (ILlmClient.cs)**: Missing response status check, unsafe JSON property access, HttpClient should be static
3. **ConversionOrchestrator.cs**: Silent catch block in `ExtractEwsUsages`; add defensive null check on roadmap.ConversionTier
4. **EwsAnalyzerCodeFixProvider.cs (new method)**: `map` could be default roadmap with null `GraphCodeTemplate`; `InsertGraphSdkConversion` handles this but `root`/`semanticModel` null safety missing
5. **TemplateGuidedConverter.cs**: `BuildRetryPrompt` accesses roadmap properties without null guard
6. **FullContextConverter.cs**: `string.Join` on potentially null elements; null roadmap properties accessed

### Medium Priority
7. **Program.cs**: `ApplyConversionAsync` — File.Copy and File.WriteAllTextAsync lack try-catch
8. **DeterministicTransformerTests.cs**: Verify test for `TransformAuth` default param works correctly

### Won't Fix (pre-existing / by design)
- Program.cs null-forgiving operators on id parsing (pre-existing)
- Fire-and-forget Task.Run pattern (pre-existing, has catch block)
- AnalyzeSymbol First() without error handling (pre-existing)
- InternalsVisibleTo — already correctly configured
- ProgramExtensions location (style only)
