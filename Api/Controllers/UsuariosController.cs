using Application.Usuarios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>Administración de usuarios. Por ahora solo para el administrador principal.</summary>
[ApiController]
[Route("api/usuarios")]
[Authorize(Policy = Politicas.Administrador)]
public sealed class UsuariosController(UsuarioService servicio) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<UsuarioDto>> Listar([FromQuery] bool incluirInactivos, CancellationToken cancellationToken) =>
        servicio.ListarAsync(incluirInactivos, cancellationToken);

    [HttpGet("{id:long}")]
    public Task<UsuarioDto> Obtener(long id, CancellationToken cancellationToken) =>
        servicio.ObtenerAsync(id, cancellationToken);

    [HttpPost]
    public async Task<ActionResult<UsuarioDto>> Crear(CrearUsuarioRequest request, CancellationToken cancellationToken)
    {
        var usuario = await servicio.CrearAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Obtener), new { id = usuario.Id }, usuario);
    }

    [HttpPut("{id:long}")]
    public Task<UsuarioDto> Actualizar(long id, ActualizarUsuarioRequest request, CancellationToken cancellationToken) =>
        servicio.ActualizarAsync(id, request, cancellationToken);

    /// <summary>Cambia la clave y cierra todas las sesiones del usuario.</summary>
    [HttpPut("{id:long}/clave")]
    public async Task<IActionResult> CambiarClave(long id, CambiarClaveRequest request, CancellationToken cancellationToken)
    {
        await servicio.CambiarClaveAsync(id, request, cancellationToken);
        return NoContent();
    }

    /// <summary>Borrado lógico: marca el usuario como inactivo y cierra sus sesiones.</summary>
    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Desactivar(long id, CancellationToken cancellationToken)
    {
        await servicio.DesactivarAsync(id, cancellationToken);
        return NoContent();
    }
}
