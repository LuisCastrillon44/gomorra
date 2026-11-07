using Application.Caja;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/ventas")]
public sealed class VentasController(CajaService servicio) : ControllerBase
{
    [HttpGet("{id:long}")]
    public Task<VentaDto> Obtener(long id, CancellationToken cancellationToken) =>
        servicio.ObtenerVentaAsync(id, cancellationToken);

    /// <summary>Anula la venta (solo con la caja abierta) y devuelve su stock al inventario.</summary>
    [HttpPost("{id:long}/anular")]
    public Task<VentaDto> Anular(long id, CancellationToken cancellationToken) =>
        servicio.AnularVentaAsync(id, cancellationToken);
}
