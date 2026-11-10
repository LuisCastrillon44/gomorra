using Application.Caja;
using Application.Cuentas;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>
/// Cuentas abiertas por mesa o cliente: los pedidos se acumulan y se pagan al final.
/// Para abrir una: POST /api/cajas/{cajaId}/cuentas.
/// </summary>
[ApiController]
[Route("api/cuentas")]
public sealed class CuentasController(CuentaService servicio) : ControllerBase
{
    [HttpGet("{id:long}")]
    public Task<CuentaDto> Obtener(long id, CancellationToken cancellationToken) =>
        servicio.ObtenerAsync(id, cancellationToken);

    /// <summary>Cambia el nombre (p. ej. al pasar a otra mesa).</summary>
    [HttpPut("{id:long}")]
    public Task<CuentaDto> Renombrar(long id, RenombrarCuentaRequest request, CancellationToken cancellationToken) =>
        servicio.RenombrarAsync(id, request, cancellationToken);

    /// <summary>Suma una ronda de productos a la cuenta.</summary>
    [HttpPost("{id:long}/items")]
    public Task<CuentaDto> AgregarItems(long id, AgregarItemsCuentaRequest request, CancellationToken cancellationToken) =>
        servicio.AgregarItemsAsync(id, request, cancellationToken);

    /// <summary>Fija la cantidad de un producto; 0 lo quita.</summary>
    [HttpPut("{id:long}/items/{productoId:long}")]
    public Task<CuentaDto> FijarCantidad(
        long id, long productoId, FijarCantidadRequest request, CancellationToken cancellationToken) =>
        servicio.FijarCantidadAsync(id, productoId, request, cancellationToken);

    /// <summary>Cobra la cuenta: registra la venta (descuenta inventario) y la cierra.</summary>
    [HttpPost("{id:long}/pagar")]
    public async Task<ActionResult<VentaDto>> Pagar(long id, PagarCuentaRequest request, CancellationToken cancellationToken)
    {
        var venta = await servicio.PagarAsync(id, request, cancellationToken);
        return CreatedAtAction(nameof(VentasController.Obtener), "Ventas", new { id = venta.Id }, venta);
    }

    /// <summary>Anula la cuenta sin cobrar (libera lo apartado; el inventario no se había tocado).</summary>
    [HttpPost("{id:long}/anular")]
    public Task<CuentaDto> Anular(long id, CancellationToken cancellationToken) =>
        servicio.AnularAsync(id, cancellationToken);
}
