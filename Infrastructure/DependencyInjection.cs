using System.Text;
using Application.Abstractions.Data;
using Application.Abstractions.Seguridad;
using Application.Autenticacion;
using Application.Caja;
using Application.Usuarios;
using Infrastructure.Autenticacion;
using Infrastructure.Seguridad;
using Infrastructure.Usuarios;
using Application.Categorias;
using Application.Cuentas;
using Application.Inventario;
using Application.Productos;
using Dapper;
using Infrastructure.Caja;
using Infrastructure.Categorias;
using Infrastructure.Cuentas;
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
        services.AddScoped<ICuentaRepository, CuentaRepository>();
        services.AddScoped<IUsuarioRepository, UsuarioRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();

        services.AddSingleton(GetJwtOptions(configuration));
        services.AddSingleton(GetAdminOptions(configuration));
        services.AddSingleton<IHasherClaves, HasherClavesBcrypt>();
        services.AddSingleton<IGeneradorTokens, GeneradorTokensJwt>();

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

    /// <summary>
    /// Lee la sección "Jwt". La clave de firma ('Jwt:Clave') es secreta: en local va en user-secrets del
    /// proyecto Api y en Render en la variable de entorno 'Jwt__Clave'; nunca en appsettings.
    /// Sin una clave de al menos 32 bytes la API no arranca.
    /// </summary>
    public static JwtOptions GetJwtOptions(IConfiguration configuration)
    {
        var seccion = configuration.GetSection("Jwt");
        var defecto = new JwtOptions();

        var opciones = new JwtOptions
        {
            Emisor = seccion["Emisor"] ?? defecto.Emisor,
            Audiencia = seccion["Audiencia"] ?? defecto.Audiencia,
            Clave = seccion["Clave"] ?? string.Empty,
            MinutosAcceso = int.TryParse(seccion["MinutosAcceso"], out var minutos) ? minutos : defecto.MinutosAcceso,
            DiasRefresh = int.TryParse(seccion["DiasRefresh"], out var dias) ? dias : defecto.DiasRefresh,
        };

        if (Encoding.UTF8.GetByteCount(opciones.Clave) < 32)
        {
            throw new InvalidOperationException(
                "Falta 'Jwt:Clave' o tiene menos de 32 bytes. En local: " +
                "'dotnet user-secrets set Jwt:Clave <valor> --project Api'; en Render: variable de entorno 'Jwt__Clave'.");
        }

        return opciones;
    }

    /// <summary>
    /// Lee la sección "Admin" (Usuario, Clave, Nombre): en local en user-secrets, en Render como
    /// variables de entorno Admin__Usuario, Admin__Clave, Admin__Nombre.
    /// </summary>
    public static AdminOptions GetAdminOptions(IConfiguration configuration)
    {
        var seccion = configuration.GetSection("Admin");
        return new AdminOptions
        {
            Usuario = seccion["Usuario"],
            Clave = seccion["Clave"],
            Nombre = string.IsNullOrWhiteSpace(seccion["Nombre"]) ? new AdminOptions().Nombre : seccion["Nombre"]!,
        };
    }
}
