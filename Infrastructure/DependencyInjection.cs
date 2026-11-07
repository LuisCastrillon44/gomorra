using Application.Abstractions.Data;
using Application.Caja;
using Application.Categorias;
using Application.Inventario;
using Application.Productos;
using Dapper;
using Infrastructure.Caja;
using Infrastructure.Categorias;
using Infrastructure.Data;
using Infrastructure.Inventario;
using Infrastructure.Productos;
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

        services.AddScoped<ICategoriaRepository, CategoriaRepository>();
        services.AddScoped<IProductoRepository, ProductoRepository>();
        services.AddScoped<IInventarioRepository, InventarioRepository>();
        services.AddScoped<ICajaRepository, CajaRepository>();

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
                "Configúrala con la variable de entorno " +
                $"'ConnectionStrings__{ConnectionStringName}' (en local: variable de usuario de Windows; en Render: Environment).");
        }

        return connectionString;
    }
}
