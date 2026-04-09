using FluentMigrator;

namespace Stewie.Infrastructure.Migrations;

/// <summary>
/// Migration 013: Adds task chaining fields to the Tasks table.
/// - ParentTaskId: FK to self (tester task → dev task)
/// - AttemptNumber: retry iteration counter
/// - GovernanceViolationsJson: violation feedback for retry dev tasks
/// REF: JOB-007 T-066
/// </summary>
[Migration(13)]
public class Migration_013_AddTaskChainFields : Migration
{
    public override void Up()
    {
        Alter.Table("Tasks")
            .AddColumn("ParentTaskId").AsGuid().Nullable()
            .AddColumn("AttemptNumber").AsInt32().NotNullable().WithDefaultValue(1)
            .AddColumn("GovernanceViolationsJson").AsString(int.MaxValue).Nullable();

        // Self-referential FK: Tasks.ParentTaskId → Tasks.Id
        Create.ForeignKey("FK_Tasks_ParentTaskId")
            .FromTable("Tasks").ForeignColumn("ParentTaskId")
            .ToTable("Tasks").PrimaryColumn("Id");
    }

    public override void Down()
    {
        Delete.ForeignKey("FK_Tasks_ParentTaskId").OnTable("Tasks");
        Delete.Column("ParentTaskId").FromTable("Tasks");
        Delete.Column("AttemptNumber").FromTable("Tasks");
        Delete.Column("GovernanceViolationsJson").FromTable("Tasks");
    }
}
