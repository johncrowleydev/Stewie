/// <summary>
/// Project entity — groups Runs under a repository/project context.
/// Used by: RunOrchestrationService, ProjectsController.
/// REF: CON-002 §5.1, BLU-001 §3.2
/// </summary>
namespace Stewie.Domain.Entities;

/// <summary>
/// Represents a project that groups related runs together.
/// A project maps to a source code repository or logical work unit.
/// </summary>
public class Project
{
    /// <summary>Unique identifier for the project.</summary>
    public virtual Guid Id { get; set; }

    /// <summary>Human-readable project name.</summary>
    public virtual string Name { get; set; } = string.Empty;

    /// <summary>Repository URL associated with this project.</summary>
    public virtual string RepoUrl { get; set; } = string.Empty;

    /// <summary>Timestamp when the project was created.</summary>
    public virtual DateTime CreatedAt { get; set; }
}
