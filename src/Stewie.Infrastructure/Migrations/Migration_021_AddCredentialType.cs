using FluentMigrator;

namespace Stewie.Infrastructure.Migrations;

/// <summary>
/// Migration 021: Adds CredentialType column to UserCredentials table.
/// Default 0 = GitHubPat, preserving backward compatibility for existing rows.
/// REF: JOB-021 T-183
/// </summary>
[Migration(21)]
public class Migration_021_AddCredentialType : Migration
{
    public override void Up()
    {
        Alter.Table("UserCredentials")
            .AddColumn("CredentialType")
            .AsInt32()
            .NotNullable()
            .WithDefaultValue(0);
    }

    public override void Down()
    {
        Delete.Column("CredentialType").FromTable("UserCredentials");
    }
}
