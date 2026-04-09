/// <summary>
/// Migration 006: Creates the Workspaces table for workspace lifecycle tracking.
/// REF: BLU-001 §3.2
/// </summary>
using FluentMigrator;

namespace Stewie.Infrastructure.Migrations;

/// <summary>
/// Creates the Workspaces table to track task workspace lifecycle.
/// </summary>
[Migration(6)]
public class Migration_006_CreateWorkspacesTable : Migration
{
    /// <summary>
    /// Creates the Workspaces table with lifecycle timestamp columns.
    /// </summary>
    public override void Up()
    {
        Create.Table("Workspaces")
            .WithColumn("Id").AsGuid().PrimaryKey()
            .WithColumn("TaskId").AsGuid().NotNullable()
            .WithColumn("Path").AsString(500).NotNullable()
            .WithColumn("Status").AsInt32().NotNullable()
            .WithColumn("CreatedAt").AsDateTime2().NotNullable()
            .WithColumn("MountedAt").AsDateTime2().Nullable()
            .WithColumn("CleanedAt").AsDateTime2().Nullable();

        // Foreign key to Tasks table
        Create.ForeignKey("FK_Workspaces_TaskId")
            .FromTable("Workspaces").ForeignColumn("TaskId")
            .ToTable("Tasks").PrimaryColumn("Id");
    }

    /// <summary>Drops the Workspaces table.</summary>
    public override void Down()
    {
        Delete.Table("Workspaces");
    }
}
