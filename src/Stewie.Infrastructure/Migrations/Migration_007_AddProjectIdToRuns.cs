/// <summary>
/// Migration 007: Adds nullable ProjectId foreign key to the Runs table.
/// REF: CON-002 §5.2 — runs can optionally be associated with a project.
/// </summary>
using FluentMigrator;

namespace Stewie.Infrastructure.Migrations;

/// <summary>
/// Adds a nullable ProjectId column to Runs and creates a foreign key to Projects.
/// Existing runs will have NULL ProjectId (backward compatible).
/// </summary>
[Migration(7)]
public class Migration_007_AddProjectIdToRuns : Migration
{
    /// <summary>
    /// Adds the nullable ProjectId column and FK constraint.
    /// </summary>
    public override void Up()
    {
        Alter.Table("Runs")
            .AddColumn("ProjectId").AsGuid().Nullable();

        Create.ForeignKey("FK_Runs_ProjectId")
            .FromTable("Runs").ForeignColumn("ProjectId")
            .ToTable("Projects").PrimaryColumn("Id");
    }

    /// <summary>Removes the ProjectId column and FK.</summary>
    public override void Down()
    {
        Delete.ForeignKey("FK_Runs_ProjectId").OnTable("Runs");
        Delete.Column("ProjectId").FromTable("Runs");
    }
}
