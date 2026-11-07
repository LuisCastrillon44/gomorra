namespace Application.Inventario;

public interface IInventarioRepository
{
    Task<IReadOnlyList<InventarioItemDto>> ListarAsync(InventarioFiltro filtro, CancellationToken cancellationToken);
    Task<InventarioItemDto?> ObtenerAsync(long productoId, CancellationToken cancellationToken);
    Task ActualizarStockMinimoAsync(long productoId, int stockMinimo, CancellationToken cancellationToken);
    Task<IReadOnlyList<MovimientoDto>> ListarMovimientosAsync(long productoId, int limite, CancellationToken cancellationToken);

    /// <summary>
    /// Registra un movimiento de forma atómica: bloquea el stock del producto, pide el cambio a
    /// <paramref name="calcularDelta"/> (que recibe el stock actual y puede lanzar una excepción para cancelar)
    /// y guarda el nuevo stock junto con el movimiento.
    /// </summary>
    Task RegistrarMovimientoAsync(
        long productoId,
        string tipo,
        Func<int, int> calcularDelta,
        decimal? costoUnitario,
        CancellationToken cancellationToken);
}
