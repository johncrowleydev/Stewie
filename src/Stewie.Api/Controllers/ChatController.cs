/// <summary>
/// Chat API controller — project-scoped messaging for Human↔Architect communication.
/// REF: JOB-013 T-133, JOB-018 T-170, CON-002 v2.0.0
/// </summary>
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stewie.Application.Interfaces;
using Stewie.Domain.Entities;
using Stewie.Domain.Messaging;
using Stewie.Application.Services;

namespace Stewie.Api.Controllers;

/// <summary>
/// Exposes REST endpoints for project chat: retrieving message history
/// and sending new messages with real-time SignalR push.
/// When the project has an active Architect agent, Human messages are also
/// relayed to the Architect's RabbitMQ queue (best-effort — failures logged, never fail the HTTP request).
/// </summary>
[ApiController]
[Route("api/projects/{projectId}/chat")]
[Authorize]
public class ChatController : ControllerBase
{
    private readonly IChatMessageRepository _chatRepo;
    private readonly IProjectRepository _projectRepo;
    private readonly IRealTimeNotifier _notifier;
    private readonly IRabbitMqService _rabbitMq;
    private readonly IAgentSessionRepository _sessionRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly AgentLifecycleService _lifecycle;
    private readonly ILogger<ChatController> _logger;

    /// <summary>Initializes the chat controller with required dependencies.</summary>
    public ChatController(
        IChatMessageRepository chatRepo,
        IProjectRepository projectRepo,
        IRealTimeNotifier notifier,
        IRabbitMqService rabbitMq,
        IAgentSessionRepository sessionRepo,
        IUnitOfWork unitOfWork,
        AgentLifecycleService lifecycle,
        ILogger<ChatController> logger)
    {
        _chatRepo = chatRepo;
        _projectRepo = projectRepo;
        _notifier = notifier;
        _rabbitMq = rabbitMq;
        _sessionRepo = sessionRepo;
        _unitOfWork = unitOfWork;
        _lifecycle = lifecycle;
        _logger = logger;
    }

    /// <summary>Get chat history for a project (paginated, oldest-first).</summary>
    /// <param name="projectId">The project to retrieve messages for.</param>
    /// <param name="limit">Max messages to return (default 100).</param>
    /// <param name="offset">Number of messages to skip (default 0).</param>
    /// <returns>Paginated message list with total count.</returns>
    [HttpGet]
    public async Task<IActionResult> GetMessages(
        Guid projectId,
        [FromQuery] int limit = 100,
        [FromQuery] int offset = 0)
    {
        // Clamp limit to prevent abuse
        limit = Math.Clamp(limit, 1, 500);
        offset = Math.Max(offset, 0);

        var project = await _projectRepo.GetByIdAsync(projectId);
        if (project is null)
            return NotFound(new { error = $"Project '{projectId}' not found." });

        var messages = await _chatRepo.GetByProjectIdAsync(projectId, limit, offset);
        var total = await _chatRepo.GetCountByProjectIdAsync(projectId);

        return Ok(new
        {
            messages = messages.Select(m => new
            {
                id = m.Id,
                projectId = m.ProjectId,
                senderRole = m.SenderRole,
                senderName = m.SenderName,
                content = m.Content,
                createdAt = m.CreatedAt.ToString("O")
            }),
            total,
            limit,
            offset
        });
    }

    /// <summary>Send a new chat message to a project.</summary>
    /// <param name="projectId">The project to send the message to.</param>
    /// <param name="request">Message content.</param>
    /// <returns>201 Created with the persisted message.</returns>
    [HttpPost]
    public async Task<IActionResult> SendMessage(
        Guid projectId,
        [FromBody] SendChatMessageRequest request)
    {
        // Validate content
        if (request is null || string.IsNullOrWhiteSpace(request.Content))
            return BadRequest(new { error = "Message content is required." });

        if (request.Content.Length > 10000)
            return BadRequest(new { error = "Message content must not exceed 10000 characters." });

        // Validate project exists
        var project = await _projectRepo.GetByIdAsync(projectId);
        if (project is null)
            return NotFound(new { error = $"Project '{projectId}' not found." });

        var architectSession = await _lifecycle.GetActiveArchitectAsync(projectId);
        if (architectSession is null)
        {
            return StatusCode(409, new { error = "Architect is offline. Please start a new session." });
        }

        // Extract sender from JWT claims
        var senderName = User.FindFirst("username")?.Value ?? "unknown";

        var message = new ChatMessage
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            SenderRole = "Human",
            SenderName = senderName,
            Content = request.Content.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _unitOfWork.BeginTransaction();
        await _chatRepo.SaveAsync(message);
        await _unitOfWork.CommitAsync();

        _logger.LogInformation("Chat message {MessageId} sent to project {ProjectId} by {Sender}",
            message.Id, projectId, senderName);

        // Push real-time notification — fire-and-forget safe (notifier swallows exceptions)
        await _notifier.NotifyChatMessageAsync(
            projectId, message.Id, message.SenderRole,
            message.SenderName, message.Content, message.CreatedAt);

        // Best-effort relay to Architect Agent via RabbitMQ (T-170)
        // If there's no active Architect session, this is a no-op.
        // If RabbitMQ publish fails, log at Warning and continue — the HTTP request never fails.
        await RelayChatToArchitectAsync(projectId, message);

        return StatusCode(201, new
        {
            id = message.Id,
            projectId = message.ProjectId,
            senderRole = message.SenderRole,
            senderName = message.SenderName,
            content = message.Content,
            createdAt = message.CreatedAt.ToString("O")
        });
    }

