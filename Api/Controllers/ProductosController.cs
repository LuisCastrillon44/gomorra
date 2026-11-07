using Application.Productos;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/productos")]
public sealed class ProductosController(ProductoService servicio) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<ProductoDto>> Listar(
        [FromQuery] string? buscar,
        [FromQuery] long? categoriaId,
        [FromQuery] bool incluirInactivos,
        CancellationToken cancellationToken) =>
        servicio.ListarAsync(
            new ProductoFiltro { Buscar = buscar, CategoriaId = categoriaId, IncluirInactivos = incluirInactivos },
            cancellationToken);

    [HttpGet("{id:long}")]
    public Task<ProductoDto> Obtener(long id, CancellationToken cancellationToken) =>
        servicio.ObtenerAsync(id, cancellationToken);

    [HttpPost]
    public async Task<ActionResult<ProductoDto>> Crear(GuardarProductoRequest request, CancellationToken cancellationToken)
    {
        var producto = await servicio.CrearAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Obtener), new { id = producto.Id }, producto);
    }

    [HttpPut("{id:long}")]
    public Task<ProductoDto> Actualizar(long id, GuardarProductoRequest request, CancellationToken cancellationToken) =>
        servicio.ActualizarAsync(id, request, cancellationToken);

    /// <summary>Borrado lógico: marca el producto como inactivo.</summary>
    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Desactivar(long id, CancellationToken cancellationToken)
    {
        await servicio.DesactivarAsync(id, cancellationToken);
        return NoContent();
    }
}
