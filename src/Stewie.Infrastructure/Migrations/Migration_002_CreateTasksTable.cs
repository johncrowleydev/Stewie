using FluentMigrator;

namespace Stewie.Infrastructure.Migrations;

[Migration(2)]
public class Migration_002_CreateTasksTable : Migration
{
    public override void Up()
    {
        Create.Table("Tasks")
            .WithColumn("Id").AsGuid().PrimaryKey()
            .WithColumn("RunId").AsGuid().NotNullable().ForeignKey("FK_Tasks_Runs", "Runs", "Id")
            .WithColumn("Role").AsString(100).NotNullable()
            .WithColumn("Status").AsInt32().NotNullable()
            .WithColumn("WorkspacePath").AsString(500).NotNullable()
            .WithColumn("CreatedAt").AsDateTime2().NotNullable()
            .WithColumn("StartedAt").AsDateTime2().Nullable()
            .WithColumn("CompletedAt").AsDateTime2().Nullable();
    }

    public override void Down()
    {
        Delete.Table("Tasks");
    }
}
