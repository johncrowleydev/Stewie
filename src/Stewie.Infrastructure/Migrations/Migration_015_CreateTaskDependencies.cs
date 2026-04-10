using FluentMigrator;

namespace Stewie.Infrastructure.Migrations;

/// <summary>
/// Migration 015: Creates the TaskDependencies table.
/// Models directed edges in the task dependency DAG. Each row represents
/// "TaskId depends on DependsOnTaskId" — the downstream task cannot execute
/// until the upstream task completes.
/// REF: JOB-009 T-081
/// </summary>
[Migration(15)]
public class Migration_015_CreateTaskDependencies : Migration
{
    public override void Up()
    {
        Create.Table("TaskDependencies")
            .WithColumn("Id").AsGuid().PrimaryKey()
            .WithColumn("TaskId").AsGuid().NotNullable()
            .WithColumn("DependsOnTaskId").AsGuid().NotNullable()
            .WithColumn("CreatedAt").AsDateTime().NotNullable();

        Create.ForeignKey("FK_TaskDependencies_TaskId")
            .FromTable("TaskDependencies").ForeignColumn("TaskId")
            .ToTable("Tasks").PrimaryColumn("Id");

        Create.ForeignKey("FK_TaskDependencies_DependsOnTaskId")
            .FromTable("TaskDependencies").ForeignColumn("DependsOnTaskId")
            .ToTable("Tasks").PrimaryColumn("Id");

        Create.UniqueConstraint("UQ_TaskDependencies_TaskId_DependsOnTaskId")
            .OnTable("TaskDependencies")
            .Columns("TaskId", "DependsOnTaskId");
    }

    public override void Down()
    {
        Delete.UniqueConstraint("UQ_TaskDependencies_TaskId_DependsOnTaskId").FromTable("TaskDependencies");
        Delete.ForeignKey("FK_TaskDependencies_DependsOnTaskId").OnTable("TaskDependencies");
        Delete.ForeignKey("FK_TaskDependencies_TaskId").OnTable("TaskDependencies");
        Delete.Table("TaskDependencies");
    }
}
