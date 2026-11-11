using System.ComponentModel.DataAnnotations;

namespace Application.Inventario;

public static class TiposMovimiento
{
    public const string Entrada = "entrada";
    public const string Salida = "salida";
    public const string Ajuste = "ajuste";

    // Los registra la caja; no se pueden crear a mano.
    public const string Venta = "venta";
    public const string AnulacionVenta = "anulacion_venta";
}

public sealed class InventarioItemDto
{
    public long ProductoId { get; init; }
    public string Nombre { get; init; } = string.Empty;
    public long CategoriaId { get; init; }
    public string CategoriaNombre { get; init; } = string.Empty;
    public string? Presentacion { get; init; }
    public string? ImagenUrl { get; init; }
    public decimal PrecioVenta { get; init; }

    /// <summary>Precio cuando se consume en el local (lo que se cobra en cuentas).</summary>
    public decimal PrecioLocal { get; init; }
    public bool Activo { get; init; }
    public int Stock { get; init; }
    public int StockMinimo { get; init; }

    /// <summary>Unidades pedidas en cuentas abiertas (aún no descontadas del stock).</summary>
    public int EnCuentas { get; init; }

    /// <summary>Lo que se puede vender o pedir: stock menos lo que está en cuentas abiertas.</summary>
    public int Disponible { get; init; }

    /// <summary>Agotado, o con mínimo definido y en o por debajo de él.</summary>
    public bool PorReponer { get; init; }
}

public sealed class MovimientoDto
{
    public long Id { get; init; }
    public string Tipo { get; init; } = string.Empty;

    /// <summary>Cambio con signo: positivo suma al stock, negativo resta.</summary>
    public int Cantidad { get; init; }

    public int StockResultante { get; init; }
    public decimal? CostoUnitario { get; init; }
    public DateTime CreadoEn { get; init; }
}

public sealed class RegistrarMovimientoRequest
{
    [Required(ErrorMessage = "El tipo de movimiento es obligatorio.")]
    [RegularExpression("^(entrada|salida|ajuste)$", ErrorMessage = "El tipo debe ser entrada, salida o ajuste.")]
    public string Tipo { get; init; } = string.Empty;

    /// <summary>Entrada/salida: unidades que entran o salen. Ajuste: cantidad real contada.</summary>
    [Range(0, int.MaxValue, ErrorMessage = "La cantidad no puede ser negativa.")]
    public int Cantidad { get; init; }

    [Range(0, 9_999_999_999.99, ErrorMessage = "El costo unitario no puede ser negativo.")]
    public decimal? CostoUnitario { get; init; }
}

public sealed class ActualizarStockMinimoRequest
{
    [Range(0, int.MaxValue, ErrorMessage = "El stock mínimo no puede ser negativo.")]
    public int StockMinimo { get; init; }
}

public sealed class InventarioFiltro
{
    public string? Buscar { get; init; }
    public long? CategoriaId { get; init; }
    public bool SoloPorReponer { get; init; }
    public bool IncluirInactivos { get; init; }
}
