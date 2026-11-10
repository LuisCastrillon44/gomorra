using System.Data.Common;
using System.Globalization;
using Application.Caja;
using Application.Common;
using Dapper;

namespace Infrastructure.Caja;

/// <summary>
/// Pasos compartidos por la venta directa y el cobro de cuentas, siempre dentro de la transacción del llamador.
/// Orden de bloqueo en todas las operaciones: caja → cuenta → productos → inventario (por id ascendente).
/// </summary>
internal static class OperacionesCaja
{
    private static readonly CultureInfo Colombia = CultureInfo.GetCultureInfo("es-CO");

    /// <summary>Bloquea la fila de la caja (FOR SHARE / FOR UPDATE) y exige que exista y esté abierta.</summary>
    public static async Task BloquearCajaAbiertaAsync(
        DbConnection connection,
        DbTransaction transaction,
        long cajaId,
        string bloqueo,
        CancellationToken cancellationToken,
        string mensajeCerrada = "La caja está cerrada.")
    {
        var estado = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            $"SELECT estado FROM gomorra.cajas WHERE id = @Id {bloqueo}",
            new { Id = cajaId }, transaction, cancellationToken: cancellationToken))
            ?? throw new NoEncontradoException($"No existe la caja {cajaId}.");

        if (estado != EstadosCaja.Abierta)
        {
            throw new ConflictoException(mensajeCerrada);
        }
    }

    /// <summary>Bloquea el producto (FOR SHARE) y exige que exista y esté activo.</summary>
    public static async Task<ProductoVenta> BloquearProductoActivoAsync(
        DbConnection connection, DbTransaction transaction, long productoId, CancellationToken cancellationToken)
    {
        var producto = await connection.QuerySingleOrDefaultAsync<ProductoVenta>(new CommandDefinition(
            "SELECT nombre, precio_venta, activo FROM gomorra.productos WHERE id = @Id FOR SHARE",
            new { Id = productoId }, transaction, cancellationToken: cancellationToken))
            ?? throw new NoEncontradoException($"No existe el producto {productoId}.");

        if (!producto.Activo)
        {
            throw new ConflictoException($"{producto.Nombre} está inactivo y no se puede vender.");
        }

        return producto;
    }

    /// <summary>Asegura la fila de inventario del producto, la bloquea y devuelve su stock.</summary>
    public static async Task<int> BloquearStockAsync(
        DbConnection connection, DbTransaction transaction, long productoId, CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            "INSERT INTO gomorra.inventario (producto_id) VALUES (@ProductoId) ON CONFLICT (producto_id) DO NOTHING",
            new { ProductoId = productoId }, transaction, cancellationToken: cancellationToken));

        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT stock FROM gomorra.inventario WHERE producto_id = @ProductoId FOR UPDATE",
            new { ProductoId = productoId }, transaction, cancellationToken: cancellationToken));
    }

    /// <summary>
    /// Unidades del producto pedidas en cuentas abiertas (sin descontar aún del stock), opcionalmente sin contar una cuenta.
    /// Llamar con la fila de inventario ya bloqueada: así las cuentas y ventas del producto quedan en fila.
    /// </summary>
    public static Task<int> EnCuentasAsync(
        DbConnection connection, DbTransaction transaction, long productoId, long? excluirCuentaId, CancellationToken cancellationToken) =>
        connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            SELECT COALESCE(SUM(d.cantidad), 0)::int
            FROM gomorra.cuentas_detalle d
            JOIN gomorra.cuentas c ON c.id = d.cuenta_id
            WHERE c.estado = 'abierta' AND d.producto_id = @ProductoId
              AND (@Excluir::bigint IS NULL OR c.id <> @Excluir)
            """,
            new { ProductoId = productoId, Excluir = excluirCuentaId }, transaction, cancellationToken: cancellationToken));

    /// <summary>Mensaje de falta de stock que explica cuánto está apartado en cuentas.</summary>
    public static ConflictoException SinDisponible(string producto, int disponible, int enCuentas) =>
        new(enCuentas > 0
            ? $"No hay suficiente {producto}: solo quedan {Unidades(Math.Max(disponible, 0))} disponibles ({enCuentas} en cuentas abiertas)."
            : $"No hay suficiente stock de {producto}: solo hay {Unidades(Math.Max(disponible, 0))}.");

    /// <summary>
    /// Registra la venta (precio actual de cada producto) y descuenta el inventario. La caja ya debe estar bloqueada.
    /// Valida contra lo disponible: stock menos lo pedido en cuentas abiertas, sin contar <paramref name="cuentaId"/>
    /// (al cobrar una cuenta, lo suyo ya estaba apartado).
    /// </summary>
    public static async Task<long> RegistrarVentaAsync(
        DbConnection connection,
        DbTransaction transaction,
        long cajaId,
        Pago pago,
        IReadOnlyList<ItemVenta> items,
        long? cuentaId,
        CancellationToken cancellationToken)
    {
        // Primero se validan todos los productos y su disponible (bloqueando cada fila, en orden de id).
        var lineas = new List<(ItemVenta Item, decimal Precio, int StockActual)>();
        foreach (var item in items)
        {
            var producto = await BloquearProductoActivoAsync(connection, transaction, item.ProductoId, cancellationToken);
            var stock = await BloquearStockAsync(connection, transaction, item.ProductoId, cancellationToken);
            var enCuentas = await EnCuentasAsync(connection, transaction, item.ProductoId, cuentaId, cancellationToken);

            if (item.Cantidad > stock - enCuentas)
            {
                throw SinDisponible(producto.Nombre, stock - enCuentas, enCuentas);
            }

            lineas.Add((item, producto.PrecioVenta, stock));
        }

        var total = lineas.Sum(l => l.Item.Cantidad * l.Precio);
        var (efectivo, transferencia) = Repartir(pago, total);

        var ventaId = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            INSERT INTO gomorra.ventas (caja_id, metodo_pago, total, pago_efectivo, pago_transferencia)
            VALUES (@CajaId, @MetodoPago, @Total, @Efectivo, @Transferencia)
            RETURNING id
            """,
            new { CajaId = cajaId, MetodoPago = pago.Metodo, Total = total, Efectivo = efectivo, Transferencia = transferencia },
            transaction, cancellationToken: cancellationToken));

        foreach (var (item, precio, stockActual) in lineas)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO gomorra.ventas_detalle (venta_id, producto_id, cantidad, precio_unitario)
                VALUES (@VentaId, @ProductoId, @Cantidad, @Precio)
                """,
                new { VentaId = ventaId, item.ProductoId, item.Cantidad, Precio = precio },
                transaction, cancellationToken: cancellationToken));

            await MoverStockAsync(
                connection, transaction, item.ProductoId, "venta", -item.Cantidad, stockActual, ventaId, cancellationToken);
        }

        return ventaId;
    }

    /// <summary>Aplica el cambio al stock (ya bloqueado) y deja el movimiento enlazado a la venta.</summary>
    public static async Task MoverStockAsync(
        DbConnection connection,
        DbTransaction transaction,
        long productoId,
        string tipo,
        int cantidad,
        int stockActual,
        long ventaId,
        CancellationToken cancellationToken)
    {
        var stockResultante = stockActual + cantidad;

        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE gomorra.inventario
            SET stock = @StockResultante, actualizado_en = now()
            WHERE producto_id = @ProductoId
            """,
            new { ProductoId = productoId, StockResultante = stockResultante },
            transaction, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO gomorra.movimientos_inventario (producto_id, tipo, cantidad, stock_resultante, venta_id)
            VALUES (@ProductoId, @Tipo, @Cantidad, @StockResultante, @VentaId)
            """,
            new { ProductoId = productoId, Tipo = tipo, Cantidad = cantidad, StockResultante = stockResultante, VentaId = ventaId },
            transaction, cancellationToken: cancellationToken));
    }

    /// <summary>Cuánto entra por cada medio. En mixto los montos deben sumar exactamente el total.</summary>
    private static (decimal Efectivo, decimal Transferencia) Repartir(Pago pago, decimal total)
    {
        switch (pago.Metodo)
        {
            case MetodosPago.Efectivo:
                return (total, 0);
            case MetodosPago.Transferencia:
                return (0, total);
            default:
                var efectivo = pago.Efectivo ?? 0;
                var transferencia = pago.Transferencia ?? 0;
                var diferencia = total - (efectivo + transferencia);
                if (diferencia != 0)
                {
                    throw new ValidacionException(diferencia > 0
                        ? $"El pago mixto no cubre el total de {Pesos(total)}: faltan {Pesos(diferencia)}."
                        : $"El pago mixto supera el total de {Pesos(total)} por {Pesos(-diferencia)}.");
                }

                return (efectivo, transferencia);
        }
    }

    public static string Unidades(int n) => n == 1 ? "1 unidad" : $"{n} unidades";

    public static string Pesos(decimal valor) => valor.ToString("C0", Colombia);

    public sealed class ProductoVenta
    {
        public string Nombre { get; init; } = string.Empty;
        public decimal PrecioVenta { get; init; }
        public bool Activo { get; init; }
    }
}
