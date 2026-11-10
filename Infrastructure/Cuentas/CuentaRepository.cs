using System.Data.Common;
using Application.Abstractions.Data;
using Application.Caja;
using Application.Common;
using Application.Cuentas;
using Dapper;
using Infrastructure.Data;
using static Infrastructure.Caja.OperacionesCaja;

namespace Infrastructure.Cuentas;

internal sealed class CuentaRepository(IDbConnectionFactory connectionFactory) : ICuentaRepository
{
    // Las cuentas son consumo en el local: se cobran a precio_local.
    // Abierta: total al precio actual. Pagada: el total de su venta.
    private const string SelectCuenta = """
        SELECT c.id, c.caja_id, c.nombre, c.estado, c.venta_id, c.abierta_en, c.cerrada_en,
               COALESCE(v.total, t.total, 0) AS total,
               COALESCE(t.articulos, 0) AS articulos
        FROM gomorra.cuentas c
        LEFT JOIN gomorra.ventas v ON v.id = c.venta_id
        LEFT JOIN LATERAL (
            SELECT SUM(d.cantidad * p.precio_local) AS total, SUM(d.cantidad)::int AS articulos
            FROM gomorra.cuentas_detalle d
            JOIN gomorra.productos p ON p.id = d.producto_id
            WHERE d.cuenta_id = c.id
        ) t ON true
        """;

    private const string SelectItems = """
        SELECT d.cuenta_id, d.producto_id, p.nombre, p.presentacion_ml, p.imagen_url, d.cantidad,
               p.precio_local AS precio_unitario, d.cantidad * p.precio_local AS subtotal
        FROM gomorra.cuentas_detalle d
        JOIN gomorra.productos p ON p.id = d.producto_id
        """;

    private static readonly Dictionary<string, string> MensajesUnicos = new()
    {
        ["cuentas_nombre_abierta_unico"] = "Ya hay una cuenta abierta con ese nombre.",
    };

    public async Task<IReadOnlyList<CuentaDto>> ListarAbiertasAsync(long cajaId, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        var filas = await connection.QueryAsync<CuentaDto>(new CommandDefinition(
            $"{SelectCuenta}\nWHERE c.caja_id = @CajaId AND c.estado = 'abierta'\nORDER BY c.abierta_en, c.id",
            new { CajaId = cajaId }, cancellationToken: cancellationToken));
        return filas.AsList();
    }

    public async Task<CuentaDto?> ObtenerAsync(long id, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);

        var cuenta = await connection.QuerySingleOrDefaultAsync<CuentaDto>(new CommandDefinition(
            $"{SelectCuenta}\nWHERE c.id = @Id", new { Id = id }, cancellationToken: cancellationToken));
        if (cuenta is null) return null;

