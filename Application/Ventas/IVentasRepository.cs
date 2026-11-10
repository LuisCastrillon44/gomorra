namespace Application.Ventas;

/// <summary>Consultas de ventas para reportes (solo lectura).</summary>
public interface IVentasRepository
{
    Task<ResumenVentasDto> ResumenAsync(PeriodoVentas periodo, CancellationToken cancellationToken);

    /// <summary>Ventas (con su detalle) de la más reciente a la más antigua.</summary>
    Task<PaginaVentasDto> HistorialAsync(FiltroHistorialVentas filtro, CancellationToken cancellationToken);
}
