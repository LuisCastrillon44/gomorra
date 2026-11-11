namespace Domain.Productos;

public sealed class Producto
{
    public long Id { get; set; }
    public long CategoriaId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Presentacion { get; set; }
    public decimal PrecioVenta { get; set; }
    /// <summary>Precio cuando el producto se consume en el local.</summary>
    public decimal PrecioLocal { get; set; }

    /// <summary>TEMPORAL: URL externa de la imagen, hasta que exista la subida de imágenes.</summary>
    public string? ImagenUrl { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime CreadoEn { get; set; }
    public DateTime ActualizadoEn { get; set; }
}
