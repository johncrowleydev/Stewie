using Microsoft.AspNetCore.Mvc;
using Stewie.Application.Interfaces;
using Stewie.Domain.Entities;
using Stewie.Domain.Enums;

namespace Stewie.Api.Controllers;

#if DEBUG
[ApiController]
[Route("api/dev")]
public class DevController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IJobRepository _jobRepository;
    private readonly IWorkTaskRepository _workTaskRepository;
    private readonly IEventRepository _eventRepository;
    private readonly IGovernanceReportRepository _governanceReportRepository;
    private readonly ITaskDependencyRepository _taskDependencyRepository;
    private readonly IChatMessageRepository _chatMessageRepository;
    private readonly IAgentSessionRepository _agentSessionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DevController(
        IUserRepository userRepository,
        IProjectRepository projectRepository,
        IJobRepository jobRepository,
        IWorkTaskRepository workTaskRepository,
        IEventRepository eventRepository,
        IGovernanceReportRepository governanceReportRepository,
        ITaskDependencyRepository taskDependencyRepository,
        IChatMessageRepository chatMessageRepository,
        IAgentSessionRepository agentSessionRepository,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _projectRepository = projectRepository;
        _jobRepository = jobRepository;
        _workTaskRepository = workTaskRepository;
        _eventRepository = eventRepository;
        _governanceReportRepository = governanceReportRepository;
        _taskDependencyRepository = taskDependencyRepository;
        _chatMessageRepository = chatMessageRepository;
        _agentSessionRepository = agentSessionRepository;
        _unitOfWork = unitOfWork;
    }

    [HttpPost("seed")]
    public async Task<IActionResult> Seed()
    {
        var admin = await _userRepository.GetByUsernameAsync("admin");
        if (admin == null) return BadRequest("Admin user not found, cannot seed.");

        _unitOfWork.BeginTransaction();

        // 1. Projects
        var p1 = new Project { Id = Guid.NewGuid(), Name = "Project Alpha", RepoUrl = "https://github.com/stewie/alpha", RepoProvider = "github", CreatedAt = DateTime.UtcNow.AddDays(-10) };
        var p2 = new Project { Id = Guid.NewGuid(), Name = "Core Services", RepoUrl = "https://github.com/stewie/core", RepoProvider = "github", CreatedAt = DateTime.UtcNow.AddDays(-5) };
        await _projectRepository.SaveAsync(p1);
        await _projectRepository.SaveAsync(p2);

        // 2. Jobs
        var j1 = new Job { Id = Guid.NewGuid(), ProjectId = p1.Id, CreatedByUserId = admin.Id, Branch = "feature/alpha-login", Status = JobStatus.Running, CreatedAt = DateTime.UtcNow.AddDays(-2) };
        var j2 = new Job { Id = Guid.NewGuid(), ProjectId = p2.Id, CreatedByUserId = admin.Id, Branch = "feature/core-auth", Status = JobStatus.Completed, CreatedAt = DateTime.UtcNow.AddDays(-4), CompletedAt = DateTime.UtcNow.AddDays(-1) };
        var j3 = new Job { Id = Guid.NewGuid(), ProjectId = p1.Id, CreatedByUserId = admin.Id, Branch = "feature/alpha-dashboard", Status = JobStatus.Failed, CreatedAt = DateTime.UtcNow.AddHours(-1) };
        var j4 = new Job { Id = Guid.NewGuid(), ProjectId = p2.Id, CreatedByUserId = admin.Id, Branch = "feature/core-metrics", Status = JobStatus.Pending, CreatedAt = DateTime.UtcNow };
        
        await _jobRepository.SaveAsync(j1);
        await _jobRepository.SaveAsync(j2);
        await _jobRepository.SaveAsync(j3);
        await _jobRepository.SaveAsync(j4);

        // 3. Agent Session
        var s1 = new AgentSession { Id = Guid.NewGuid(), ProjectId = p1.Id, TaskId = null, AgentRole = "Architect", RuntimeName = "stub", Status = AgentSessionStatus.Active, StartedAt = DateTime.UtcNow.AddHours(-2), ContainerId = "arch-abc" };
        var s2 = new AgentSession { Id = Guid.NewGuid(), ProjectId = p2.Id, TaskId = null, AgentRole = "Developer", RuntimeName = "stub", Status = AgentSessionStatus.Terminated, StartedAt = DateTime.UtcNow.AddDays(-3), StoppedAt = DateTime.UtcNow.AddDays(-1), ContainerId = "dev-xyz", StopReason = "Completed" };
        await _agentSessionRepository.SaveAsync(s1);
        await _agentSessionRepository.SaveAsync(s2);

        // 4. Tasks for j1
        var t1 = new WorkTask { Id = Guid.NewGuid(), JobId = j1.Id, Role = "Coder", Objective = "Update Auth Service", Scope = "Implement OAuth wrapper", Status = WorkTaskStatus.Running, WorkspacePath = "/tmp/ws1", CreatedAt = DateTime.UtcNow.AddHours(-1), StartedAt = DateTime.UtcNow.AddMinutes(-30) };
        var t2 = new WorkTask { Id = Guid.NewGuid(), JobId = j1.Id, Role = "Tester", Objective = "Verify Auth Tests", Scope = "Run oauth verification", Status = WorkTaskStatus.Pending, WorkspacePath = "/tmp/ws1", CreatedAt = DateTime.UtcNow.AddHours(-1) };
        await _workTaskRepository.SaveAsync(t1);
        await _workTaskRepository.SaveAsync(t2);

        var dep = new TaskDependency { Id = Guid.NewGuid(), TaskId = t2.Id, DependsOnTaskId = t1.Id, CreatedAt = DateTime.UtcNow };
        await _taskDependencyRepository.SaveAsync(dep);

        // 5. Governance Report for a completed task on j2
        var t3 = new WorkTask { Id = Guid.NewGuid(), JobId = j2.Id, Role = "Coder", Objective = "Create JWT Middleware", Scope = "Add middleware", Status = WorkTaskStatus.Completed, WorkspacePath = "/tmp/ws2", CreatedAt = DateTime.UtcNow.AddDays(-3), StartedAt = DateTime.UtcNow.AddDays(-3), CompletedAt = DateTime.UtcNow.AddDays(-2) };
        await _workTaskRepository.SaveAsync(t3);

        var gov = new GovernanceReport { Id = Guid.NewGuid(), TaskId = t3.Id, Passed = true, TotalChecks = 10, PassedChecks = 10, FailedChecks = 0, CheckResultsJson = "[]", CreatedAt = DateTime.UtcNow.AddDays(-2) };
        await _governanceReportRepository.SaveAsync(gov);

        // 6. Events (Job j1 context)
        var ev1 = new Event { Id = Guid.NewGuid(), EntityType = "Job", EntityId = j1.Id, EventType = EventType.JobStarted, Payload = "{\"from\":\"Pending\",\"to\":\"Active\"}", Timestamp = DateTime.UtcNow.AddHours(-2) };
        var ev2 = new Event { Id = Guid.NewGuid(), EntityType = "Task", EntityId = t1.Id, EventType = EventType.TaskStarted, Payload = "{\"containerId\":\"cont-1\"}", Timestamp = DateTime.UtcNow.AddMinutes(-30) };
        await _eventRepository.SaveAsync(ev1);
        await _eventRepository.SaveAsync(ev2);

        // 7. Chat messages
        var m1 = new ChatMessage { Id = Guid.NewGuid(), ProjectId = p1.Id, SenderRole = "Human", SenderName = "admin", Content = "Let's update the authentication service.", CreatedAt = DateTime.UtcNow.AddHours(-3) };
        var m2 = new ChatMessage { Id = Guid.NewGuid(), ProjectId = p1.Id, SenderRole = "Architect", SenderName = "Stewie", Content = "I have drafted 2 tasks for this job: Coder to implement it, Tester to verify. Proceed?", CreatedAt = DateTime.UtcNow.AddHours(-2).AddMinutes(30) };
        var m3 = new ChatMessage { Id = Guid.NewGuid(), ProjectId = p1.Id, SenderRole = "Human", SenderName = "admin", Content = "Yes, execute.", CreatedAt = DateTime.UtcNow.AddHours(-2) };
        var m4 = new ChatMessage { Id = Guid.NewGuid(), ProjectId = p1.Id, SenderRole = "Developer", SenderName = "Dev Agent A", Content = "I am modifying Auth.cs to include the new wrapper...", CreatedAt = DateTime.UtcNow.AddMinutes(-10) };
        await _chatMessageRepository.SaveAsync(m1);
        await _chatMessageRepository.SaveAsync(m2);
        await _chatMessageRepository.SaveAsync(m3);
        await _chatMessageRepository.SaveAsync(m4);

        await _unitOfWork.CommitAsync();

        return Ok("Seeded successfully");
    }
}
#endif
