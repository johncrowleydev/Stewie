using FluentNHibernate.Mapping;
using Stewie.Domain.Entities;
using Stewie.Domain.Enums;

namespace Stewie.Infrastructure.Mappings;

public class WorkTaskMap : ClassMap<WorkTask>
{
    public WorkTaskMap()
    {
        Table("Tasks");
        Id(x => x.Id).GeneratedBy.Assigned();
        Map(x => x.RunId);
        References(x => x.Run).Column("RunId").ReadOnly();
        Map(x => x.Role);
        Map(x => x.Status).CustomType<WorkTaskStatus>();
        Map(x => x.WorkspacePath);
        Map(x => x.CreatedAt);
        Map(x => x.StartedAt);
        Map(x => x.CompletedAt);
    }
}
