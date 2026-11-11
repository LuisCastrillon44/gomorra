using Application.Caja;

namespace Application.Ventas;

/// <summary>Resumen de las ventas de los turnos de caja abiertos en el periodo. Solo cuenta ventas no anuladas.</summary>
public sealed class ResumenVentasDto
{
    public decimal TotalVendido { get; init; }
    public int CantidadVentas { get; init; }
    public decimal TicketPromedio { get; init; }
    public decimal Efectivo { get; init; }
    public decimal Transferencia { get; init; }
    public int CantidadAnuladas { get; init; }
    public decimal TotalAnulado { get; init; }

    /// <summary>Turnos del periodo, del más antiguo al más reciente (para la gráfica).</summary>
    public List<TurnoVentasDto> Turnos { get; init; } = [];

    /// <summary>Los productos que más dinero dejaron.</summary>
    public List<ProductoVendidoDto> TopProductos { get; init; } = [];
}

public sealed class TurnoVentasDto
{
    public long CajaId { get; init; }
    public string Estado { get; init; } = string.Empty;
    public DateTime AbiertaEn { get; init; }
    public DateTime? CerradaEn { get; init; }
    public decimal Total { get; init; }
    public int CantidadVentas { get; init; }
}

public sealed class ProductoVendidoDto
{
    public long ProductoId { get; init; }
    public string Nombre { get; init; } = string.Empty;
    public string? Presentacion { get; init; }
    public int Unidades { get; init; }
    public decimal Total { get; init; }
}

/// <summary>Una página del historial de ventas.</summary>
public sealed class PaginaVentasDto
{
    public List<VentaDto> Items { get; init; } = [];
    public bool HayMas { get; init; }
}

/// <summary>Periodo: turnos cuya apertura cae en [Desde, Hasta), en UTC.</summary>
public sealed record PeriodoVentas(DateTime Desde, DateTime Hasta);

public sealed record FiltroHistorialVentas(PeriodoVentas Periodo, long? CajaId, string? Metodo, int Limite, int Offset);
