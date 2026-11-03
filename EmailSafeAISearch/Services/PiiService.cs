using System.Text;
using System.Text.RegularExpressions;
using EmailSafeAISearch.Models;

namespace EmailSafeAISearch.Services;

// Presidio only detects PII (type + position). Hiding and restoring are done here in C#.
// Stateless: all per-request state is local, so one instance is safe to share across requests.
public sealed partial class PiiService(IPiiDetector detector) : IPiiService
{
    // Optional backticks: the AI sometimes wraps placeholders in markdown code formatting.
    [GeneratedRegex(@"`?(<[A-Z][A-Z0-9_]*_\d+>)`?")]
    private static partial Regex PlaceholderRegex();

    public async Task<HideResult> HideAsync(string text, CancellationToken ct = default)
    {
        var result = await HideManyAsync([text], null, ct);
        return new HideResult(result.Texts[0], result.Map);
    }

    // O(t * n * v): t = texts, n = text length, v = distinct PII values known.
    public async Task<HideManyResult> HideManyAsync(
        IReadOnlyList<string> texts,
        IReadOnlyDictionary<string, string>? existingMap = null,
        CancellationToken ct = default)
    {
        var detected = await Task.WhenAll(texts.Select(t => detector.DetectAsync(t, ct)));

        var map = existingMap is null ? new Dictionary<string, string>() : new Dictionary<string, string>(existingMap);
        var placeholderByValue = map.ToDictionary(kv => kv.Value, kv => kv.Key);
        var counters = new Dictionary<string, int>();
        foreach (var placeholder in map.Keys)
        {
            var (type, n) = ParsePlaceholder(placeholder);
            counters[type] = Math.Max(counters.GetValueOrDefault(type), n);
        }

        // Pass 1: one placeholder per distinct value, numbered per type.
        for (var i = 0; i < texts.Count; i++)
        {
            foreach (var e in detected[i])
            {
                var value = texts[i][e.Start..e.End];
                AddValue(value, e.EntityType);

                // "Mark Lopez" -> also hide "Mark" and "Lopez", which Presidio often misses on their own.
                if (e.EntityType == "PERSON")
                {
                    foreach (var part in value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (part.Length >= 3 && part.All(char.IsLetter)) AddValue(part, "PERSON");
                    }
                }
            }
        }

        // Pass 2: replace every occurrence of every known value, including ones Presidio
        // missed in a given text (e.g. a name it caught in the question but not in the email).
        var hidden = new string[texts.Count];
        for (var i = 0; i < texts.Count; i++)
        {
            hidden[i] = Replace(texts[i], FindAllOccurrences(texts[i], placeholderByValue));
        }

        return new HideManyResult(hidden, map);

        void AddValue(string value, string type)
        {  
            if (placeholderByValue.ContainsKey(value)) return;
            var n = counters.GetValueOrDefault(type) + 1;
            counters[type] = n;
            var placeholder = $"<{type}_{n}>";
            placeholderByValue[value] = placeholder;
            map[placeholder] = value;
        }
    }

    // O(n): single regex pass over the text.
    public RestoreResult Restore(string text, IReadOnlyDictionary<string, string> map)
    {
        var unknown = new List<string>();
        var restored = PlaceholderRegex().Replace(text, m =>
        {
            var placeholder = m.Groups[1].Value;
            if (map.TryGetValue(placeholder, out var original)) return original;
            unknown.Add(placeholder);
            return placeholder;
        });
        return new RestoreResult(restored, unknown);
    }

    // "<PERSON_12>" -> ("PERSON", 12)
    private static (string Type, int Number) ParsePlaceholder(string placeholder)
    {
        var inner = placeholder[1..^1];
        var split = inner.LastIndexOf('_');
        return (inner[..split], int.Parse(inner[(split + 1)..]));
    }

    private static List<(int Start, int End, string Placeholder)> FindAllOccurrences(
        string text, Dictionary<string, string> placeholderByValue)
    {
        var spans = new List<(int Start, int End, string Placeholder)>();
        foreach (var (value, placeholder) in placeholderByValue)
        {
            // Whole-word match only, so "Mark" does not match inside "Marketing".
            var pattern = $@"(?<!\w){Regex.Escape(value)}(?!\w)";
            foreach (Match m in Regex.Matches(text, pattern))
            {
                spans.Add((m.Index, m.Index + m.Length, placeholder));
            }
        }

        // Overlaps: keep the earliest-starting span; on ties keep the longest.
        var kept = new List<(int Start, int End, string Placeholder)>();
        foreach (var s in spans.OrderBy(s => s.Start).ThenByDescending(s => s.End - s.Start))
        {
            if (kept.Count > 0 && s.Start < kept[^1].End) continue;
            kept.Add(s);
        }
        return kept;
    }

    private static string Replace(string text, List<(int Start, int End, string Placeholder)> spans)
    {
        var sb = new StringBuilder(text.Length);
        var pos = 0;
        foreach (var (start, end, placeholder) in spans)
        {
            sb.Append(text, pos, start - pos).Append(placeholder);
            pos = end;
        }
        sb.Append(text, pos, text.Length - pos);
        return sb.ToString();
    }
}