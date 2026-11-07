using Application.Abstractions.Data;
using Dapper;
using Infrastructure.Data;
using Infrastructure.Database;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "Gomorra";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = GetConnectionString(configuration);

        // Columnas snake_case de Postgres -> propiedades PascalCase.
        DefaultTypeMap.MatchNamesWithUnderscores = true;

        services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));
        services.AddSingleton<IDbConnectionFactory, NpgsqlConnectionFactory>();

        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("database");

        return services;
    }

    public static string GetConnectionString(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Falta la cadena de conexión 'ConnectionStrings:{ConnectionStringName}'. " +
                "En desarrollo configúrala con 'dotnet user-secrets' en el proyecto Api; " +
                $"en producción con la variable de entorno 'ConnectionStrings__{ConnectionStringName}'.");
        }

        return connectionString;
    }
}
