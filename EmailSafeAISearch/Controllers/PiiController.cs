using EmailSafeAISearch.Services;
using Microsoft.AspNetCore.Mvc;

namespace EmailSafeAISearch.Controllers;

/// <summary>Test endpoints for PII: detect, hide, and restore.</summary>
[ApiController]
[Route("api/[controller]")]
public class PiiController(IPiiDetector detector, IPiiService pii) : ControllerBase
{
    public sealed record TextRequest(string Text);
    public sealed record RestoreRequest(string Text, Dictionary<string, string> Map);

    // POST api/pii/detect
    [HttpPost("detect")]
    public async Task<IActionResult> Detect(TextRequest request, CancellationToken ct)
        => Ok(await detector.DetectAsync(request.Text, ct));

    // POST api/pii/hide
    [HttpPost("hide")]
    public async Task<IActionResult> Hide(TextRequest request, CancellationToken ct)
        => Ok(await pii.HideAsync(request.Text, ct));

    // POST api/pii/restore
    [HttpPost("restore")]
    public IActionResult Restore(RestoreRequest request)
        => Ok(pii.Restore(request.Text, request.Map));
}