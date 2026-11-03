using EmailSafeAISearch.Services;
using Microsoft.AspNetCore.Mvc;

namespace EmailSafeAISearch.Controllers;

[ApiController]
[Route("api/[controller]")]
/// <summary>Main endpoint: POST api/ask with a question; returns the answer and every stage of processing.</summary>
public class AskController(IAskService ask) : ControllerBase
{
    public sealed record AskRequest(string Question);

    // POST api/ask
    [HttpPost]
    public async Task<IActionResult> Ask(AskRequest request, CancellationToken ct)
        => Ok(await ask.AskAsync(request.Question, ct));
}