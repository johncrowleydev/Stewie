/// <summary>
/// Migration 005: Creates the Events table for the audit trail.
/// REF: BLU-001 §3.2
/// </summary>
using FluentMigrator;

namespace Stewie.Infrastructure.Migrations;

/// <summary>
/// Creates the Events table to store immutable audit trail records.
/// </summary>
[Migration(5)]
public class Migration_005_CreateEventsTable : Migration
{
    /// <summary>
    /// Creates the Events table with entity tracking and event classification columns.
    /// </summary>
    public override void Up()
    {
        Create.Table("Events")
            .WithColumn("Id").AsGuid().PrimaryKey()
            .WithColumn("EntityType").AsString(100).NotNullable()
            .WithColumn("EntityId").AsGuid().NotNullable()
            .WithColumn("EventType").AsInt32().NotNullable()
            .WithColumn("Payload").AsString(4000).Nullable()
            .WithColumn("Timestamp").AsDateTime2().NotNullable();

        // Index for querying events by entity
        Create.Index("IX_Events_EntityType_EntityId")
            .OnTable("Events")
            .OnColumn("EntityType").Ascending()
            .OnColumn("EntityId").Ascending();
    }

    /// <summary>Drops the Events table.</summary>
    public override void Down()
    {
        Delete.Table("Events");
    }
}
