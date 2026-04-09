using FluentMigrator;

namespace Stewie.Infrastructure.Migrations;

/// <summary>
/// Migration 014: Creates the GovernanceReports table.
/// Stores governance check results produced by tester worker containers.
/// REF: JOB-007 T-067, CON-001 §6
/// </summary>
[Migration(14)]
public class Migration_014_CreateGovernanceReports : Migration
{
    public override void Up()
    {
        Create.Table("GovernanceReports")
            .WithColumn("Id").AsGuid().PrimaryKey()
            .WithColumn("TaskId").AsGuid().NotNullable()
            .WithColumn("Passed").AsBoolean().NotNullable()
            .WithColumn("TotalChecks").AsInt32().NotNullable()
            .WithColumn("PassedChecks").AsInt32().NotNullable()
            .WithColumn("FailedChecks").AsInt32().NotNullable()
            .WithColumn("CheckResultsJson").AsString(int.MaxValue).NotNullable()
            .WithColumn("CreatedAt").AsDateTime().NotNullable();

        Create.ForeignKey("FK_GovernanceReports_TaskId")
            .FromTable("GovernanceReports").ForeignColumn("TaskId")
            .ToTable("Tasks").PrimaryColumn("Id");
    }

    public override void Down()
    {
        Delete.ForeignKey("FK_GovernanceReports_TaskId").OnTable("GovernanceReports");
        Delete.Table("GovernanceReports");
    }
}
