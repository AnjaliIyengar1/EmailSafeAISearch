namespace EmailSafeAISearch.Models;

// Every stage is returned so the UI can show question -> emails found -> sent to AI -> final.
/// <summary>Every stage of one question: emails found, text sent to the AI, AI answer, and final answer.</summary>
public sealed record AskResult(
    string Question,
    IReadOnlyList<int> RetrievedEmailIds,
    string SentToAi,
    string AiAnswer,
    string Final,
    IReadOnlyDictionary<string, string> Map,
    IReadOnlyList<string> UnknownPlaceholders);