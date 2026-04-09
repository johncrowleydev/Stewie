/// <summary>
/// Migration 004: Creates the Projects table.
/// REF: BLU-001 §3.2, CON-002 §5.1
/// </summary>
using FluentMigrator;

namespace Stewie.Infrastructure.Migrations;

/// <summary>
/// Creates the Projects table for grouping runs under a repository context.
/// </summary>
[Migration(4)]
public class Migration_004_CreateProjectsTable : Migration
{
    /// <summary>
    /// Creates the Projects table with Id, Name, RepoUrl, and CreatedAt columns.
    /// </summary>
    public override void Up()
    {
        Create.Table("Projects")
            .WithColumn("Id").AsGuid().PrimaryKey()
            .WithColumn("Name").AsString(255).NotNullable()
            .WithColumn("RepoUrl").AsString(2048).NotNullable()
            .WithColumn("CreatedAt").AsDateTime2().NotNullable();
    }

    /// <summary>Drops the Projects table.</summary>
    public override void Down()
    {
        Delete.Table("Projects");
    }
}
