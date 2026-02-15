# Roadmap Lookup

Look up the migration roadmap for a specific EWS operation to find the Graph SDK equivalent.

## Instructions

1. Take the EWS operation name: $ARGUMENTS
   - This can be an EWS SOAP operation (e.g., "FindItem", "SendItem", "CreateItem")
   - Or an SDK qualified name (e.g., "Microsoft.Exchange.WebServices.Data.ExchangeService.FindItems")
2. Look up the operation in the roadmap file at `src/Ews.Code.Analyzer/Ews.Analyzer/roadmap.json`
3. Report:
   - **EWS Operation**: The SOAP operation name and SDK method
   - **Graph Equivalent**: The Graph API name and HTTP request
   - **Status**: Available (GA), Preview, Gap, or TBD
   - **Has Parity**: Yes/No
   - **Documentation**: Links to both EWS and Graph docs
   - **Gap Plan**: If no equivalent, what's the workaround or timeline
   - **Conversion Tier**: 1 (deterministic), 2 (template-LLM), or 3 (full-context)
   - **Code Template**: If a Graph code template exists, show it
4. If the operation isn't found, suggest similar operations from the roadmap
