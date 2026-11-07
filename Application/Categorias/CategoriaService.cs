using Application.Common;
using Domain.Categorias;

namespace Application.Categorias;

public sealed class CategoriaService(ICategoriaRepository repositorio)
{
    private const string TieneProductosActivos =
        "No se puede desactivar la categoría porque tiene productos activos.";

    public Task<IReadOnlyList<CategoriaDto>> ListarAsync(bool incluirInactivas, CancellationToken cancellationToken) =>
        repositorio.ListarAsync(incluirInactivas, cancellationToken);

    public async Task<CategoriaDto> ObtenerAsync(long id, CancellationToken cancellationToken) =>
        await repositorio.ObtenerAsync(id, cancellationToken) ?? throw NoEncontrada(id);

    public async Task<CategoriaDto> CrearAsync(GuardarCategoriaRequest request, CancellationToken cancellationToken)
    {
        var id = await repositorio.CrearAsync(
            new Categoria { Nombre = request.Nombre.Trim(), Activo = request.Activo },
            cancellationToken);

        return await ObtenerAsync(id, cancellationToken);
    }

    public async Task<CategoriaDto> ActualizarAsync(long id, GuardarCategoriaRequest request, CancellationToken cancellationToken)
    {
        if (!request.Activo && await repositorio.TieneProductosActivosAsync(id, cancellationToken))
        {
            throw new ConflictoException(TieneProductosActivos);
        }

        var actualizada = await repositorio.ActualizarAsync(
            new Categoria { Id = id, Nombre = request.Nombre.Trim(), Activo = request.Activo },
            cancellationToken);

        if (!actualizada)
        {
            throw NoEncontrada(id);
        }

        return await ObtenerAsync(id, cancellationToken);
    }

    public async Task DesactivarAsync(long id, CancellationToken cancellationToken)
    {
        if (await repositorio.TieneProductosActivosAsync(id, cancellationToken))
        {
            throw new ConflictoException(TieneProductosActivos);
        }

        if (!await repositorio.DesactivarAsync(id, cancellationToken))
        {
            throw NoEncontrada(id);
        }
    }

    private static NoEncontradoException NoEncontrada(long id) => new($"No existe la categoría {id}.");
}
