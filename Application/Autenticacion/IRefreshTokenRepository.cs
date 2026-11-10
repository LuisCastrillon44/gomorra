using Domain.Usuarios;

namespace Application.Autenticacion;

public interface IRefreshTokenRepository
{
    Task CrearAsync(long usuarioId, string tokenHash, DateTime expiraEn, CancellationToken cancellationToken);
    Task<RefreshToken?> ObtenerPorHashAsync(string tokenHash, CancellationToken cancellationToken);

    /// <summary>
    /// En una transacción revoca el token actual y crea el nuevo enlazado en reemplazado_por.
    /// Devuelve false si el token actual ya estaba revocado (otro refresh lo usó primero).
    /// </summary>
    Task<bool> RotarAsync(long idActual, long usuarioId, string nuevoHash, DateTime expiraEn, CancellationToken cancellationToken);

    Task RevocarAsync(string tokenHash, CancellationToken cancellationToken);
    Task RevocarTodosAsync(long usuarioId, CancellationToken cancellationToken);
}
