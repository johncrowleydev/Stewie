using Microsoft.AspNetCore.Mvc;
using Stewie.Application.Services;

namespace Stewie.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class RunsController : ControllerBase
{
    private readonly RunOrchestrationService _orchestrationService;
    private readonly ILogger<RunsController> _logger;

    public RunsController(RunOrchestrationService orchestrationService, ILogger<RunsController> logger)
    {
        _orchestrationService = orchestrationService;
        _logger = logger;
    }

    [HttpPost("test")]
    public async Task<IActionResult> TriggerTestRun()
    {
        _logger.LogInformation("Test run triggered");
        var result = await _orchestrationService.ExecuteTestRunAsync();
        _logger.LogInformation("Test run completed: {Status}", result.Status);
        return Ok(result);
    }
}
