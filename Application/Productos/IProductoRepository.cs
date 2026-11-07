using Domain.Productos;

namespace Application.Productos;

public interface IProductoRepository
{
    Task<IReadOnlyList<ProductoDto>> ListarAsync(ProductoFiltro filtro, CancellationToken cancellationToken);
    Task<ProductoDto?> ObtenerAsync(long id, CancellationToken cancellationToken);
    Task<long> CrearAsync(Producto producto, CancellationToken cancellationToken);
    Task<bool> ActualizarAsync(Producto producto, CancellationToken cancellationToken);
    Task<bool> DesactivarAsync(long id, CancellationToken cancellationToken);
}
