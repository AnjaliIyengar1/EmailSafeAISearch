using System.Text.Json.Serialization;

namespace EmailSafeAISearch.Models;

// One PII item found by Presidio. Start/End are character positions in the text.
// Start and End give the position of the PII in the text, counted in characters from 0.
// End is the position just after the last character.
//
// Text:   H i _ M a r k , ...
// Index:  0 1 2 3 4 5 6 7
//               ^       ^
//           Start=3   End=7
//
// text.Substring(3, 7 - 3) returns "Mark".
// These positions can be used to replace "Mark" with "<PERSON_1>".

public sealed record PiiEntity(
    [property: JsonPropertyName("entity_type")] string EntityType,
    [property: JsonPropertyName("start")] int Start,
    [property: JsonPropertyName("end")] int End,
    [property: JsonPropertyName("score")] double Score);