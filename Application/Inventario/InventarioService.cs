using Application.Common;

namespace Application.Inventario;

public sealed class InventarioService(IInventarioRepository repositorio)
{
    public const int LimiteMovimientosMaximo = 200;

    public Task<IReadOnlyList<InventarioItemDto>> ListarAsync(InventarioFiltro filtro, CancellationToken cancellationToken) =>
        repositorio.ListarAsync(filtro, cancellationToken);

    public async Task<InventarioItemDto> ObtenerAsync(long productoId, CancellationToken cancellationToken) =>
        await repositorio.ObtenerAsync(productoId, cancellationToken)
        ?? throw new NoEncontradoException($"No existe el producto {productoId}.");

    public async Task<InventarioItemDto> ActualizarStockMinimoAsync(
        long productoId, ActualizarStockMinimoRequest request, CancellationToken cancellationToken)
    {
        await ObtenerAsync(productoId, cancellationToken); // 404 si no existe
        await repositorio.ActualizarStockMinimoAsync(productoId, request.StockMinimo, cancellationToken);
        return await ObtenerAsync(productoId, cancellationToken);
    }

    public async Task<IReadOnlyList<MovimientoDto>> ListarMovimientosAsync(
        long productoId, int limite, CancellationToken cancellationToken)
    {
        await ObtenerAsync(productoId, cancellationToken); // 404 si no existe
        return await repositorio.ListarMovimientosAsync(
            productoId, Math.Clamp(limite, 1, LimiteMovimientosMaximo), cancellationToken);
    }

    public async Task<InventarioItemDto> RegistrarMovimientoAsync(
        long productoId, RegistrarMovimientoRequest request, CancellationToken cancellationToken)
    {
        var item = await ObtenerAsync(productoId, cancellationToken);
        if (!item.Activo)
        {
            throw new ConflictoException("El producto está inactivo. Reactívalo para registrar movimientos.");
        }

        if (request.CostoUnitario is not null && request.Tipo != TiposMovimiento.Entrada)
        {
            throw new ValidacionException("El costo unitario solo aplica a las entradas.");
        }

        Func<int, int> calcularDelta = request.Tipo switch
        {
            TiposMovimiento.Entrada => _ => request.Cantidad > 0
                ? request.Cantidad
                : throw new ValidacionException("La cantidad de la entrada debe ser mayor que 0."),

            TiposMovimiento.Salida => stock =>
            {
                if (request.Cantidad <= 0)
                    throw new ValidacionException("La cantidad de la salida debe ser mayor que 0.");
                if (request.Cantidad > stock)
                    throw new ConflictoException($"No hay suficiente stock: solo hay {Unidades(stock)}.");
                return -request.Cantidad;
            },

            // En el ajuste la cantidad es lo que se contó físicamente.
            TiposMovimiento.Ajuste => stock => request.Cantidad != stock
                ? request.Cantidad - stock
                : throw new ValidacionException($"El stock ya es {Unidades(stock)}; no hay nada que ajustar."),

            _ => throw new ValidacionException("El tipo debe ser entrada, salida o ajuste."),
        };

        await repositorio.RegistrarMovimientoAsync(
            productoId, request.Tipo, calcularDelta, request.CostoUnitario, cancellationToken);

        return await ObtenerAsync(productoId, cancellationToken);
    }

    private static string Unidades(int n) => n == 1 ? "1 unidad" : $"{n} unidades";
}
