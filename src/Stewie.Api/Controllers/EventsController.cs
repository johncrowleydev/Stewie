/// <summary>
/// Events API controller — endpoints for querying audit trail events.
/// REF: CON-002 §4.5, §5.5
///
/// READING GUIDE FOR INCIDENT RESPONDERS:
/// 1. If events list is empty     → check event emission in RunOrchestrationService
/// 2. If filtering returns wrong  → check entityType/entityId query param binding
/// 3. If limit ignored            → check GetRecentAsync clamping logic
/// </summary>
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stewie.Application.Interfaces;

namespace Stewie.Api.Controllers;

/// <summary>
/// Exposes read-only endpoints for querying audit trail events.
/// Events are emitted by the orchestration service, not created via API.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class EventsController : ControllerBase
{
    private readonly IEventRepository _eventRepository;
    private readonly ILogger<EventsController> _logger;

    /// <summary>Initializes the EventsController with required dependencies.</summary>
    public EventsController(
        IEventRepository eventRepository,
        ILogger<EventsController> logger)
    {
        _eventRepository = eventRepository;
        _logger = logger;
    }

    /// <summary>
    /// Lists events with optional filtering by entity type and entity ID.
    /// Returns most recent events first. Default limit 100, max 500.
    /// </summary>
    /// <param name="entityType">Optional: filter by entity type (e.g. "Run", "Task").</param>
    /// <param name="entityId">Optional: filter by entity ID (requires entityType).</param>
    /// <param name="limit">Optional: max results (default 100, max 500).</param>
    /// <returns>200 OK with array of event objects per CON-002 §5.5.</returns>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? entityType = null,
        [FromQuery] Guid? entityId = null,
        [FromQuery] int limit = 100)
    {
        _logger.LogInformation(
            "Listing events: entityType={EntityType}, entityId={EntityId}, limit={Limit}",
            entityType, entityId, limit);

        IList<Domain.Entities.Event> events;

        if (!string.IsNullOrWhiteSpace(entityType) && entityId.HasValue)
        {
            // Filter by specific entity
            events = await _eventRepository.GetByEntityAsync(entityType, entityId.Value);
        }
        else
        {
            // Return recent events with limit
            events = await _eventRepository.GetRecentAsync(limit);
        }

        var response = events.Select(e => new
        {
            id = e.Id,
            entityType = e.EntityType,
            entityId = e.EntityId,
            eventType = e.EventType.ToString(),
            payload = e.Payload,
            timestamp = e.Timestamp.ToString("o")
        });

        return Ok(response);
    }
}
