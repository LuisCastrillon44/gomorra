namespace Application.Caja;

/// <summary>
/// Acceso a cajas, ventas y movimientos de efectivo. Las operaciones de escritura son atómicas y validan
/// el estado de la caja (y el stock) dentro de su transacción; si una regla falla lanzan
/// <see cref="Common.ConflictoException"/> o <see cref="Common.NoEncontradoException"/>.
/// </summary>
public interface ICajaRepository
{
    Task<CajaDto?> ObtenerAsync(long id, CancellationToken cancellationToken);
    Task<CajaDto?> ObtenerAbiertaAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<CajaDto>> ListarCerradasAsync(int limite, CancellationToken cancellationToken);

    /// <summary>Abre una caja. Lanza conflicto si ya hay una abierta.</summary>
    Task<long> AbrirAsync(decimal baseInicial, CancellationToken cancellationToken);

    /// <summary>Calcula y guarda el efectivo esperado junto con el contado.</summary>
    Task CerrarAsync(long id, decimal efectivoContado, string? nota, CancellationToken cancellationToken);

    Task<IReadOnlyList<VentaDto>> ListarVentasAsync(long cajaId, CancellationToken cancellationToken);
    Task<VentaDto?> ObtenerVentaAsync(long ventaId, CancellationToken cancellationToken);

    /// <summary>
    /// Registra la venta con el precio actual de cada producto y descuenta el inventario.
    /// <paramref name="items"/> debe venir agrupado por producto.
    /// </summary>
    Task<long> RegistrarVentaAsync(long cajaId, string metodoPago, IReadOnlyList<ItemVenta> items, CancellationToken cancellationToken);

    /// <summary>Anula la venta y devuelve su stock al inventario.</summary>
    Task AnularVentaAsync(long ventaId, CancellationToken cancellationToken);

    Task<IReadOnlyList<MovimientoCajaDto>> ListarMovimientosAsync(long cajaId, CancellationToken cancellationToken);
    Task<long> RegistrarMovimientoAsync(long cajaId, string tipo, decimal monto, string motivo, CancellationToken cancellationToken);
    Task<MovimientoCajaDto?> ObtenerMovimientoAsync(long id, CancellationToken cancellationToken);
}
