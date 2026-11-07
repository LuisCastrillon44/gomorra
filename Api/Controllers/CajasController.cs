using Application.Caja;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/cajas")]
public sealed class CajasController(CajaService servicio) : ControllerBase
{
    /// <summary>Historial de cajas cerradas, de la más reciente a la más antigua.</summary>
    [HttpGet]
    public Task<IReadOnlyList<CajaDto>> ListarCerradas([FromQuery] int limite = 30, CancellationToken cancellationToken = default) =>
        servicio.ListarCerradasAsync(limite, cancellationToken);

    /// <summary>La caja abierta con su resumen, o 204 si no hay ninguna.</summary>
    [HttpGet("abierta")]
    public async Task<ActionResult<CajaDto>> ObtenerAbierta(CancellationToken cancellationToken)
    {
        var caja = await servicio.ObtenerAbiertaAsync(cancellationToken);
        return caja is null ? NoContent() : caja;
    }

    [HttpGet("{id:long}")]
    public Task<CajaDto> Obtener(long id, CancellationToken cancellationToken) =>
        servicio.ObtenerAsync(id, cancellationToken);

    [HttpPost]
    public async Task<ActionResult<CajaDto>> Abrir(AbrirCajaRequest request, CancellationToken cancellationToken)
    {
        var caja = await servicio.AbrirAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Obtener), new { id = caja.Id }, caja);
    }

    /// <summary>Arqueo y cierre: guarda el efectivo esperado y el contado.</summary>
    [HttpPost("{id:long}/cerrar")]
    public Task<CajaDto> Cerrar(long id, CerrarCajaRequest request, CancellationToken cancellationToken) =>
        servicio.CerrarAsync(id, request, cancellationToken);

    [HttpGet("{id:long}/ventas")]
    public Task<IReadOnlyList<VentaDto>> ListarVentas(long id, CancellationToken cancellationToken) =>
        servicio.ListarVentasAsync(id, cancellationToken);

    /// <summary>Registra una venta con el precio actual de cada producto y descuenta el inventario.</summary>
    [HttpPost("{id:long}/ventas")]
    public async Task<ActionResult<VentaDto>> RegistrarVenta(
        long id, RegistrarVentaRequest request, CancellationToken cancellationToken)
    {
        var venta = await servicio.RegistrarVentaAsync(id, request, cancellationToken);
        return CreatedAtAction(nameof(VentasController.Obtener), "Ventas", new { id = venta.Id }, venta);
    }

    /// <summary>Entradas y salidas de efectivo, de la más reciente a la más antigua.</summary>
    [HttpGet("{id:long}/movimientos")]
    public Task<IReadOnlyList<MovimientoCajaDto>> ListarMovimientos(long id, CancellationToken cancellationToken) =>
        servicio.ListarMovimientosAsync(id, cancellationToken);

    [HttpPost("{id:long}/movimientos")]
    public async Task<ActionResult<MovimientoCajaDto>> RegistrarMovimiento(
        long id, RegistrarMovimientoCajaRequest request, CancellationToken cancellationToken)
    {
        var movimiento = await servicio.RegistrarMovimientoAsync(id, request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, movimiento);
    }
}
