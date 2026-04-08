using Microsoft.Data.SqlClient;

namespace Stewie.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    public static void EnsureDatabaseExists(string connectionString)
    {
        var builder = new SqlConnectionStringBuilder(connectionString);
        var databaseName = builder.InitialCatalog;
        builder.InitialCatalog = "master";

        using var connection = new SqlConnection(builder.ConnectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = $"""
            IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = @dbName)
            BEGIN
                CREATE DATABASE [{databaseName}]
            END
            """;
        command.Parameters.AddWithValue("@dbName", databaseName);
        command.ExecuteNonQuery();
    }
}
