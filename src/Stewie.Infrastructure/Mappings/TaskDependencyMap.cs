/// <summary>
/// NHibernate mapping for the TaskDependency entity.
/// Maps to the "TaskDependencies" table in SQL Server.
/// REF: JOB-009 T-081
/// </summary>
using FluentNHibernate.Mapping;
using Stewie.Domain.Entities;

namespace Stewie.Infrastructure.Mappings;

/// <summary>
/// FluentNHibernate mapping configuration for <see cref="TaskDependency"/>.
/// </summary>
public class TaskDependencyMap : ClassMap<TaskDependency>
{
    /// <summary>
    /// Initializes the TaskDependency-to-TaskDependencies table mapping.
    /// </summary>
    public TaskDependencyMap()
    {
        Table("TaskDependencies");
        Id(x => x.Id).GeneratedBy.Assigned();
        Map(x => x.TaskId);
        Map(x => x.DependsOnTaskId);
        Map(x => x.CreatedAt);
    }
}
