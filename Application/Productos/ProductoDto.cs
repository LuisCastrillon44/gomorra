using System.ComponentModel.DataAnnotations;

namespace Application.Productos;

public sealed class ProductoDto
{
    public long Id { get; init; }
    public long CategoriaId { get; init; }
    public string CategoriaNombre { get; init; } = string.Empty;
    public string Nombre { get; init; } = string.Empty;
    public string? Presentacion { get; init; }
    public decimal PrecioVenta { get; init; }
    public decimal PrecioLocal { get; init; }
    public string? ImagenUrl { get; init; } // TEMPORAL
    public bool Activo { get; init; }
    public DateTime CreadoEn { get; init; }
    public DateTime ActualizadoEn { get; init; }
}

public sealed class GuardarProductoRequest
{
    private const double PrecioMaximo = 9_999_999_999.99; // numeric(12,2)

    [Range(1, long.MaxValue, ErrorMessage = "La categoría es obligatoria.")]
    public long CategoriaId { get; init; }

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(120, ErrorMessage = "El nombre admite máximo 120 caracteres.")]
    public string Nombre { get; init; } = string.Empty;

    /// <summary>Texto libre: Media, Botella, Litro, Garrafa, "350 ml"… Vacío = sin presentación.</summary>
    [StringLength(40, ErrorMessage = "La presentación admite máximo 40 caracteres.")]
    public string? Presentacion { get; init; }

    [Range(0, PrecioMaximo, ErrorMessage = "El precio de venta no puede ser negativo.")]
    public decimal PrecioVenta { get; init; }

    [Range(0, PrecioMaximo, ErrorMessage = "El precio en el local no puede ser negativo.")]
    public decimal PrecioLocal { get; init; }

    /// <summary>TEMPORAL: URL externa de la imagen (http/https). Vacía = sin imagen.</summary>
    [StringLength(500, ErrorMessage = "La URL de la imagen admite máximo 500 caracteres.")]
    [UrlHttp(ErrorMessage = "La URL de la imagen debe empezar por http:// o https://.")]
    public string? ImagenUrl { get; init; }

    public bool Activo { get; init; } = true;
}

/// <summary>Valida que el texto sea una URL absoluta http/https. Nulo o vacío se considera válido.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class UrlHttpAttribute : ValidationAttribute
{
    public override bool IsValid(object? value) =>
        value is not string texto
        || string.IsNullOrWhiteSpace(texto)
        || (Uri.TryCreate(texto.Trim(), UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps));
}

public sealed class ProductoFiltro
{
    public string? Buscar { get; init; }
    public long? CategoriaId { get; init; }
    public bool IncluirInactivos { get; init; }
}
