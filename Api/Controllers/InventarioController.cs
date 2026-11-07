using Application.Inventario;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/inventario")]
public sealed class InventarioController(InventarioService servicio) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<InventarioItemDto>> Listar(
        [FromQuery] string? buscar,
        [FromQuery] long? categoriaId,
        [FromQuery] bool soloPorReponer,
        [FromQuery] bool incluirInactivos,
        CancellationToken cancellationToken) =>
        servicio.ListarAsync(
            new InventarioFiltro
            {
                Buscar = buscar,
                CategoriaId = categoriaId,
                SoloPorReponer = soloPorReponer,
                IncluirInactivos = incluirInactivos,
            },
            cancellationToken);

    [HttpGet("{productoId:long}")]
    public Task<InventarioItemDto> Obtener(long productoId, CancellationToken cancellationToken) =>
        servicio.ObtenerAsync(productoId, cancellationToken);

    [HttpPut("{productoId:long}/stock-minimo")]
    public Task<InventarioItemDto> ActualizarStockMinimo(
        long productoId, ActualizarStockMinimoRequest request, CancellationToken cancellationToken) =>
        servicio.ActualizarStockMinimoAsync(productoId, request, cancellationToken);

    /// <summary>Historial de movimientos, del más reciente al más antiguo.</summary>
    [HttpGet("{productoId:long}/movimientos")]
    public Task<IReadOnlyList<MovimientoDto>> ListarMovimientos(
        long productoId, [FromQuery] int limite = 50, CancellationToken cancellationToken = default) =>
        servicio.ListarMovimientosAsync(productoId, limite, cancellationToken);

    /// <summary>Registra una entrada, salida o ajuste y devuelve el inventario actualizado del producto.</summary>
    [HttpPost("{productoId:long}/movimientos")]
    public Task<InventarioItemDto> RegistrarMovimiento(
        long productoId, RegistrarMovimientoRequest request, CancellationToken cancellationToken) =>
        servicio.RegistrarMovimientoAsync(productoId, request, cancellationToken);
}
