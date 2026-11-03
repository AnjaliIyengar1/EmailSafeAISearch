using System.Text.Json.Serialization;

namespace EmailSafeAISearch.Models;

/// <summary>One email from data/emails.json (id and raw text only).</summary>
public sealed record EmailRecord(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("raw_email")] string RawEmail);