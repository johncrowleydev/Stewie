/// <summary>
/// Health check endpoint — returns system status, version, and timestamp.
/// No authentication required per CON-002 §4.4.
/// REF: CON-002 §4.4, §5.4
/// </summary>
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Stewie.Api.Controllers;

/// <summary>
/// Provides a lightweight health check endpoint for monitoring and load balancer probes.
/// </summary>
[ApiController]
[AllowAnonymous]
public class HealthController : ControllerBase
{
    private readonly ILogger<HealthController> _logger;

    /// <summary>Assembly version, computed once at startup for performance.</summary>
    private static readonly string AssemblyVersion =
        Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
        ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
        ?? "unknown";

    /// <summary>
    /// Initializes the health controller.
    /// </summary>
    /// <param name="logger">Structured logger for health check requests.</param>
    public HealthController(ILogger<HealthController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Returns a health check response with system status, version, and current timestamp.
    /// No authentication required — suitable for load balancer and monitoring probe consumption.
    /// </summary>
    /// <returns>200 OK with health status JSON per CON-002 §5.4.</returns>
    [HttpGet("/health")]
    public IActionResult GetHealth()
    {
        _logger.LogDebug("Health check requested");

        return Ok(new
        {
            status = "healthy",
            version = AssemblyVersion,
            timestamp = DateTime.UtcNow.ToString("o")
        });
    }
}
