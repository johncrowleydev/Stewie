/// <summary>
/// NHibernate mapping for the GovernanceReport entity.
/// Maps to the "GovernanceReports" table in SQL Server.
/// REF: JOB-007 T-067
/// </summary>
using FluentNHibernate.Mapping;
using Stewie.Domain.Entities;

namespace Stewie.Infrastructure.Mappings;

/// <summary>
/// FluentNHibernate mapping configuration for <see cref="GovernanceReport"/>.
/// </summary>
public class GovernanceReportMap : ClassMap<GovernanceReport>
{
    /// <summary>
    /// Initializes the GovernanceReport-to-GovernanceReports table mapping.
    /// </summary>
    public GovernanceReportMap()
    {
        Table("GovernanceReports");
        Id(x => x.Id).GeneratedBy.Assigned();
        Map(x => x.TaskId);
        Map(x => x.Passed);
        Map(x => x.TotalChecks);
        Map(x => x.PassedChecks);
        Map(x => x.FailedChecks);
        Map(x => x.CheckResultsJson).Length(4001);
        Map(x => x.CreatedAt);
    }
}
