using EmailSafeAISearch.Models;

namespace EmailSafeAISearch.Services;

public interface IAskService
{
    Task<AskResult> AskAsync(string question, CancellationToken ct = default);
}

// Full flow: hide question -> search hidden emails (RAG) -> ask Gemini -> restore PII.
public sealed class AskService(IPiiService pii, EmailIndex index, ILlmClient llm, IConfiguration config) : IAskService
{
    private const string Instructions =
        """
        You answer questions using only the emails below. If they do not contain the answer, say so.
        Values like <PERSON_1> or <PHONE_NUMBER_1> are placeholders for hidden private data.
        Copy placeholders exactly as written. Do not change, merge, or invent placeholders.
        Write placeholders as plain text, without backticks or quotes.
        """;

    private readonly int _topK = config.GetValue("Rag:TopK", 3);
    
    public async Task<AskResult> AskAsync(string question, CancellationToken ct = default)
    {
        // Hide the question with the same map as the emails, so "Mark" is the same placeholder everywhere.
        var corpusMap = await index.GetMapAsync(ct);
        var hiddenQuestion = await pii.HideManyAsync([question], corpusMap, ct);
        var questionText = hiddenQuestion.Texts[0];

        var hits = await index.SearchAsync(questionText, _topK, ct);

        var context = string.Join("\n\n---\n\n", hits.Select(h => $"Email {h.Id}:\n{h.HiddenText}"));
        var sentToAi = $"{context}\n\nQuestion: {questionText}";

        var aiAnswer = await llm.GenerateAsync($"{Instructions}\n{sentToAi}");
        var restored = pii.Restore(aiAnswer, hiddenQuestion.Map);

        // Return only the placeholders used in this request, not the whole email set's map.
        var usedMap = hiddenQuestion.Map
            .Where(kv => sentToAi.Contains(kv.Key))
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        return new AskResult(
            question,
            hits.Select(h => h.Id).ToList(),
            sentToAi,
            aiAnswer,
            restored.Text,
            usedMap,
            restored.UnknownPlaceholders);
    }
}