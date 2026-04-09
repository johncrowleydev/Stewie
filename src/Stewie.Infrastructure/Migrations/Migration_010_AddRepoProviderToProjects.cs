using FluentMigrator;

namespace Stewie.Infrastructure.Migrations;

/// <summary>
/// Migration 010: Adds RepoProvider to Projects table and makes RepoUrl nullable.
/// RepoUrl is nullable to support create-mode where the repo doesn't exist yet.
/// REF: CON-002 §5.1 (v1.4.0), SPR-005 T-049
/// </summary>
[Migration(10)]
public class Migration_010_AddRepoProviderToProjects : Migration
{
    public override void Up()
    {
        Alter.Table("Projects")
            .AddColumn("RepoProvider").AsString(100).Nullable();

        // Make RepoUrl nullable to support create-mode (repo provisioned after project creation)
        Alter.Column("RepoUrl").OnTable("Projects").AsString(500).Nullable();
    }

    public override void Down()
    {
        Delete.Column("RepoProvider").FromTable("Projects");

        // Restore NOT NULL — set any nulls to empty string first
        Execute.Sql("UPDATE Projects SET RepoUrl = '' WHERE RepoUrl IS NULL");
        Alter.Column("RepoUrl").OnTable("Projects").AsString(500).NotNullable();
    }
}
