using FluentMigrator;

namespace Stewie.Infrastructure.Migrations;

/// <summary>
/// Migration 012: Renames Run → Job across the database schema.
/// - Renames table Runs → Jobs
/// - Renames column Tasks.RunId → Tasks.JobId
/// - Renames column Jobs.CreatedByUserId stays (no change needed — user FK)
/// Uses sp_rename for SQL Server to preserve data and constraints.
/// REF: JOB-006 T-057, CON-002 v1.5.0
/// </summary>
[Migration(12)]
public class Migration_012_RenameRunsToJobs : Migration
{
    public override void Up()
    {
        // Rename table: Runs → Jobs
        Execute.Sql("EXEC sp_rename 'Runs', 'Jobs';");

        // Rename column: Tasks.RunId → Tasks.JobId
        Execute.Sql("EXEC sp_rename 'Tasks.RunId', 'JobId', 'COLUMN';");
    }

    public override void Down()
    {
        // Reverse: Jobs → Runs
        Execute.Sql("EXEC sp_rename 'Jobs', 'Runs';");

        // Reverse: Tasks.JobId → Tasks.RunId
        Execute.Sql("EXEC sp_rename 'Tasks.JobId', 'RunId', 'COLUMN';");
    }
}
