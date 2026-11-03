using Google.GenAI;

namespace EmailSafeAISearch.Services;

/// <summary>Sends prompts to Gemini. Only receives text with PII already hidden.</summary>
// Only ever receives text that has already had its PII hidden.
public sealed class GeminiClient : ILlmClient
{
    private const int MaxAttempts = 3;
    private readonly Client _client;
    private readonly string _model;

    public GeminiClient(IConfiguration config)
    {
        var apiKey = config["Gemini:ApiKey"]
            ?? throw new InvalidOperationException("Gemini:ApiKey is not set. See README.");
        _model = config["Gemini:Model"] ?? "gemini-flash-latest";
        _client = new Client(apiKey: apiKey);
    }

    public async Task<string> GenerateAsync(string prompt)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var response = await _client.Models.GenerateContentAsync(model: _model, contents: prompt);
                return response.Candidates?[0].Content?.Parts?[0].Text ?? string.Empty;
            }
            catch (ServerError) when (attempt < MaxAttempts)
            {
                // Temporary overload on Google's side: wait 2s, then 4s, before retrying.
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)));
            }
        }
    }
}