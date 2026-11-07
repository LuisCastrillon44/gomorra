using Application.Categorias;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/categorias")]
public sealed class CategoriasController(CategoriaService servicio) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<CategoriaDto>> Listar([FromQuery] bool incluirInactivas, CancellationToken cancellationToken) =>
        servicio.ListarAsync(incluirInactivas, cancellationToken);

    [HttpGet("{id:long}")]
    public Task<CategoriaDto> Obtener(long id, CancellationToken cancellationToken) =>
        servicio.ObtenerAsync(id, cancellationToken);

    [HttpPost]
    public async Task<ActionResult<CategoriaDto>> Crear(GuardarCategoriaRequest request, CancellationToken cancellationToken)
    {
        var categoria = await servicio.CrearAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Obtener), new { id = categoria.Id }, categoria);
    }

    [HttpPut("{id:long}")]
    public Task<CategoriaDto> Actualizar(long id, GuardarCategoriaRequest request, CancellationToken cancellationToken) =>
        servicio.ActualizarAsync(id, request, cancellationToken);

    /// <summary>Borrado lógico: marca la categoría como inactiva.</summary>
    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Desactivar(long id, CancellationToken cancellationToken)
    {
        await servicio.DesactivarAsync(id, cancellationToken);
        return NoContent();
    }
}