        var items = await connection.QueryAsync<CuentaItemDto>(new CommandDefinition(
            $"{SelectItems}\nWHERE d.cuenta_id = @Id\nORDER BY d.id",
            new { Id = id }, cancellationToken: cancellationToken));
        cuenta.Items.AddRange(items);
        return cuenta;
    }

    public async Task<long> CrearAsync(long cajaId, string nombre, IReadOnlyList<ItemVenta> items, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await BloquearCajaAbiertaAsync(connection, transaction, cajaId, "FOR SHARE", cancellationToken);

        var id = await PostgresErrores.TraducirUnicosAsync(
            () => connection.ExecuteScalarAsync<long>(new CommandDefinition(
                "INSERT INTO gomorra.cuentas (caja_id, nombre) VALUES (@CajaId, @Nombre) RETURNING id",
                new { CajaId = cajaId, Nombre = nombre }, transaction, cancellationToken: cancellationToken)),
            MensajesUnicos);

        foreach (var item in items)
        {
            await ApartarAsync(connection, transaction, id, item.ProductoId, item.Cantidad, actual: 0, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return id;
    }

    public async Task RenombrarAsync(long id, string nombre, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await BloquearCuentaAbiertaAsync(connection, transaction, id, cancellationToken);
        await PostgresErrores.TraducirUnicosAsync(
            () => connection.ExecuteAsync(new CommandDefinition(
                "UPDATE gomorra.cuentas SET nombre = @Nombre WHERE id = @Id",
                new { Id = id, Nombre = nombre }, transaction, cancellationToken: cancellationToken)),
            MensajesUnicos);

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task AgregarItemsAsync(long id, IReadOnlyList<ItemVenta> items, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await BloquearCuentaAbiertaAsync(connection, transaction, id, cancellationToken);
        var actuales = await CantidadesAsync(connection, transaction, id, cancellationToken);

        foreach (var item in items)
        {
            var actual = actuales.GetValueOrDefault(item.ProductoId);
            await ApartarAsync(connection, transaction, id, item.ProductoId, actual + item.Cantidad, actual, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task FijarCantidadAsync(long id, long productoId, int cantidad, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await BloquearCuentaAbiertaAsync(connection, transaction, id, cancellationToken);
        var actual = (await CantidadesAsync(connection, transaction, id, cancellationToken)).GetValueOrDefault(productoId);

        if (cantidad == 0)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "DELETE FROM gomorra.cuentas_detalle WHERE cuenta_id = @Id AND producto_id = @ProductoId",
                new { Id = id, ProductoId = productoId }, transaction, cancellationToken: cancellationToken));
        }
        else
        {
            await ApartarAsync(connection, transaction, id, productoId, cantidad, actual, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<long> PagarAsync(long id, Pago pago, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var cajaId = await BloquearCuentaAbiertaAsync(connection, transaction, id, cancellationToken);

        var items = (await connection.QueryAsync<ItemVenta>(new CommandDefinition(
            "SELECT producto_id AS productoid, cantidad FROM gomorra.cuentas_detalle WHERE cuenta_id = @Id ORDER BY producto_id",
            new { Id = id }, transaction, cancellationToken: cancellationToken))).AsList();

        if (items.Count == 0)
        {
            throw new ValidacionException("La cuenta no tiene productos. Agrega productos o anúlala.");
        }

        // Lo de esta cuenta ya estaba apartado: se valida contra lo disponible sin contarla.
        var ventaId = await RegistrarVentaAsync(connection, transaction, cajaId, pago, items, cuentaId: id, cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE gomorra.cuentas SET estado = 'pagada', venta_id = @VentaId, cerrada_en = now() WHERE id = @Id",
            new { Id = id, VentaId = ventaId }, transaction, cancellationToken: cancellationToken));

        await transaction.CommitAsync(cancellationToken);
        return ventaId;
    }

    public async Task AnularAsync(long id, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        // El inventario no se tocó: anular solo libera lo apartado.
        await BloquearCuentaAbiertaAsync(connection, transaction, id, cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE gomorra.cuentas SET estado = 'anulada', cerrada_en = now() WHERE id = @Id",
            new { Id = id }, transaction, cancellationToken: cancellationToken));

        await transaction.CommitAsync(cancellationToken);
    }

    // ---------- Apoyo ----------

    /// <summary>
    /// Bloquea la caja de la cuenta (FOR SHARE, el cierre espera) y la cuenta (FOR UPDATE), y exige que ambas
    /// estén abiertas. Devuelve el id de la caja.
    /// </summary>
    private static async Task<long> BloquearCuentaAbiertaAsync(
        DbConnection connection, DbTransaction transaction, long id, CancellationToken cancellationToken)
    {
        var cajaId = await connection.ExecuteScalarAsync<long?>(new CommandDefinition(
            "SELECT caja_id FROM gomorra.cuentas WHERE id = @Id",
            new { Id = id }, transaction, cancellationToken: cancellationToken))
            ?? throw new NoEncontradoException($"No existe la cuenta {id}.");

        await BloquearCajaAbiertaAsync(
            connection, transaction, cajaId, "FOR SHARE", cancellationToken,
            mensajeCerrada: "La caja de esta cuenta ya está cerrada.");

        var estado = await connection.ExecuteScalarAsync<string>(new CommandDefinition(
            "SELECT estado FROM gomorra.cuentas WHERE id = @Id FOR UPDATE",
            new { Id = id }, transaction, cancellationToken: cancellationToken));

        return estado switch
        {
            EstadosCuenta.Abierta => cajaId,
            EstadosCuenta.Pagada => throw new ConflictoException("La cuenta ya fue pagada."),
            _ => throw new ConflictoException("La cuenta está anulada."),
        };
    }

    private static async Task<Dictionary<long, int>> CantidadesAsync(
        DbConnection connection, DbTransaction transaction, long cuentaId, CancellationToken cancellationToken)
    {
        var filas = await connection.QueryAsync<ItemVenta>(new CommandDefinition(
            "SELECT producto_id AS productoid, cantidad FROM gomorra.cuentas_detalle WHERE cuenta_id = @Id",
            new { Id = cuentaId }, transaction, cancellationToken: cancellationToken));
        return filas.ToDictionary(f => f.ProductoId, f => f.Cantidad);
    }

    /// <summary>
    /// Deja el producto en la cuenta con <paramref name="cantidad"/>. Si aumenta, exige que el producto esté activo
    /// y que alcance lo disponible (stock menos lo pedido en otras cuentas). Bajar siempre se permite.
    /// </summary>
    private static async Task ApartarAsync(
        DbConnection connection,
        DbTransaction transaction,
        long cuentaId,
        long productoId,
        int cantidad,
        int actual,
        CancellationToken cancellationToken)
    {
        if (cantidad > actual)
        {
            var producto = await BloquearProductoActivoAsync(connection, transaction, productoId, cancellationToken);
            var stock = await BloquearStockAsync(connection, transaction, productoId, cancellationToken);
            var enOtras = await EnCuentasAsync(connection, transaction, productoId, cuentaId, cancellationToken);

            if (cantidad > stock - enOtras)
            {
                throw SinDisponible(producto.Nombre, stock - enOtras, enOtras);
            }
        }

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO gomorra.cuentas_detalle (cuenta_id, producto_id, cantidad)
            VALUES (@CuentaId, @ProductoId, @Cantidad)
            ON CONFLICT (cuenta_id, producto_id)
            DO UPDATE SET cantidad = EXCLUDED.cantidad, actualizado_en = now()
            """,
            new { CuentaId = cuentaId, ProductoId = productoId, Cantidad = cantidad },
            transaction, cancellationToken: cancellationToken));
    }
}
