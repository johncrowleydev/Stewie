/// <summary>
/// Migration 008: Adds new fields to Runs and Tasks tables for Phase 2 real repo interaction.
/// REF: CON-002 §5.2, §5.3
/// </summary>
using FluentMigrator;

namespace Stewie.Infrastructure.Migrations;

/// <summary>
/// Adds Branch/DiffSummary/CommitSha to Runs and Objective/Scope/ScriptJson/AcceptanceCriteriaJson to Tasks.
/// All columns are nullable for backward compatibility with existing data.
/// </summary>
[Migration(8)]
public class Migration_008_AddPhase2Fields : Migration
{
    /// <summary>Adds new columns to Runs and Tasks tables.</summary>
    public override void Up()
    {
        // Run fields for git tracking
        Alter.Table("Runs")
            .AddColumn("Branch").AsString(500).Nullable()
            .AddColumn("DiffSummary").AsString(4000).Nullable()
            .AddColumn("CommitSha").AsString(40).Nullable();

        // Task fields for real work
        Alter.Table("Tasks")
            .AddColumn("Objective").AsString(2000).Nullable()
            .AddColumn("Scope").AsString(2000).Nullable()
            .AddColumn("ScriptJson").AsString(4000).Nullable()
            .AddColumn("AcceptanceCriteriaJson").AsString(4000).Nullable();
    }

    /// <summary>Removes columns added in Up.</summary>
    public override void Down()
    {
        Delete.Column("Branch").FromTable("Runs");
        Delete.Column("DiffSummary").FromTable("Runs");
        Delete.Column("CommitSha").FromTable("Runs");

        Delete.Column("Objective").FromTable("Tasks");
        Delete.Column("Scope").FromTable("Tasks");
        Delete.Column("ScriptJson").FromTable("Tasks");
        Delete.Column("AcceptanceCriteriaJson").FromTable("Tasks");
    }
}
