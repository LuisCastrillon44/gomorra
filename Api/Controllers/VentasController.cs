using Application.Caja;
using Application.Ventas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/ventas")]
public sealed class VentasController(CajaService servicio, VentasService ventas) : ControllerBase
{
    /// <summary>Resumen de las ventas de los turnos abiertos en [desde, hasta). Solo administrador.</summary>
    [HttpGet("resumen")]
    [Authorize(Policy = Politicas.Administrador)]
    public Task<ResumenVentasDto> Resumen(
        [FromQuery] DateTimeOffset desde, [FromQuery] DateTimeOffset hasta, CancellationToken cancellationToken) =>
        ventas.ResumenAsync(desde, hasta, cancellationToken);

    /// <summary>Historial de ventas de los turnos abiertos en [desde, hasta), paginado. Solo administrador.</summary>
    [HttpGet]
    [Authorize(Policy = Politicas.Administrador)]
    public Task<PaginaVentasDto> Historial(
        [FromQuery] DateTimeOffset desde,
        [FromQuery] DateTimeOffset hasta,
        [FromQuery] long? cajaId,
        [FromQuery] string? metodo,
        [FromQuery] int limite = 50,
        [FromQuery] int offset = 0,
        CancellationToken cancellationToken = default) =>
        ventas.HistorialAsync(desde, hasta, cajaId, metodo, limite, offset, cancellationToken);

    [HttpGet("{id:long}")]
    public Task<VentaDto> Obtener(long id, CancellationToken cancellationToken) =>
        servicio.ObtenerVentaAsync(id, cancellationToken);

    /// <summary>Anula la venta (solo con la caja abierta) y devuelve su stock al inventario.</summary>
    [HttpPost("{id:long}/anular")]
    public Task<VentaDto> Anular(long id, CancellationToken cancellationToken) =>
        servicio.AnularVentaAsync(id, cancellationToken);
}
