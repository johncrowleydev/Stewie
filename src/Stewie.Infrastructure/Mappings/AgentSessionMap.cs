/// <summary>
/// NHibernate mapping for the AgentSession entity.
/// Maps to the "AgentSessions" table in SQL Server.
/// REF: JOB-017 T-163
/// </summary>
using FluentNHibernate.Mapping;
using Stewie.Domain.Entities;
using Stewie.Domain.Enums;

namespace Stewie.Infrastructure.Mappings;

/// <summary>
/// FluentNHibernate mapping configuration for <see cref="AgentSession"/>.
/// </summary>
public class AgentSessionMap : ClassMap<AgentSession>
{
    /// <summary>
    /// Initializes the AgentSession-to-AgentSessions table mapping.
    /// </summary>
    public AgentSessionMap()
    {
        Table("AgentSessions");
        Id(x => x.Id).GeneratedBy.Assigned();
        Map(x => x.ProjectId);
        Map(x => x.TaskId);
        Map(x => x.ContainerId).Length(100);
        Map(x => x.RuntimeName).Length(50);
        Map(x => x.AgentRole).Length(50);
        Map(x => x.Status).CustomType<AgentSessionStatus>();
        Map(x => x.StartedAt);
        Map(x => x.StoppedAt);
        Map(x => x.StopReason).Length(500);
    }
}
