using EmailSafeAISearch.Models;

namespace EmailSafeAISearch.Services;

public interface IPiiDetector
{
    Task<IReadOnlyList<PiiEntity>> DetectAsync(string text, CancellationToken ct = default);
}