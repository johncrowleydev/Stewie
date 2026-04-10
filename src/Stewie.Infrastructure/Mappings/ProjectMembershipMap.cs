using FluentNHibernate.Mapping;
using Stewie.Domain.Entities;

namespace Stewie.Infrastructure.Mappings;

/// <summary>
/// FluentNHibernate mapping configuration for <see cref="ProjectMembership"/>.
/// </summary>
public class ProjectMembershipMap : ClassMap<ProjectMembership>
{
    public ProjectMembershipMap()
    {
        Table("ProjectMemberships");
        CompositeId()
            .KeyProperty(x => x.UserId, "UserId")
            .KeyProperty(x => x.ProjectId, "ProjectId");

        Map(x => x.IsFavorite).Not.Nullable();
        Map(x => x.JoinedAt).Not.Nullable();
    }
}
