namespace Domain.Productos;

public sealed class Producto
{
    public long Id { get; set; }
    public long CategoriaId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public int? PresentacionMl { get; set; }
    public decimal PrecioVenta { get; set; }
    public decimal Costo { get; set; }

    /// <summary>TEMPORAL: URL externa de la imagen, hasta que exista la subida de imágenes.</summary>
    public string? ImagenUrl { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime CreadoEn { get; set; }
    public DateTime ActualizadoEn { get; set; }
}