    /// <summary>
    /// Relays a Human chat message to the active Architect agent via RabbitMQ.
    /// This is best-effort: RabbitMQ failures are logged and swallowed.
    /// </summary>
    internal async Task RelayChatToArchitectAsync(Guid projectId, ChatMessage message)
    {
        try
        {
            var architectSession = await _sessionRepo.GetActiveByProjectAndRoleAsync(projectId, "architect");
            if (architectSession is null)
            {
                _logger.LogDebug("No active Architect session for project {ProjectId} — skipping chat relay", projectId);
                return;
            }

            var agentMessage = new AgentMessage
            {
                Type = "chat.human_message",
                AgentId = architectSession.Id.ToString(),
                RoutingKey = $"architect.{projectId}",
                Payload = System.Text.Json.JsonSerializer.SerializeToElement(new {
                    projectId = projectId.ToString(),
                    userId = Guid.Empty.ToString(),
                    username = message.SenderName,
                    content = message.Content,
                    chatMessageId = message.Id.ToString()
                }),
                Timestamp = message.CreatedAt,
                CorrelationId = message.Id.ToString()
            };

            await _rabbitMq.PublishChatAsync($"architect.{projectId}", agentMessage);

            _logger.LogInformation(
                "Chat message {MessageId} relayed to Architect session {SessionId} via RabbitMQ",
                message.Id, architectSession.Id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to relay chat message {MessageId} to Architect for project {ProjectId} — best-effort, continuing",
                message.Id, projectId);
        }
    }

    /// <summary>
    /// Submit a plan decision (approve/reject) — relayed to the Architect via RabbitMQ.
    /// REF: JOB-022 T-194, CON-004 command.plan_decision
    /// </summary>
    /// <param name="projectId">The project whose Architect will receive the decision.</param>
    /// <param name="request">Plan decision details.</param>
    /// <returns>200 OK on success, 404 if no active Architect.</returns>
    [HttpPost("plan-decision")]
    public async Task<IActionResult> PlanDecision(
        Guid projectId,
        [FromBody] PlanDecisionRequest request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.PlanId))
            return BadRequest(new { error = "PlanId is required." });

        var validDecisions = new[] { "approved", "rejected" };
        if (!validDecisions.Contains(request.Decision?.ToLowerInvariant()))
            return BadRequest(new { error = "Decision must be 'approved' or 'rejected'." });

        var architectSession = await _lifecycle.GetActiveArchitectAsync(projectId);
        if (architectSession is null)
            return NotFound(new { error = "No active Architect session for this project." });

        // Persist decision as a chat message for audit trail
        var senderName = User.FindFirst("username")?.Value ?? "unknown";
        var decisionLabel = request.Decision!.ToLowerInvariant() == "approved" ? "approved" : "rejected";
        var messageType = decisionLabel == "approved" ? "plan_approved" : "plan_rejected";

        var chatMsg = new ChatMessage
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            SenderRole = "Human",
            SenderName = senderName,
            Content = $"Plan {request.PlanId} {decisionLabel}." + (string.IsNullOrEmpty(request.Feedback) ? "" : $" Feedback: {request.Feedback}"),
            MessageType = messageType,
            CreatedAt = DateTime.UtcNow
        };

        _unitOfWork.BeginTransaction();
        await _chatRepo.SaveAsync(chatMsg);
        await _unitOfWork.CommitAsync();

        // Push to dashboard
        await _notifier.NotifyChatMessageAsync(
            projectId, chatMsg.Id, chatMsg.SenderRole,
            chatMsg.SenderName, chatMsg.Content, chatMsg.CreatedAt);

        // Publish command.plan_decision to the Architect's command queue via RabbitMQ
        try
        {
            var agentMessage = new AgentMessage
            {
                Type = "command.plan_decision",
                AgentId = architectSession.Id.ToString(),
                RoutingKey = $"agent.{architectSession.Id}",
                Payload = System.Text.Json.JsonSerializer.SerializeToElement(new
                {
                    planId = request.PlanId,
                    decision = decisionLabel,
                    feedback = request.Feedback ?? ""
                }),
                Timestamp = DateTime.UtcNow,
                CorrelationId = chatMsg.Id.ToString()
            };

            await _rabbitMq.PublishCommandAsync($"agent.{architectSession.Id}", agentMessage);

            _logger.LogInformation(
                "Plan decision '{Decision}' for plan {PlanId} sent to Architect {SessionId}",
                decisionLabel, request.PlanId, architectSession.Id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to relay plan decision to Architect for project {ProjectId}",
                projectId);
        }

        return Ok(new
        {
            planId = request.PlanId,
            decision = decisionLabel,
            chatMessageId = chatMsg.Id
        });
    }
}

/// <summary>Request body for sending a chat message.</summary>
public class SendChatMessageRequest
{
    /// <summary>Message text content. Required. Max 10000 characters.</summary>
    public string Content { get; set; } = string.Empty;
}

/// <summary>Request body for plan approval/rejection decisions. REF: JOB-022 T-194.</summary>
public class PlanDecisionRequest
{
    /// <summary>ID of the plan being decided on. Required.</summary>
    public string PlanId { get; set; } = string.Empty;

    /// <summary>Decision: "approved" or "rejected". Required.</summary>
    public string Decision { get; set; } = string.Empty;

    /// <summary>Optional human feedback text.</summary>
    public string? Feedback { get; set; }
}
