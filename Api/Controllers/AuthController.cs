using Application.Autenticacion;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(AutenticacionService servicio) : ControllerBase
{
    /// <summary>Inicia sesión con usuario y clave. Devuelve el access token y el refresh token.</summary>
    [AllowAnonymous]
    [HttpPost("login")]
    public Task<SesionDto> Login(LoginRequest request, CancellationToken cancellationToken) =>
        servicio.LoginAsync(request, cancellationToken);

    /// <summary>Cambia un refresh token vigente por una sesión nueva (el token usado queda revocado).</summary>
    [AllowAnonymous]
    [HttpPost("refresh")]
    public Task<SesionDto> Refrescar(RefrescarRequest request, CancellationToken cancellationToken) =>
        servicio.RefrescarAsync(request, cancellationToken);

    /// <summary>Revoca el refresh token. Es anónimo porque el access token puede haber vencido.</summary>
    [AllowAnonymous]
    [HttpPost("logout")]
    public async Task<IActionResult> CerrarSesion(RefrescarRequest request, CancellationToken cancellationToken)
    {
        await servicio.CerrarSesionAsync(request, cancellationToken);
        return NoContent();
    }

    /// <summary>Usuario de la sesión actual.</summary>
    [HttpGet("yo")]
    public Task<UsuarioSesionDto> Yo(CancellationToken cancellationToken) =>
        servicio.ObtenerUsuarioAsync(long.Parse(User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value), cancellationToken);
}
