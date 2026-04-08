using FluentMigrator;

namespace Stewie.Infrastructure.Migrations;

[Migration(1)]
public class Migration_001_CreateRunsTable : Migration
{
    public override void Up()
    {
        Create.Table("Runs")
            .WithColumn("Id").AsGuid().PrimaryKey()
            .WithColumn("Status").AsInt32().NotNullable()
            .WithColumn("CreatedAt").AsDateTime2().NotNullable()
            .WithColumn("CompletedAt").AsDateTime2().Nullable();
    }

    public override void Down()
    {
        Delete.Table("Runs");
    }
}
