/// <summary>
/// NHibernate mapping for the Project entity.
/// Maps to the "Projects" table in SQL Server.
/// REF: BLU-001 §6 (persistence strategy)
/// </summary>
using FluentNHibernate.Mapping;
using Stewie.Domain.Entities;

namespace Stewie.Infrastructure.Mappings;

/// <summary>
/// FluentNHibernate mapping configuration for <see cref="Project"/>.
/// </summary>
public class ProjectMap : ClassMap<Project>
{
    /// <summary>
    /// Initializes the Project-to-Projects table mapping.
    /// DECISION: Id is Assigned (not auto-generated) to match the existing
    /// pattern where the orchestrator assigns GUIDs before persistence.
    /// </summary>
    public ProjectMap()
    {
        Table("Projects");
        Id(x => x.Id).GeneratedBy.Assigned();
        Map(x => x.Name).Not.Nullable();
        Map(x => x.RepoUrl).Not.Nullable();
        Map(x => x.CreatedAt).Not.Nullable();
    }
}
