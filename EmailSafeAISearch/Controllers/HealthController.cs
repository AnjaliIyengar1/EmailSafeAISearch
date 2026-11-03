using Microsoft.AspNetCore.Mvc;

namespace EmailSafeAISearch.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<HealthResponse>(StatusCodes.Status200OK)]
    public ActionResult<HealthResponse> Get() => Ok(new HealthResponse("Healthy"));
}

/// <summary>Reports the API's health status.</summary>
public sealed record HealthResponse(string Status);
