using FluentMigrator;

namespace Stewie.Infrastructure.Migrations;

/// <summary>
/// Migration 017: Creates the AgentSessions table for tracking agent container lifecycles.
/// REF: JOB-017 T-163
/// </summary>
[Migration(17)]
public class Migration_017_CreateAgentSessions : Migration
{
    public override void Up()
    {
        Create.Table("AgentSessions")
            .WithColumn("Id").AsGuid().PrimaryKey()
            .WithColumn("ProjectId").AsGuid().NotNullable()
            .WithColumn("TaskId").AsGuid().Nullable()
            .WithColumn("ContainerId").AsString(100).Nullable()
            .WithColumn("RuntimeName").AsString(50).NotNullable()
            .WithColumn("AgentRole").AsString(50).NotNullable()
            .WithColumn("Status").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("StartedAt").AsDateTime2().NotNullable()
            .WithColumn("StoppedAt").AsDateTime2().Nullable()
            .WithColumn("StopReason").AsString(500).Nullable();

        Create.Index("IX_AgentSessions_ProjectId")
            .OnTable("AgentSessions")
            .OnColumn("ProjectId").Ascending();

        Create.Index("IX_AgentSessions_Status")
            .OnTable("AgentSessions")
            .OnColumn("Status").Ascending();

        Create.Index("IX_AgentSessions_ProjectId_AgentRole_Status")
            .OnTable("AgentSessions")
            .OnColumn("ProjectId").Ascending()
            .OnColumn("AgentRole").Ascending()
            .OnColumn("Status").Ascending();
    }

    public override void Down()
    {
        Delete.Index("IX_AgentSessions_ProjectId_AgentRole_Status").OnTable("AgentSessions");
        Delete.Index("IX_AgentSessions_Status").OnTable("AgentSessions");
        Delete.Index("IX_AgentSessions_ProjectId").OnTable("AgentSessions");
        Delete.Table("AgentSessions");
    }
}
