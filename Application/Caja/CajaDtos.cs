using System.ComponentModel.DataAnnotations;

namespace Application.Caja;

public static class EstadosCaja
{
    public const string Abierta = "abierta";
    public const string Cerrada = "cerrada";
}

public static class MetodosPago
{
    public const string Efectivo = "efectivo";
    public const string Transferencia = "transferencia";

    /// <summary>Parte en efectivo y parte por transferencia.</summary>
    public const string Mixto = "mixto";

    public const string Patron = "^(efectivo|transferencia|mixto)$";
    public const string MensajePatron = "El método de pago debe ser efectivo, transferencia o mixto.";
}

/// <summary>
/// Cómo se paga una venta. En efectivo o transferencia el monto es el total (lo calcula el servidor);
/// en mixto se indican los dos montos y deben sumar el total.
/// </summary>
public sealed record Pago(string Metodo, decimal? Efectivo, decimal? Transferencia);

public static class TiposMovimientoCaja
{
    public const string Entrada = "entrada";
    public const string Salida = "salida";
}

/// <summary>Caja con el resumen de su turno.</summary>
public sealed class CajaDto
{
    public long Id { get; init; }
    public string Estado { get; init; } = string.Empty;
    public decimal BaseInicial { get; init; }
    public DateTime AbiertaEn { get; init; }
    public DateTime? CerradaEn { get; init; }

    /// <summary>Ventas no anuladas pagadas en efectivo.</summary>
    public decimal VentasEfectivo { get; init; }

    /// <summary>Ventas no anuladas pagadas por transferencia.</summary>
    public decimal VentasTransferencia { get; init; }

    public decimal TotalVentas => VentasEfectivo + VentasTransferencia;

    /// <summary>Cantidad de ventas no anuladas.</summary>
    public int CantidadVentas { get; init; }

    public decimal Entradas { get; init; }
    public decimal Salidas { get; init; }

    /// <summary>
    /// Base + ventas en efectivo + entradas - salidas. En una caja cerrada es el valor guardado al cerrar.
    /// </summary>
    public decimal EfectivoEsperado { get; init; }

    public decimal? EfectivoContado { get; init; }

    /// <summary>Contado - esperado: positivo = sobrante, negativo = faltante. Solo en cajas cerradas.</summary>
    public decimal? Diferencia => EfectivoContado - EfectivoEsperado;

    public string? NotaCierre { get; init; }
}

public sealed class VentaDto
{
    public long Id { get; init; }
    public long CajaId { get; init; }
    public string MetodoPago { get; init; } = string.Empty;
    public decimal Total { get; init; }
    public decimal PagoEfectivo { get; init; }
    public decimal PagoTransferencia { get; init; }
    public bool Anulada { get; init; }
    public DateTime? AnuladaEn { get; init; }
    public DateTime CreadoEn { get; init; }
    public List<VentaDetalleDto> Items { get; init; } = [];
}

public sealed class VentaDetalleDto
{
    public long VentaId { get; init; }
    public long ProductoId { get; init; }
    public string ProductoNombre { get; init; } = string.Empty;
    public string? Presentacion { get; init; }
    public int Cantidad { get; init; }
    public decimal PrecioUnitario { get; init; }
    public decimal Subtotal { get; init; }
}

public sealed class MovimientoCajaDto
{
    public long Id { get; init; }
    public long CajaId { get; init; }
    public string Tipo { get; init; } = string.Empty;
    public decimal Monto { get; init; }
    public string Motivo { get; init; } = string.Empty;
    public DateTime CreadoEn { get; init; }
}

public sealed class AbrirCajaRequest
{
    [Required(ErrorMessage = "La base inicial es obligatoria.")]
    [Range(0, 9_999_999_999.99, ErrorMessage = "La base inicial no puede ser negativa.")]
    public decimal? BaseInicial { get; init; }
}

public sealed class CerrarCajaRequest
{
    [Required(ErrorMessage = "El efectivo contado es obligatorio.")]
    [Range(0, 9_999_999_999.99, ErrorMessage = "El efectivo contado no puede ser negativo.")]
    public decimal? EfectivoContado { get; init; }

    [MaxLength(500, ErrorMessage = "La nota no puede superar 500 caracteres.")]
    public string? Nota { get; init; }
}

/// <summary>Datos de pago comunes a la venta directa y al cobro de una cuenta.</summary>
public class PagoRequest
{
    [Required(ErrorMessage = "El método de pago es obligatorio.")]
    [RegularExpression(MetodosPago.Patron, ErrorMessage = MetodosPago.MensajePatron)]
    public string MetodoPago { get; init; } = string.Empty;

    /// <summary>Solo en pago mixto.</summary>
    [Range(0.01, 9_999_999_999.99, ErrorMessage = "El monto en efectivo debe ser mayor que 0.")]
    public decimal? PagoEfectivo { get; init; }

    /// <summary>Solo en pago mixto.</summary>
    [Range(0.01, 9_999_999_999.99, ErrorMessage = "El monto por transferencia debe ser mayor que 0.")]
    public decimal? PagoTransferencia { get; init; }

    public Pago ComoPago() => new(MetodoPago, PagoEfectivo, PagoTransferencia);
}

public sealed class RegistrarVentaRequest : PagoRequest
{
    [Required(ErrorMessage = "La venta debe tener al menos un producto.")]
    [MinLength(1, ErrorMessage = "La venta debe tener al menos un producto.")]
    public List<ItemVentaRequest> Items { get; init; } = [];
}

public sealed class ItemVentaRequest
{
    [Range(1, long.MaxValue, ErrorMessage = "El producto es obligatorio.")]
    public long ProductoId { get; init; }

    [Range(1, 10_000, ErrorMessage = "La cantidad debe estar entre 1 y 10.000.")]
    public int Cantidad { get; init; }
}

public sealed class RegistrarMovimientoCajaRequest
{
    [Required(ErrorMessage = "El tipo es obligatorio.")]
    [RegularExpression("^(entrada|salida)$", ErrorMessage = "El tipo debe ser entrada o salida.")]
    public string Tipo { get; init; } = string.Empty;

    [Required(ErrorMessage = "El monto es obligatorio.")]
    [Range(0.01, 9_999_999_999.99, ErrorMessage = "El monto debe ser mayor que 0.")]
    public decimal? Monto { get; init; }

    [Required(ErrorMessage = "El motivo es obligatorio.")]
    [MaxLength(200, ErrorMessage = "El motivo no puede superar 200 caracteres.")]
    public string Motivo { get; init; } = string.Empty;
}

/// <summary>Ítem de venta ya agrupado por producto, listo para el repositorio.</summary>
public sealed record ItemVenta(long ProductoId, int Cantidad);
