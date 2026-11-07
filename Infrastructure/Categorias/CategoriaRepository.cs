using Application.Abstractions.Data;
using Application.Categorias;
using Dapper;
using Domain.Categorias;
using Infrastructure.Data;

namespace Infrastructure.Categorias;

internal sealed class CategoriaRepository(IDbConnectionFactory connectionFactory) : ICategoriaRepository
{
    private const string Columnas = "id, nombre, activo, creado_en, actualizado_en";

    private static readonly Dictionary<string, string> MensajesUnicos = new()
    {
        ["categorias_nombre_unico"] = "Ya existe una categoría con ese nombre.",
    };

    public async Task<IReadOnlyList<CategoriaDto>> ListarAsync(bool incluirInactivas, CancellationToken cancellationToken)
    {
        var sql = $"""
            SELECT {Columnas}
            FROM gomorra.categorias
            WHERE @IncluirInactivas OR activo
            ORDER BY lower(nombre)
            """;

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        var filas = await connection.QueryAsync<CategoriaDto>(
            new CommandDefinition(sql, new { IncluirInactivas = incluirInactivas }, cancellationToken: cancellationToken));
        return filas.AsList();
    }

    public async Task<CategoriaDto?> ObtenerAsync(long id, CancellationToken cancellationToken)
    {
        var sql = $"SELECT {Columnas} FROM gomorra.categorias WHERE id = @Id";

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<CategoriaDto>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public Task<long> CrearAsync(Categoria categoria, CancellationToken cancellationToken) =>
        PostgresErrores.TraducirUnicosAsync(async () =>
        {
            const string sql = """
                INSERT INTO gomorra.categorias (nombre, activo)
                VALUES (@Nombre, @Activo)
                RETURNING id
                """;

            await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
            return await connection.ExecuteScalarAsync<long>(
                new CommandDefinition(sql, categoria, cancellationToken: cancellationToken));
        }, MensajesUnicos);

    public Task<bool> ActualizarAsync(Categoria categoria, CancellationToken cancellationToken) =>
        PostgresErrores.TraducirUnicosAsync(async () =>
        {
            const string sql = """
                UPDATE gomorra.categorias
                SET nombre = @Nombre, activo = @Activo, actualizado_en = now()
                WHERE id = @Id
                """;

            await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
            return await connection.ExecuteAsync(
                new CommandDefinition(sql, categoria, cancellationToken: cancellationToken)) > 0;
        }, MensajesUnicos);

    public async Task<bool> DesactivarAsync(long id, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE gomorra.categorias
            SET activo = false, actualizado_en = now()
            WHERE id = @Id
            """;

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        return await connection.ExecuteAsync(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken)) > 0;
    }

    public async Task<bool> TieneProductosActivosAsync(long id, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1 FROM gomorra.productos WHERE categoria_id = @Id AND activo
            )
            """;

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }
}
