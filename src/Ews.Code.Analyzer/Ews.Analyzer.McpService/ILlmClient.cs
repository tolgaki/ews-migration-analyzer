using System;
using System.Threading;
using System.Threading.Tasks;

namespace Ews.Analyzer.McpService;

/// <summary>
/// Abstraction over an LLM backend used for Tier 2 and Tier 3 conversions.
/// </summary>
internal interface ILlmClient
{
    /// <summary>
    /// Send a prompt to the LLM and get a completion response.
    /// </summary>
    Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default);
}

/// <summary>
/// LLM client that returns the prompt as an MCP sampling/createMessage request.
/// The MCP host (Copilot, Claude, etc.) handles the actual LLM call.
/// When used standalone this simply returns the assembled prompt as the "conversion"
/// so the caller can relay it to their own LLM.
/// </summary>
internal sealed class McpRelayLlmClient : ILlmClient
{
    public Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
        // In MCP mode the host performs the LLM call.
        // We return a structured prompt block that the MCP host can relay.
        var combined = $"[SYSTEM]\n{systemPrompt}\n\n[USER]\n{userPrompt}";
        return Task.FromResult(combined);
    }
}

/// <summary>
/// LLM client that calls an HTTP endpoint directly (e.g. Azure OpenAI, Anthropic, OpenAI).
/// Configured via environment variables: LLM_ENDPOINT, LLM_API_KEY, LLM_MODEL.
/// </summary>
internal sealed class HttpLlmClient : ILlmClient
{
    private readonly string _endpoint;
    private readonly string _apiKey;
    private readonly string _model;
    private static readonly System.Net.Http.HttpClient _http = new();

    public HttpLlmClient()
    {
        _endpoint = Environment.GetEnvironmentVariable("LLM_ENDPOINT") ?? string.Empty;
        _apiKey = Environment.GetEnvironmentVariable("LLM_API_KEY") ?? string.Empty;
        _model = Environment.GetEnvironmentVariable("LLM_MODEL") ?? "gpt-4o";

        // Validate that the endpoint uses HTTPS to protect the API key in transit.
        if (!string.IsNullOrWhiteSpace(_endpoint) &&
            !_endpoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
            !_endpoint.StartsWith("http://localhost", StringComparison.OrdinalIgnoreCase) &&
            !_endpoint.StartsWith("http://127.0.0.1", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "LLM_ENDPOINT must use HTTPS (or localhost for development). " +
                "Sending API keys over unencrypted HTTP is not allowed.");
        }
    }

    public async Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_endpoint) || string.IsNullOrWhiteSpace(_apiKey))
        {
            return "[ERROR] LLM_ENDPOINT and LLM_API_KEY environment variables are required for HTTP LLM calls.";
        }

        var body = System.Text.Json.JsonSerializer.Serialize(new
        {
            model = _model,
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            temperature = 0.2,
            max_tokens = 4096
        });

        var request = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Post, _endpoint)
        {
            Content = new System.Net.Http.StringContent(body, System.Text.Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Authorization", $"Bearer {_apiKey}");

        var response = await _http.SendAsync(request, ct);
        var json = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            // Do not return raw API response body — it may contain sensitive information.
            return $"[ERROR] LLM API returned HTTP {(int)response.StatusCode}. Check your LLM_ENDPOINT and LLM_API_KEY configuration.";
        }

        // Parse the first choice content from OpenAI-compatible response
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("choices", out var choices) &&
                choices.GetArrayLength() > 0)
            {
                var firstChoice = choices[0];
                if (firstChoice.TryGetProperty("message", out var msg) &&
                    msg.TryGetProperty("content", out var content))
                {
                    return content.GetString() ?? string.Empty;
                }
            }
        }
        catch (System.Text.Json.JsonException)
        {
            return "[ERROR] Failed to parse LLM response. The endpoint may not be OpenAI-compatible.";
        }

        return json;
    }
}
