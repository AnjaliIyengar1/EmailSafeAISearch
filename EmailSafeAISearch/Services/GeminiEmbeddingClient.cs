using Google.GenAI;
using Google.GenAI.Types;

namespace EmailSafeAISearch.Services;

public interface IEmbeddingClient
{
    Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken ct = default);
}

 /// <summary>Gets embeddings from Gemini. Only receives text with PII already hidden.</summary>
public sealed class GeminiEmbeddingClient : IEmbeddingClient
{
    private readonly Client _client;
    private readonly string _model;

    public GeminiEmbeddingClient(IConfiguration config)
    {
        var apiKey = config["Gemini:ApiKey"]
            ?? throw new InvalidOperationException("Gemini:ApiKey is not set. See README.");
        _model = config["Gemini:EmbeddingModel"] ?? "gemini-embedding-001";
        _client = new Client(apiKey: apiKey);
    }

    // One API call for the whole batch.
    public async Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
    {
        var contents = texts.Select(t => new Content { Parts = [new Part { Text = t }] }).ToList();
        var response = await _client.Models.EmbedContentAsync(_model, contents, cancellationToken: ct);

        var embeddings = response.Embeddings
            ?? throw new InvalidOperationException("Embedding response contained no embeddings.");
        return embeddings.Select(e => (e.Values ?? []).Select(v => (float)v).ToArray()).ToList();
    }
}