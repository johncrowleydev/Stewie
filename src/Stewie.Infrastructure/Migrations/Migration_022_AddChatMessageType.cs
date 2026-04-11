/// <summary>
/// Migration 022 — Add MessageType column to ChatMessages table.
/// Supports plan_proposal, plan_approved, plan_rejected message types
/// for the Architect Agent plan approval protocol.
/// REF: JOB-022 T-194
/// </summary>
using FluentMigrator;

namespace Stewie.Infrastructure.Migrations;

/// <summary>
/// Adds a nullable MessageType column to ChatMessages for distinguishing
/// plan proposals from regular chat messages. Backwards compatible —
/// existing rows will have MessageType = NULL (plain chat).
/// </summary>
[Migration(22)]
public class Migration_022_AddChatMessageType : Migration
{
    /// <summary>Add MessageType column.</summary>
    public override void Up()
    {
        Alter.Table("ChatMessages")
            .AddColumn("MessageType")
            .AsString(50)
            .Nullable();
    }

    /// <summary>Remove MessageType column.</summary>
    public override void Down()
    {
        Delete.Column("MessageType").FromTable("ChatMessages");
    }
}
