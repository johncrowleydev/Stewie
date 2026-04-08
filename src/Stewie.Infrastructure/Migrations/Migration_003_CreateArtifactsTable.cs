using FluentMigrator;

namespace Stewie.Infrastructure.Migrations;

[Migration(3)]
public class Migration_003_CreateArtifactsTable : Migration
{
    public override void Up()
    {
        Create.Table("Artifacts")
            .WithColumn("Id").AsGuid().PrimaryKey()
            .WithColumn("TaskId").AsGuid().NotNullable().ForeignKey("FK_Artifacts_Tasks", "Tasks", "Id")
            .WithColumn("Type").AsString(100).NotNullable()
            .WithColumn("ContentJson").AsString(int.MaxValue).NotNullable()
            .WithColumn("CreatedAt").AsDateTime2().NotNullable();
    }

    public override void Down()
    {
        Delete.Table("Artifacts");
    }
}
