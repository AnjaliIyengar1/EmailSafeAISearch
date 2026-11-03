namespace EmailSafeAISearch.Services;

/// <summary>Sends a prompt to an AI model and returns its reply.</summary>
public interface ILlmClient
{
    Task<string> GenerateAsync(string prompt);
}