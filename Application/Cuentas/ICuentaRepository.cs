using Application.Caja;

namespace Application.Cuentas;

/// <summary>
/// Cuentas abiertas. Las escrituras son atómicas, exigen la caja abierta y validan lo disponible
/// (stock menos lo pedido en cuentas abiertas); si una regla falla lanzan ConflictoException o NoEncontradoException.
/// </summary>
public interface ICuentaRepository
{
    /// <summary>Cuentas abiertas de la caja, con total y artículos (sin el detalle).</summary>
    Task<IReadOnlyList<CuentaDto>> ListarAbiertasAsync(long cajaId, CancellationToken cancellationToken);

    Task<CuentaDto?> ObtenerAsync(long id, CancellationToken cancellationToken);

    /// <summary><paramref name="items"/> agrupado por producto y ordenado por id.</summary>
    Task<long> CrearAsync(long cajaId, string nombre, IReadOnlyList<ItemVenta> items, CancellationToken cancellationToken);

    Task RenombrarAsync(long id, string nombre, CancellationToken cancellationToken);

    /// <summary>Suma las cantidades a lo que ya tiene la cuenta.</summary>
    Task AgregarItemsAsync(long id, IReadOnlyList<ItemVenta> items, CancellationToken cancellationToken);

    /// <summary>Deja el producto con esa cantidad; 0 lo quita.</summary>
    Task FijarCantidadAsync(long id, long productoId, int cantidad, CancellationToken cancellationToken);

    /// <summary>Registra la venta con lo de la cuenta (descuenta inventario) y la marca pagada. Devuelve el id de la venta.</summary>
    Task<long> PagarAsync(long id, Pago pago, CancellationToken cancellationToken);

    Task AnularAsync(long id, CancellationToken cancellationToken);
}
