using Application.Abstractions.Data;
using Application.Usuarios;
using Dapper;
using Domain.Usuarios;
using Infrastructure.Data;

namespace Infrastructure.Usuarios;

internal sealed class UsuarioRepository(IDbConnectionFactory connectionFactory) : IUsuarioRepository
{
    private const string ColumnasDto = "id, usuario, email, nombre, activo, ultimo_acceso, creado_en, actualizado_en";
    // La columna "usuario" se llama NombreUsuario en la entidad (una propiedad no puede llamarse como su clase).
    private const string ColumnasEntidad =
        "id, usuario AS nombre_usuario, email, nombre, clave_hash, activo, ultimo_acceso, creado_en, actualizado_en";

    private static readonly Dictionary<string, string> MensajesUnicos = new()
    {
        ["usuarios_usuario_unico"] = "Ya existe un usuario con ese nombre de usuario.",
        ["usuarios_email_unico"] = "Ya existe un usuario con ese correo.",
    };

    public async Task<IReadOnlyList<UsuarioDto>> ListarAsync(bool incluirInactivos, CancellationToken cancellationToken)
    {
        var sql = $"""
            SELECT {ColumnasDto}
            FROM gomorra.usuarios
            WHERE @IncluirInactivos OR activo
            ORDER BY lower(nombre), lower(usuario)
            """;

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        var filas = await connection.QueryAsync<UsuarioDto>(
            new CommandDefinition(sql, new { IncluirInactivos = incluirInactivos }, cancellationToken: cancellationToken));
        return filas.AsList();
    }

    public async Task<UsuarioDto?> ObtenerAsync(long id, CancellationToken cancellationToken)
    {
        var sql = $"SELECT {ColumnasDto} FROM gomorra.usuarios WHERE id = @Id";

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<UsuarioDto>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<Usuario?> ObtenerEntidadAsync(long id, CancellationToken cancellationToken)
    {
        var sql = $"SELECT {ColumnasEntidad} FROM gomorra.usuarios WHERE id = @Id";

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<Usuario>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<Usuario?> ObtenerPorNombreUsuarioAsync(string usuario, CancellationToken cancellationToken)
    {
        var sql = $"SELECT {ColumnasEntidad} FROM gomorra.usuarios WHERE lower(usuario) = lower(@Usuario)";

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<Usuario>(
            new CommandDefinition(sql, new { Usuario = usuario }, cancellationToken: cancellationToken));
    }

    public Task<long> CrearAsync(Usuario usuario, CancellationToken cancellationToken) =>
        PostgresErrores.TraducirUnicosAsync(async () =>
        {
            const string sql = """
                INSERT INTO gomorra.usuarios (usuario, email, nombre, clave_hash, activo)
                VALUES (@NombreUsuario, @Email, @Nombre, @ClaveHash, @Activo)
                RETURNING id
                """;

            await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
            return await connection.ExecuteScalarAsync<long>(
                new CommandDefinition(sql, usuario, cancellationToken: cancellationToken));
        }, MensajesUnicos);

    public Task<bool> ActualizarAsync(Usuario usuario, CancellationToken cancellationToken) =>
        PostgresErrores.TraducirUnicosAsync(async () =>
        {
            const string sql = """
                UPDATE gomorra.usuarios
                SET usuario = @NombreUsuario, email = @Email, nombre = @Nombre, activo = @Activo, actualizado_en = now()
                WHERE id = @Id
                """;

            await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
            return await connection.ExecuteAsync(
                new CommandDefinition(sql, usuario, cancellationToken: cancellationToken)) > 0;
        }, MensajesUnicos);

    public async Task<bool> CambiarClaveAsync(long id, string claveHash, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE gomorra.usuarios
            SET clave_hash = @ClaveHash, actualizado_en = now()
            WHERE id = @Id
            """;

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        return await connection.ExecuteAsync(
            new CommandDefinition(sql, new { Id = id, ClaveHash = claveHash }, cancellationToken: cancellationToken)) > 0;
    }

    public async Task<bool> DesactivarAsync(long id, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE gomorra.usuarios
            SET activo = false, actualizado_en = now()
            WHERE id = @Id
            """;

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        return await connection.ExecuteAsync(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken)) > 0;
    }

    public async Task RegistrarAccesoAsync(long id, CancellationToken cancellationToken)
    {
        const string sql = "UPDATE gomorra.usuarios SET ultimo_acceso = now() WHERE id = @Id";

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }
}
