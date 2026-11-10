using Application.Abstractions.Seguridad;
using Application.Autenticacion;
using Application.Common;
using Domain.Usuarios;

namespace Application.Usuarios;

public sealed class UsuarioService(
    IUsuarioRepository repositorio,
    IRefreshTokenRepository refreshTokens,
    IHasherClaves hasher,
    AdminOptions admin)
{
    private const string AdminProtegido =
        "El administrador principal no se puede desactivar ni cambiar de usuario.";

    public async Task<IReadOnlyList<UsuarioDto>> ListarAsync(bool incluirInactivos, CancellationToken cancellationToken)
    {
        var lista = await repositorio.ListarAsync(incluirInactivos, cancellationToken);
        foreach (var usuario in lista)
        {
            usuario.EsAdministrador = admin.EsAdministrador(usuario.Usuario);
        }

        return lista;
    }

    public async Task<UsuarioDto> ObtenerAsync(long id, CancellationToken cancellationToken)
    {
        var usuario = await repositorio.ObtenerAsync(id, cancellationToken) ?? throw NoEncontrado(id);
        usuario.EsAdministrador = admin.EsAdministrador(usuario.Usuario);
        return usuario;
    }

    public async Task<UsuarioDto> CrearAsync(CrearUsuarioRequest request, CancellationToken cancellationToken)
    {
        var id = await repositorio.CrearAsync(
            new Usuario
            {
                NombreUsuario = NormalizarUsuario(request.Usuario),
                Email = NormalizarEmail(request.Email),
                Nombre = request.Nombre.Trim(),
                ClaveHash = hasher.Hash(request.Clave),
            },
            cancellationToken);

        return await ObtenerAsync(id, cancellationToken);
    }

    public async Task<UsuarioDto> ActualizarAsync(long id, ActualizarUsuarioRequest request, CancellationToken cancellationToken)
    {
        var actual = await repositorio.ObtenerAsync(id, cancellationToken) ?? throw NoEncontrado(id);
        var nombreUsuario = NormalizarUsuario(request.Usuario);

        if (admin.EsAdministrador(actual.Usuario) && (!request.Activo || !admin.EsAdministrador(nombreUsuario)))
        {
            throw new ConflictoException(AdminProtegido);
        }

        await repositorio.ActualizarAsync(
            new Usuario
            {
                Id = id,
                NombreUsuario = nombreUsuario,
                Email = NormalizarEmail(request.Email),
                Nombre = request.Nombre.Trim(),
                Activo = request.Activo,
            },
            cancellationToken);

        if (!request.Activo)
        {
            await refreshTokens.RevocarTodosAsync(id, cancellationToken);
        }

        return await ObtenerAsync(id, cancellationToken);
    }

    /// <summary>Cambia la clave y cierra todas las sesiones abiertas del usuario.</summary>
    public async Task CambiarClaveAsync(long id, CambiarClaveRequest request, CancellationToken cancellationToken)
    {
        if (!await repositorio.CambiarClaveAsync(id, hasher.Hash(request.Clave), cancellationToken))
        {
            throw NoEncontrado(id);
        }

        await refreshTokens.RevocarTodosAsync(id, cancellationToken);
    }

    public async Task DesactivarAsync(long id, CancellationToken cancellationToken)
    {
        var actual = await repositorio.ObtenerAsync(id, cancellationToken) ?? throw NoEncontrado(id);
        if (admin.EsAdministrador(actual.Usuario))
        {
            throw new ConflictoException(AdminProtegido);
        }

        await repositorio.DesactivarAsync(id, cancellationToken);
        await refreshTokens.RevocarTodosAsync(id, cancellationToken);
    }

    /// <summary>
    /// Crea el administrador configurado en "Admin" si todavía no existe un usuario con ese nombre.
    /// Devuelve false si no hay configuración o si ya existía.
    /// </summary>
    public async Task<bool> SembrarAdministradorAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(admin.Usuario) || string.IsNullOrWhiteSpace(admin.Clave))
        {
            return false;
        }

        var nombreUsuario = NormalizarUsuario(admin.Usuario);
        if (await repositorio.ObtenerPorNombreUsuarioAsync(nombreUsuario, cancellationToken) is not null)
        {
            return false;
        }

        if (admin.Clave.Length is < Reglas.ClaveMinimo or > Reglas.ClaveMaximo)
        {
            throw new InvalidOperationException($"'Admin:Clave' no es válida: {Reglas.MensajeClave}");
        }

        await repositorio.CrearAsync(
            new Usuario { NombreUsuario = nombreUsuario, Nombre = admin.Nombre.Trim(), ClaveHash = hasher.Hash(admin.Clave) },
            cancellationToken);
        return true;
    }

    private static string NormalizarUsuario(string usuario) => usuario.Trim().ToLowerInvariant();

    private static string? NormalizarEmail(string? email) =>
        string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();

    private static NoEncontradoException NoEncontrado(long id) => new($"No existe el usuario {id}.");
}
