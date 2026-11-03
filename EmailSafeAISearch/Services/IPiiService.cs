using EmailSafeAISearch.Models;

namespace EmailSafeAISearch.Services;

public interface IPiiService
{
    Task<HideResult> HideAsync(string text, CancellationToken ct = default);
    
    Task<HideManyResult> HideManyAsync(
        IReadOnlyList<string> texts,
        IReadOnlyDictionary<string, string>? existingMap = null,
        CancellationToken ct = default);

    RestoreResult Restore(string text, IReadOnlyDictionary<string, string> map);
}