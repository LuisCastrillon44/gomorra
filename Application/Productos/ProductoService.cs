using Application.Categorias;
using Application.Common;
using Domain.Productos;

namespace Application.Productos;

public sealed class ProductoService(IProductoRepository repositorio, ICategoriaRepository categorias)
{
    public Task<IReadOnlyList<ProductoDto>> ListarAsync(ProductoFiltro filtro, CancellationToken cancellationToken) =>
        repositorio.ListarAsync(filtro, cancellationToken);

    public async Task<ProductoDto> ObtenerAsync(long id, CancellationToken cancellationToken) =>
        await repositorio.ObtenerAsync(id, cancellationToken) ?? throw NoEncontrado(id);

    public async Task<ProductoDto> CrearAsync(GuardarProductoRequest request, CancellationToken cancellationToken)
    {
        await ValidarCategoriaAsync(request, cancellationToken);

        var id = await repositorio.CrearAsync(AProducto(0, request), cancellationToken);
        return await ObtenerAsync(id, cancellationToken);
    }

    public async Task<ProductoDto> ActualizarAsync(long id, GuardarProductoRequest request, CancellationToken cancellationToken)
    {
        await ValidarCategoriaAsync(request, cancellationToken);

        if (!await repositorio.ActualizarAsync(AProducto(id, request), cancellationToken))
        {
            throw NoEncontrado(id);
        }

        return await ObtenerAsync(id, cancellationToken);
    }

    public async Task DesactivarAsync(long id, CancellationToken cancellationToken)
    {
        if (!await repositorio.DesactivarAsync(id, cancellationToken))
        {
            throw NoEncontrado(id);
        }
    }

    /// <summary>
    /// La categoría debe existir. Si el producto queda activo, la categoría también debe estar activa
    /// (así una categoría inactiva nunca tiene productos activos).
    /// </summary>
    private async Task ValidarCategoriaAsync(GuardarProductoRequest request, CancellationToken cancellationToken)
    {
        var categoria = await categorias.ObtenerAsync(request.CategoriaId, cancellationToken)
            ?? throw new ValidacionException($"No existe la categoría {request.CategoriaId}.");

        if (request.Activo && !categoria.Activo)
        {
            throw new ValidacionException($"La categoría \"{categoria.Nombre}\" está inactiva.");
        }
    }

    private static Producto AProducto(long id, GuardarProductoRequest request) => new()
    {
        Id = id,
        CategoriaId = request.CategoriaId,
        Nombre = request.Nombre.Trim(),
        PresentacionMl = request.PresentacionMl,
        PrecioVenta = request.PrecioVenta,
        PrecioLocal = request.PrecioLocal,
        ImagenUrl = string.IsNullOrWhiteSpace(request.ImagenUrl) ? null : request.ImagenUrl.Trim(),
        Activo = request.Activo,
    };

    private static NoEncontradoException NoEncontrado(long id) => new($"No existe el producto {id}.");
}
