using System.Text.Json;
using EmailSafeAISearch.Models;

namespace EmailSafeAISearch.Services;

public sealed record SearchHit(int Id, string HiddenText, double Score);

// In-memory vector index over the emails. Built once, on first use.
// Emails are hidden BEFORE embedding, so the index and Google's embedding API never see real PII.
public sealed class EmailIndex(
    IServiceScopeFactory scopeFactory,
    IEmbeddingClient embedder,
    IConfiguration config,
    IWebHostEnvironment env)
{
    private sealed record IndexState(
        IReadOnlyList<int> Ids,
        IReadOnlyList<string> HiddenTexts,
        IReadOnlyList<float[]> Vectors,
        IReadOnlyDictionary<string, string> Map);

    private readonly SemaphoreSlim _buildLock = new(1, 1);
    private volatile IndexState? _state;

    // Placeholder map for the whole email set; questions are hidden against it.
    public async Task<IReadOnlyDictionary<string, string>> GetMapAsync(CancellationToken ct = default)
        => (await GetStateAsync(ct)).Map;

    // Brute-force cosine similarity: O(N * d), N = emails, d = vector size. Fine for small N.
    public async Task<IReadOnlyList<SearchHit>> SearchAsync(string hiddenQuestion, int topK, CancellationToken ct = default)
    {
        var state = await GetStateAsync(ct);
        var query = (await embedder.EmbedAsync([hiddenQuestion], ct))[0];

        return Enumerable.Range(0, state.Ids.Count)
            .Select(i => new SearchHit(state.Ids[i], state.HiddenTexts[i], CosineSimilarity(query, state.Vectors[i])))
            .OrderByDescending(h => h.Score)
            .Take(topK)
            .ToList();
    }

    private async Task<IndexState> GetStateAsync(CancellationToken ct)
    {
        if (_state is not null) return _state;

        await _buildLock.WaitAsync(ct);
        try
        {
            if (_state is not null) return _state;

            var path = Path.Combine(env.ContentRootPath, config["Rag:EmailsPath"]
                ?? throw new InvalidOperationException("Rag:EmailsPath is not set."));
            await using var stream = File.OpenRead(path);
            var emails = await JsonSerializer.DeserializeAsync<List<EmailRecord>>(stream, cancellationToken: ct) ?? [];

            // IPiiService is scoped; this class is a singleton, so resolve it in a short-lived scope.
            using var scope = scopeFactory.CreateScope();
            var pii = scope.ServiceProvider.GetRequiredService<IPiiService>();
            var hidden = await pii.HideManyAsync(emails.Select(e => e.RawEmail).ToList(), null, ct);

            var vectors = await embedder.EmbedAsync(hidden.Texts, ct);
            _state = new IndexState(emails.Select(e => e.Id).ToList(), hidden.Texts, vectors, hidden.Map);
            return _state;
        }
        finally
        {
            _buildLock.Release();
        }
    }

    private static double CosineSimilarity(float[] a, float[] b)
    {
        double dot = 0, normA = 0, normB = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }
        return dot / (Math.Sqrt(normA) * Math.Sqrt(normB));
    }
}