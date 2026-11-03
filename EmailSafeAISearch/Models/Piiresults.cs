namespace EmailSafeAISearch.Models;

/// <summary>Text with PII replaced by placeholders, plus the map to restore it.</summary>
public sealed record HideResult(string Text, IReadOnlyDictionary<string, string> Map);

/// <summary>Several texts hidden with one shared map, so the same value gets the same placeholder in all of them.</summary>
public sealed record HideManyResult(IReadOnlyList<string> Texts, IReadOnlyDictionary<string, string> Map);

/// <summary>Text with real values restored, plus any placeholders that were not in the map.</summary>
public sealed record RestoreResult(string Text, IReadOnlyList<string> UnknownPlaceholders);