using Domain.Usuarios;

namespace Application.Usuarios;

public interface IUsuarioRepository
{
    Task<IReadOnlyList<UsuarioDto>> ListarAsync(bool incluirInactivos, CancellationToken cancellationToken);
    Task<UsuarioDto?> ObtenerAsync(long id, CancellationToken cancellationToken);

    /// <summary>Usuario completo (con hash de clave) para autenticar.</summary>
    Task<Usuario?> ObtenerEntidadAsync(long id, CancellationToken cancellationToken);
    Task<Usuario?> ObtenerPorNombreUsuarioAsync(string usuario, CancellationToken cancellationToken);

    Task<long> CrearAsync(Usuario usuario, CancellationToken cancellationToken);
    Task<bool> ActualizarAsync(Usuario usuario, CancellationToken cancellationToken);
    Task<bool> CambiarClaveAsync(long id, string claveHash, CancellationToken cancellationToken);
    Task<bool> DesactivarAsync(long id, CancellationToken cancellationToken);
    Task RegistrarAccesoAsync(long id, CancellationToken cancellationToken);
}
