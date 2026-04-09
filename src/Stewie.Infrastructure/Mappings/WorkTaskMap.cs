/// <summary>
/// NHibernate mapping for the WorkTask entity.
/// Maps to the "Tasks" table in SQL Server.
/// REF: BLU-001 §6, CON-002 §5.3
/// </summary>
using FluentNHibernate.Mapping;
using Stewie.Domain.Entities;
using Stewie.Domain.Enums;

namespace Stewie.Infrastructure.Mappings;

/// <summary>
/// FluentNHibernate mapping configuration for <see cref="WorkTask"/>.
/// </summary>
public class WorkTaskMap : ClassMap<WorkTask>
{
    /// <summary>
    /// Initializes the WorkTask-to-Tasks table mapping.
    /// Objective, Scope, ScriptJson, AcceptanceCriteriaJson are Phase 2 fields — all nullable.
    /// </summary>
    public WorkTaskMap()
    {
        Table("Tasks");
        Id(x => x.Id).GeneratedBy.Assigned();
        Map(x => x.JobId);
        References(x => x.Job).Column("JobId").ReadOnly();
        Map(x => x.Role);
        Map(x => x.Status).CustomType<WorkTaskStatus>();
        Map(x => x.Objective).Length(2000);
        Map(x => x.Scope).Length(2000);
        Map(x => x.ScriptJson).Length(4000);
        Map(x => x.AcceptanceCriteriaJson).Length(4000);
        Map(x => x.WorkspacePath);
        Map(x => x.CreatedAt);
        Map(x => x.StartedAt);
        Map(x => x.FailureReason);
        Map(x => x.CompletedAt);
    }
}
