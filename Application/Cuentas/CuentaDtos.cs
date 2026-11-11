using System.ComponentModel.DataAnnotations;
using Application.Caja;

namespace Application.Cuentas;

public static class EstadosCuenta
{
    public const string Abierta = "abierta";
    public const string Pagada = "pagada";
    public const string Anulada = "anulada";
}

/// <summary>Cuenta de una mesa o cliente. Mientras está abierta, el total es al precio actual.</summary>
public sealed class CuentaDto
{
    public long Id { get; init; }
    public long CajaId { get; init; }
    public string Nombre { get; init; } = string.Empty;
    public string Estado { get; init; } = string.Empty;
    public long? VentaId { get; init; }
    public DateTime AbiertaEn { get; init; }
    public DateTime? CerradaEn { get; init; }
    public decimal Total { get; init; }
    public int Articulos { get; init; }
    public List<CuentaItemDto> Items { get; init; } = [];
}

public sealed class CuentaItemDto
{
    public long CuentaId { get; init; }
    public long ProductoId { get; init; }
    public string Nombre { get; init; } = string.Empty;
    public string? Presentacion { get; init; }
    public string? ImagenUrl { get; init; }
    public int Cantidad { get; init; }
    public decimal PrecioUnitario { get; init; }
    public decimal Subtotal { get; init; }
}

public sealed class CrearCuentaRequest
{
    [Required(ErrorMessage = "El nombre de la cuenta es obligatorio.")]
    [StringLength(40, ErrorMessage = "El nombre admite máximo 40 caracteres.")]
    public string Nombre { get; init; } = string.Empty;

    /// <summary>Productos con los que nace la cuenta (opcional).</summary>
    public List<ItemVentaRequest> Items { get; init; } = [];
}

public sealed class RenombrarCuentaRequest
{
    [Required(ErrorMessage = "El nombre de la cuenta es obligatorio.")]
    [StringLength(40, ErrorMessage = "El nombre admite máximo 40 caracteres.")]
    public string Nombre { get; init; } = string.Empty;
}

public sealed class AgregarItemsCuentaRequest
{
    [Required(ErrorMessage = "Agrega al menos un producto.")]
    [MinLength(1, ErrorMessage = "Agrega al menos un producto.")]
    public List<ItemVentaRequest> Items { get; init; } = [];
}

public sealed class FijarCantidadRequest
{
    /// <summary>0 quita el producto de la cuenta.</summary>
    [Range(0, 10_000, ErrorMessage = "La cantidad debe estar entre 0 y 10.000.")]
    public int Cantidad { get; init; }
}

public sealed class PagarCuentaRequest : PagoRequest;
