using FluentMigrator;

namespace Stewie.Infrastructure.Migrations;

/// <summary>
/// Migration 011: Adds FailureReason column to Tasks table.
/// Stores the classified failure reason from the TaskFailureReason taxonomy.
/// REF: GOV-004, SPR-005 T-052
/// </summary>
[Migration(11)]
public class Migration_011_AddFailureReasonToTasks : Migration
{
    public override void Up()
    {
        Alter.Table("Tasks")
            .AddColumn("FailureReason").AsString(100).Nullable();
    }

    public override void Down()
    {
        Delete.Column("FailureReason").FromTable("Tasks");
    }
}
