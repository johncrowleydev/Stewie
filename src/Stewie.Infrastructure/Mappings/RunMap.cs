using FluentNHibernate.Mapping;
using Stewie.Domain.Entities;
using Stewie.Domain.Enums;

namespace Stewie.Infrastructure.Mappings;

public class RunMap : ClassMap<Run>
{
    public RunMap()
    {
        Table("Runs");
        Id(x => x.Id).GeneratedBy.Assigned();
        Map(x => x.Status).CustomType<RunStatus>();
        Map(x => x.CreatedAt);
        Map(x => x.CompletedAt);
    }
}
