/// <summary>
/// NHibernate mapping for the Run entity.
/// Maps to the "Runs" table in SQL Server.
/// REF: BLU-001 §6
/// </summary>
using FluentNHibernate.Mapping;
using Stewie.Domain.Entities;
using Stewie.Domain.Enums;

namespace Stewie.Infrastructure.Mappings;

/// <summary>
/// FluentNHibernate mapping configuration for <see cref="Run"/>.
/// </summary>
public class RunMap : ClassMap<Run>
{
    /// <summary>
    /// Initializes the Run-to-Runs table mapping.
    /// ProjectId is nullable to support standalone runs (backward compatible).
    /// </summary>
    public RunMap()
    {
        Table("Runs");
        Id(x => x.Id).GeneratedBy.Assigned();
        Map(x => x.ProjectId);
        References(x => x.Project).Column("ProjectId").ReadOnly();
        Map(x => x.Status).CustomType<RunStatus>();
        Map(x => x.CreatedAt);
        Map(x => x.CompletedAt);
    }
}
