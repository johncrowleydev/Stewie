using FluentMigrator;

namespace Stewie.Infrastructure.Migrations;

/// <summary>
/// Migration 009: Creates Users, InviteCodes, UserCredentials tables.
/// Adds CreatedByUserId and PullRequestUrl to Runs.
/// REF: CON-002 §4.0, §4.0.1, §4.0.2, §5.2
/// </summary>
[Migration(9)]
public class Migration_009_AddUserSystem : Migration
{
    public override void Up()
    {
        Create.Table("Users")
            .WithColumn("Id").AsGuid().PrimaryKey()
            .WithColumn("Username").AsString(255).NotNullable().Unique()
            .WithColumn("PasswordHash").AsString(500).NotNullable()
            .WithColumn("Role").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("CreatedAt").AsDateTime().NotNullable();

        Create.Table("InviteCodes")
            .WithColumn("Id").AsGuid().PrimaryKey()
            .WithColumn("Code").AsString(100).NotNullable().Unique()
            .WithColumn("CreatedByUserId").AsGuid().NotNullable()
            .WithColumn("UsedByUserId").AsGuid().Nullable()
            .WithColumn("UsedAt").AsDateTime().Nullable()
            .WithColumn("ExpiresAt").AsDateTime().Nullable()
            .WithColumn("CreatedAt").AsDateTime().NotNullable();

        Create.Table("UserCredentials")
            .WithColumn("Id").AsGuid().PrimaryKey()
            .WithColumn("UserId").AsGuid().NotNullable()
            .WithColumn("Provider").AsString(100).NotNullable()
            .WithColumn("EncryptedToken").AsString(4000).NotNullable()
            .WithColumn("CreatedAt").AsDateTime().NotNullable()
            .WithColumn("UpdatedAt").AsDateTime().NotNullable();

        Alter.Table("Runs")
            .AddColumn("CreatedByUserId").AsGuid().Nullable()
            .AddColumn("PullRequestUrl").AsString(1000).Nullable();
    }

    public override void Down()
    {
        Delete.Column("CreatedByUserId").FromTable("Runs");
        Delete.Column("PullRequestUrl").FromTable("Runs");
        Delete.Table("UserCredentials");
        Delete.Table("InviteCodes");
        Delete.Table("Users");
    }
}
