using Application.Abstractions.Seguridad;
using Application.Common;
using Application.Usuarios;
using Domain.Usuarios;

namespace Application.Autenticacion;

public sealed class AutenticacionService(
    IUsuarioRepository usuarios,
    IRefreshTokenRepository refreshTokens,
    IHasherClaves hasher,
    IGeneradorTokens generador,
    JwtOptions jwt,
    AdminOptions admin)
{
    private const string CredencialesInvalidas = "Usuario o clave incorrectos.";
    private const string SesionInvalida = "La sesión expiró. Inicia sesión de nuevo.";

    // Hash de relleno para que un usuario inexistente tarde lo mismo que una clave errónea
    // (evita adivinar qué usuarios existen midiendo el tiempo de respuesta).
    private static string? hashRelleno;

    public async Task<SesionDto> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var usuario = await usuarios.ObtenerPorNombreUsuarioAsync(request.Usuario.Trim(), cancellationToken);
        if (usuario is null)
        {
            hasher.Verificar(request.Clave, hashRelleno ??= hasher.Hash(Guid.NewGuid().ToString()));
            throw new NoAutorizadoException(CredencialesInvalidas);
        }

        if (!hasher.Verificar(request.Clave, usuario.ClaveHash) || !usuario.Activo)
        {
            throw new NoAutorizadoException(CredencialesInvalidas);
        }

        await usuarios.RegistrarAccesoAsync(usuario.Id, cancellationToken);

        var refreshToken = generador.CrearRefreshToken();
        await refreshTokens.CrearAsync(
            usuario.Id, generador.HashRefreshToken(refreshToken), VencimientoRefresh(), cancellationToken);

        return CrearSesion(usuario, refreshToken);
    }

    public async Task<SesionDto> RefrescarAsync(RefrescarRequest request, CancellationToken cancellationToken)
    {
        var actual = await refreshTokens.ObtenerPorHashAsync(generador.HashRefreshToken(request.RefreshToken), cancellationToken)
            ?? throw new NoAutorizadoException(SesionInvalida);

        if (actual.RevocadoEn is not null)
        {
            // Un token ya usado vuelve a aparecer: posible robo. Se cierran todas las sesiones del usuario.
            await refreshTokens.RevocarTodosAsync(actual.UsuarioId, cancellationToken);
            throw new NoAutorizadoException(SesionInvalida);
        }

        if (actual.ExpiraEn <= DateTime.UtcNow)
        {
            throw new NoAutorizadoException(SesionInvalida);
        }

        var usuario = await usuarios.ObtenerEntidadAsync(actual.UsuarioId, cancellationToken);
        if (usuario is null || !usuario.Activo)
        {
            await refreshTokens.RevocarTodosAsync(actual.UsuarioId, cancellationToken);
            throw new NoAutorizadoException(SesionInvalida);
        }

        var nuevo = generador.CrearRefreshToken();
        var rotado = await refreshTokens.RotarAsync(
            actual.Id, usuario.Id, generador.HashRefreshToken(nuevo), VencimientoRefresh(), cancellationToken);

        if (!rotado)
        {
            // Otra petición usó este mismo token al mismo tiempo: se trata como reutilización.
            await refreshTokens.RevocarTodosAsync(usuario.Id, cancellationToken);
            throw new NoAutorizadoException(SesionInvalida);
        }

        return CrearSesion(usuario, nuevo);
    }

    public Task CerrarSesionAsync(RefrescarRequest request, CancellationToken cancellationToken) =>
        refreshTokens.RevocarAsync(generador.HashRefreshToken(request.RefreshToken), cancellationToken);

    public async Task<UsuarioSesionDto> ObtenerUsuarioAsync(long usuarioId, CancellationToken cancellationToken)
    {
        var usuario = await usuarios.ObtenerEntidadAsync(usuarioId, cancellationToken);
        if (usuario is null || !usuario.Activo)
        {
            throw new NoAutorizadoException(SesionInvalida);
        }

        return UsuarioSesion(usuario);
    }

    private SesionDto CrearSesion(Usuario usuario, string refreshToken)
    {
        var acceso = generador.CrearAccessToken(usuario.Id, usuario.NombreUsuario, usuario.Nombre);
        return new SesionDto(acceso.Token, acceso.ExpiraEn, refreshToken, UsuarioSesion(usuario));
    }

    private UsuarioSesionDto UsuarioSesion(Usuario usuario) =>
        new(usuario.Id, usuario.NombreUsuario, usuario.Nombre, usuario.Email, admin.EsAdministrador(usuario.NombreUsuario));

    private DateTime VencimientoRefresh() => DateTime.UtcNow.AddDays(jwt.DiasRefresh);
}
