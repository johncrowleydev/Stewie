/// <summary>
/// NHibernate mapping for the Job entity.
/// Maps to the "Jobs" table in SQL Server.
/// REF: BLU-001 §6, CON-002 §5.2
/// </summary>
using FluentNHibernate.Mapping;
using Stewie.Domain.Entities;
using Stewie.Domain.Enums;

namespace Stewie.Infrastructure.Mappings;

/// <summary>
/// FluentNHibernate mapping configuration for <see cref="Job"/>.
/// </summary>
public class JobMap : ClassMap<Job>
{
    /// <summary>
    /// Initializes the Job-to-Jobs table mapping.
    /// ProjectId is nullable to support standalone jobs (backward compatible).
    /// Branch, DiffSummary, CommitSha are Phase 2 fields — all nullable.
    /// </summary>
    public JobMap()
    {
        Table("Jobs");
        Id(x => x.Id).GeneratedBy.Assigned();
        Map(x => x.ProjectId);
        References(x => x.Project).Column("ProjectId").ReadOnly();
        Map(x => x.Status).CustomType<JobStatus>();
        Map(x => x.Branch);
        Map(x => x.DiffSummary).Length(4000);
        Map(x => x.CommitSha);
        Map(x => x.PullRequestUrl).Length(1000);
        Map(x => x.CreatedByUserId);
        Map(x => x.CreatedAt);
        Map(x => x.CompletedAt);
    }
}
