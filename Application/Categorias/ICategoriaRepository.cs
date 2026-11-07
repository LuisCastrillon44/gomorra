using Domain.Categorias;

namespace Application.Categorias;

public interface ICategoriaRepository
{
    Task<IReadOnlyList<CategoriaDto>> ListarAsync(bool incluirInactivas, CancellationToken cancellationToken);
    Task<CategoriaDto?> ObtenerAsync(long id, CancellationToken cancellationToken);
    Task<long> CrearAsync(Categoria categoria, CancellationToken cancellationToken);
    Task<bool> ActualizarAsync(Categoria categoria, CancellationToken cancellationToken);
    Task<bool> DesactivarAsync(long id, CancellationToken cancellationToken);
    Task<bool> TieneProductosActivosAsync(long id, CancellationToken cancellationToken);
}
