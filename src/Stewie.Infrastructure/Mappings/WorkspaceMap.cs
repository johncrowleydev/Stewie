/// <summary>
/// NHibernate mapping for the Workspace entity.
/// Maps to the "Workspaces" table in SQL Server.
/// REF: BLU-001 §6
/// </summary>
using FluentNHibernate.Mapping;
using Stewie.Domain.Entities;
using Stewie.Domain.Enums;

namespace Stewie.Infrastructure.Mappings;

/// <summary>
/// FluentNHibernate mapping configuration for <see cref="Workspace"/>.
/// </summary>
public class WorkspaceMap : ClassMap<Workspace>
{
    /// <summary>
    /// Initializes the Workspace-to-Workspaces table mapping.
    /// WorkspaceStatus is stored as an integer via CustomType.
    /// </summary>
    public WorkspaceMap()
    {
        Table("Workspaces");
        Id(x => x.Id).GeneratedBy.Assigned();
        Map(x => x.TaskId).Not.Nullable();
        Map(x => x.Path).Not.Nullable();
        Map(x => x.Status).CustomType<WorkspaceStatus>().Not.Nullable();
        Map(x => x.CreatedAt).Not.Nullable();
        Map(x => x.MountedAt);
        Map(x => x.CleanedAt);
    }
}
