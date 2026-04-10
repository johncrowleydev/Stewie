/// <summary>
/// StewieHub — SignalR hub for real-time push notifications.
/// REF: JOB-012 T-121
/// </summary>
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Stewie.Application.Hubs;

/// <summary>
/// Central SignalR hub for Stewie real-time communication.
/// Clients join groups to receive scoped notifications:
/// - "dashboard" — all job state changes
/// - "project:{guid}" — chat messages + job updates for a project
/// - "job:{guid}" — task updates + container output for a specific job
/// </summary>
[Authorize]
public class StewieHub : Hub<IStewieHubClient>
{
    /// <summary>Join the dashboard group to receive all job state changes.</summary>
    public async Task JoinDashboard()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "dashboard");
    }

    /// <summary>Leave the dashboard group.</summary>
    public async Task LeaveDashboard()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "dashboard");
    }

    /// <summary>Join a project group to receive chat messages and job updates.</summary>
    public async Task JoinProject(Guid projectId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"project:{projectId}");
    }

    /// <summary>Leave a project group.</summary>
    public async Task LeaveProject(Guid projectId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"project:{projectId}");
    }

    /// <summary>Join a job group to receive task updates and container output.</summary>
    public async Task JoinJob(Guid jobId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"job:{jobId}");
    }

    /// <summary>Leave a job group.</summary>
    public async Task LeaveJob(Guid jobId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"job:{jobId}");
    }
}
