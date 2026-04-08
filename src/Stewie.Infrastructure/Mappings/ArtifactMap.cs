using FluentNHibernate.Mapping;
using Stewie.Domain.Entities;

namespace Stewie.Infrastructure.Mappings;

public class ArtifactMap : ClassMap<Artifact>
{
    public ArtifactMap()
    {
        Table("Artifacts");
        Id(x => x.Id).GeneratedBy.Assigned();
        Map(x => x.TaskId);
        References(x => x.WorkTask).Column("TaskId").ReadOnly();
        Map(x => x.Type);
        Map(x => x.ContentJson).Length(int.MaxValue);
        Map(x => x.CreatedAt);
    }
}
