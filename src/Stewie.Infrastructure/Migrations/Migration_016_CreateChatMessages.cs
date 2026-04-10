using FluentMigrator;

namespace Stewie.Infrastructure.Migrations;

/// <summary>
/// Migration 016: Creates the ChatMessages table for project-scoped chat.
/// REF: JOB-013 T-131
/// </summary>
[Migration(16)]
public class Migration_016_CreateChatMessages : Migration
{
    public override void Up()
    {
        Create.Table("ChatMessages")
            .WithColumn("Id").AsGuid().PrimaryKey()
            .WithColumn("ProjectId").AsGuid().NotNullable()
            .WithColumn("SenderRole").AsString(50).NotNullable()
            .WithColumn("SenderName").AsString(100).NotNullable()
            .WithColumn("Content").AsString(int.MaxValue).NotNullable()
            .WithColumn("CreatedAt").AsDateTime2().NotNullable();

        Create.Index("IX_ChatMessages_ProjectId")
            .OnTable("ChatMessages")
            .OnColumn("ProjectId").Ascending();

        Create.Index("IX_ChatMessages_CreatedAt")
            .OnTable("ChatMessages")
            .OnColumn("CreatedAt").Ascending();
    }

    public override void Down()
    {
        Delete.Index("IX_ChatMessages_CreatedAt").OnTable("ChatMessages");
        Delete.Index("IX_ChatMessages_ProjectId").OnTable("ChatMessages");
        Delete.Table("ChatMessages");
    }
}
