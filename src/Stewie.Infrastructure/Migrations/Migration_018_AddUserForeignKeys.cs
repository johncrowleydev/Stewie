using System.Data;
using FluentMigrator;

namespace Stewie.Infrastructure.Migrations;

/// <summary>
/// Migration 018: Adds missing User foreign keys to UserCredentials, InviteCodes, and Jobs.
/// Creates ProjectMemberships table to allow users to join/favorite projects.
/// </summary>
[Migration(18)]
public class Migration_018_AddUserForeignKeys : Migration
{
    public override void Up()
    {
        // 0. Clean up any orphaned records before applying constraints
        Execute.Sql("UPDATE Jobs SET CreatedByUserId = NULL WHERE CreatedByUserId IS NOT NULL AND CreatedByUserId NOT IN (SELECT Id FROM Users)");
        Execute.Sql("DELETE FROM InviteCodes WHERE CreatedByUserId NOT IN (SELECT Id FROM Users)");
        Execute.Sql("UPDATE InviteCodes SET UsedByUserId = NULL WHERE UsedByUserId IS NOT NULL AND UsedByUserId NOT IN (SELECT Id FROM Users)");
        Execute.Sql("DELETE FROM UserCredentials WHERE UserId NOT IN (SELECT Id FROM Users)");

        // 1. Add Foreign Keys for Jobs
        // SetNull is used so Jobs are not cascade deleted (per user request)
        Create.ForeignKey("FK_Jobs_Users_CreatedByUserId")
            .FromTable("Jobs").ForeignColumn("CreatedByUserId")
            .ToTable("Users").PrimaryColumn("Id")
            .OnDelete(Rule.SetNull);

        // 2. Add Foreign Keys for InviteCodes
        Create.ForeignKey("FK_InviteCodes_Users_CreatedByUserId")
            .FromTable("InviteCodes").ForeignColumn("CreatedByUserId")
            .ToTable("Users").PrimaryColumn("Id")
            .OnDelete(Rule.None); // Must be deleted or reassigned before user deletion

        Create.ForeignKey("FK_InviteCodes_Users_UsedByUserId")
            .FromTable("InviteCodes").ForeignColumn("UsedByUserId")
            .ToTable("Users").PrimaryColumn("Id")
            .OnDelete(Rule.SetNull);

        // 3. Add Foreign Keys for UserCredentials
        Create.ForeignKey("FK_UserCredentials_Users_UserId")
            .FromTable("UserCredentials").ForeignColumn("UserId")
            .ToTable("Users").PrimaryColumn("Id")
            .OnDelete(Rule.Cascade); // Credentials shouldn't survive user deletion

        // 4. Create ProjectMemberships table
        Create.Table("ProjectMemberships")
            .WithColumn("UserId").AsGuid().NotNullable()
            .WithColumn("ProjectId").AsGuid().NotNullable()
            .WithColumn("IsFavorite").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("JoinedAt").AsDateTime().NotNullable();

        Create.PrimaryKey("PK_ProjectMemberships")
            .OnTable("ProjectMemberships")
            .Columns("UserId", "ProjectId");

        Create.ForeignKey("FK_ProjectMemberships_Users_UserId")
            .FromTable("ProjectMemberships").ForeignColumn("UserId")
            .ToTable("Users").PrimaryColumn("Id")
            .OnDelete(Rule.Cascade); // Memberships should cascade delete

        Create.ForeignKey("FK_ProjectMemberships_Projects_ProjectId")
            .FromTable("ProjectMemberships").ForeignColumn("ProjectId")
            .ToTable("Projects").PrimaryColumn("Id")
            .OnDelete(Rule.Cascade);
    }

    public override void Down()
    {
        Delete.Table("ProjectMemberships");

        Delete.ForeignKey("FK_UserCredentials_Users_UserId").OnTable("UserCredentials");
        Delete.ForeignKey("FK_InviteCodes_Users_UsedByUserId").OnTable("InviteCodes");
        Delete.ForeignKey("FK_InviteCodes_Users_CreatedByUserId").OnTable("InviteCodes");
        Delete.ForeignKey("FK_Jobs_Users_CreatedByUserId").OnTable("Jobs");
    }
}
