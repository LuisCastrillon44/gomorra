using DbUp;
using Npgsql;

namespace Infrastructure.Database;

public static class DatabaseMigrator
{
    public const string Schema = "gomorra";

    /// <summary>
    /// Aplica los scripts pendientes de Database/Scripts en orden. Cada script se ejecuta una sola vez
    /// y queda registrado en gomorra.schema_versions.
    /// </summary>
    public static void Migrate(string connectionString)
    {
        // El journal de DbUp vive en el esquema propio, así que debe existir antes de ejecutar los scripts.
        using (var dataSource = NpgsqlDataSource.Create(connectionString))
        using (var command = dataSource.CreateCommand($"CREATE SCHEMA IF NOT EXISTS {Schema};"))
        {
            command.ExecuteNonQuery();
        }

        var upgrader = DeployChanges.To
            .PostgresqlDatabase(connectionString, Schema)
            .WithScriptsEmbeddedInAssembly(typeof(DatabaseMigrator).Assembly)
            .JournalToPostgresqlTable(Schema, "schema_versions")
            .WithTransactionPerScript()
            .LogToConsole()
            .Build();

        var result = upgrader.PerformUpgrade();
        if (!result.Successful)
        {
            throw new InvalidOperationException(
                $"Falló la migración de la base de datos en el script '{result.ErrorScript?.Name}'.",
                result.Error);
        }
    }
}
