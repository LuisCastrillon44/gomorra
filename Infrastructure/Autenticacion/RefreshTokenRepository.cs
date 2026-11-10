using Application.Abstractions.Data;
using Application.Autenticacion;
using Dapper;
using Domain.Usuarios;

namespace Infrastructure.Autenticacion;

internal sealed class RefreshTokenRepository(IDbConnectionFactory connectionFactory) : IRefreshTokenRepository
{
    public async Task CrearAsync(long usuarioId, string tokenHash, DateTime expiraEn, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO gomorra.refresh_tokens (usuario_id, token_hash, expira_en)
            VALUES (@UsuarioId, @TokenHash, @ExpiraEn)
            """;

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            sql, new { UsuarioId = usuarioId, TokenHash = tokenHash, ExpiraEn = expiraEn }, cancellationToken: cancellationToken));
    }

    public async Task<RefreshToken?> ObtenerPorHashAsync(string tokenHash, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id, usuario_id, token_hash, expira_en, revocado_en, reemplazado_por, creado_en
            FROM gomorra.refresh_tokens
            WHERE token_hash = @TokenHash
            """;

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<RefreshToken>(
            new CommandDefinition(sql, new { TokenHash = tokenHash }, cancellationToken: cancellationToken));
    }

    public async Task<bool> RotarAsync(long idActual, long usuarioId, string nuevoHash, DateTime expiraEn, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        // Solo gana una petición: si el token ya estaba revocado no se actualiza ninguna fila.
        var revocados = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE gomorra.refresh_tokens
            SET revocado_en = now()
            WHERE id = @Id AND revocado_en IS NULL
            """,
            new { Id = idActual }, transaction, cancellationToken: cancellationToken));

        if (revocados == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        var nuevoId = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            INSERT INTO gomorra.refresh_tokens (usuario_id, token_hash, expira_en)
            VALUES (@UsuarioId, @TokenHash, @ExpiraEn)
            RETURNING id
            """,
            new { UsuarioId = usuarioId, TokenHash = nuevoHash, ExpiraEn = expiraEn }, transaction, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE gomorra.refresh_tokens SET reemplazado_por = @NuevoId WHERE id = @Id",
            new { Id = idActual, NuevoId = nuevoId }, transaction, cancellationToken: cancellationToken));

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task RevocarAsync(string tokenHash, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE gomorra.refresh_tokens
            SET revocado_en = now()
            WHERE token_hash = @TokenHash AND revocado_en IS NULL
            """;

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, new { TokenHash = tokenHash }, cancellationToken: cancellationToken));
    }

    public async Task RevocarTodosAsync(long usuarioId, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE gomorra.refresh_tokens
            SET revocado_en = now()
            WHERE usuario_id = @UsuarioId AND revocado_en IS NULL
            """;

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, new { UsuarioId = usuarioId }, cancellationToken: cancellationToken));
    }
}
