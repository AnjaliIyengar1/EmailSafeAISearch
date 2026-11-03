using EmailSafeAISearch.Models;
using System.Text.RegularExpressions;

namespace EmailSafeAISearch.Services;

// Calls the Presidio Analyzer container (POST /analyze).
/// <summary>Calls the local Presidio container to detect PII; filters out low scores and ignored types.</summary>
public sealed class PresidioClient(HttpClient http, IConfiguration config) : IPiiDetector
{
    // Known internal ID formats (order numbers, tickets) that are never PII.
    private readonly Regex[] _allow = (config.GetSection("Presidio:AllowPatterns").Get<string[]>() ?? [])
        .Select(p => new Regex(p, RegexOptions.Compiled))
        .ToArray();
    private readonly double _threshold = config.GetValue("Presidio:ScoreThreshold", 0.3);
    private readonly HashSet<string> _ignored =
         config.GetSection("Presidio:IgnoredEntityTypes").Get<string[]>()?.ToHashSet() ?? [];
 

    public async Task<IReadOnlyList<PiiEntity>> DetectAsync(string text, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync("analyze", new { text, language = "en" }, ct);
        response.EnsureSuccessStatusCode();

        var results = await response.Content.ReadFromJsonAsync<List<PiiEntity>>(ct) ?? [];
        return results
            .Where(r => r.Score >= _threshold && !_ignored.Contains(r.EntityType))
            .SelectMany(r => SplitAtLineBreaks(text, r))
            .Where(r => !_allow.Any(a => a.IsMatch(text[r.Start..r.End])))
            .OrderBy(r => r.Start)
            .ToList();
    }

    // Presidio can return one span across lines ("TRK-55821-X.\n\nPriya"). PII never spans a
    // line break, so split there and trim surrounding whitespace and trailing punctuation.
    private static IEnumerable<PiiEntity> SplitAtLineBreaks(string text, PiiEntity e)
    {
        var segmentStart = e.Start;
        for (var i = e.Start; i <= e.End; i++)
        {
            if (i < e.End && text[i] != '\n' && text[i] != '\r') continue;

            var start = segmentStart;
            var end = i;
            while (start < end && char.IsWhiteSpace(text[start])) start++;
            while (end > start && (char.IsWhiteSpace(text[end - 1]) || text[end - 1] is '.' or ',' or ';' or ':')) end--;
            if (end > start) yield return e with { Start = start, End = end };

            segmentStart = i + 1;
        }
    }
}