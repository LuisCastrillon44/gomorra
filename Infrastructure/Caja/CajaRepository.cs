using System.Data.Common;
using System.Globalization;
using Application.Abstractions.Data;
using Application.Caja;
using Application.Common;
using Dapper;
using Infrastructure.Data;

namespace Infrastructure.Caja;

internal sealed class CajaRepository(IDbConnectionFactory connectionFactory) : ICajaRepository
{
    // Resumen calculado sobre ventas no anuladas y movimientos de efectivo.
    // En una caja cerrada el esperado es el guardado al cerrar (foto del arqueo).
    private const string Select = """
        SELECT c.id, c.estado, c.base_inicial, c.abierta_en, c.cerrada_en,
               v.ventas_efectivo, v.ventas_transferencia, v.cantidad_ventas,
               m.entradas, m.salidas,
               COALESCE(c.efectivo_esperado, c.base_inicial + v.ventas_efectivo + m.entradas - m.salidas) AS efectivo_esperado,
               c.efectivo_contado, c.nota_cierre
        FROM gomorra.cajas c
        CROSS JOIN LATERAL (
            SELECT COALESCE(SUM(total) FILTER (WHERE metodo_pago = 'efectivo'), 0) AS ventas_efectivo,
                   COALESCE(SUM(total) FILTER (WHERE metodo_pago = 'transferencia'), 0) AS ventas_transferencia,
                   COUNT(*)::int AS cantidad_ventas
            FROM gomorra.ventas
            WHERE caja_id = c.id AND NOT anulada
        ) v
        CROSS JOIN LATERAL (
            SELECT COALESCE(SUM(monto) FILTER (WHERE tipo = 'entrada'), 0) AS entradas,
                   COALESCE(SUM(monto) FILTER (WHERE tipo = 'salida'), 0) AS salidas
            FROM gomorra.movimientos_caja
            WHERE caja_id = c.id
        ) m
        """;

    private const string SelectVenta = """
        SELECT id, caja_id, metodo_pago, total, anulada, anulada_en, creado_en
        FROM gomorra.ventas
        """;

    private const string SelectDetalle = """
        SELECT d.venta_id, d.producto_id, p.nombre AS producto_nombre, p.presentacion_ml,
               d.cantidad, d.precio_unitario, d.subtotal
        FROM gomorra.ventas_detalle d
        JOIN gomorra.productos p ON p.id = d.producto_id
        """;

    private const string SelectMovimiento = """
        SELECT id, caja_id, tipo, monto, motivo, creado_en
        FROM gomorra.movimientos_caja
        """;

    private static readonly Dictionary<string, string> MensajesUnicos = new()
    {
        ["cajas_una_abierta"] = "Ya hay una caja abierta. Ciérrala antes de abrir otra.",
    };

    private static readonly CultureInfo Colombia = CultureInfo.GetCultureInfo("es-CO");

    // ---------- Cajas ----------

    public async Task<CajaDto?> ObtenerAsync(long id, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        return await ObtenerAsync(connection, null, id, cancellationToken);
    }

    public async Task<CajaDto?> ObtenerAbiertaAsync(CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<CajaDto>(new CommandDefinition(
            $"{Select}\nWHERE c.estado = 'abierta'", cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<CajaDto>> ListarCerradasAsync(int limite, CancellationToken cancellationToken)
    {
        var sql = $"{Select}\nWHERE c.estado = 'cerrada'\nORDER BY c.abierta_en DESC, c.id DESC\nLIMIT @Limite";

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        var filas = await connection.QueryAsync<CajaDto>(
            new CommandDefinition(sql, new { Limite = limite }, cancellationToken: cancellationToken));
        return filas.AsList();
    }

    public async Task<long> AbrirAsync(decimal baseInicial, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        return await PostgresErrores.TraducirUnicosAsync(
            () => connection.ExecuteScalarAsync<long>(new CommandDefinition(
                "INSERT INTO gomorra.cajas (base_inicial) VALUES (@BaseInicial) RETURNING id",
                new { BaseInicial = baseInicial }, cancellationToken: cancellationToken)),
            MensajesUnicos);
    }

    public async Task CerrarAsync(long id, decimal efectivoContado, string? nota, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        // FOR UPDATE espera a que terminen las ventas en curso (que toman la caja con FOR SHARE).
        await BloquearCajaAbiertaAsync(connection, transaction, id, "FOR UPDATE", cancellationToken);
        var caja = (await ObtenerAsync(connection, transaction, id, cancellationToken))!;

        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE gomorra.cajas
            SET estado = 'cerrada', cerrada_en = now(),
                efectivo_esperado = @EfectivoEsperado, efectivo_contado = @EfectivoContado, nota_cierre = @Nota
            WHERE id = @Id
            """,
            new { Id = id, caja.EfectivoEsperado, EfectivoContado = efectivoContado, Nota = nota },
            transaction, cancellationToken: cancellationToken));

        await transaction.CommitAsync(cancellationToken);
    }

    // ---------- Ventas ----------

    public async Task<IReadOnlyList<VentaDto>> ListarVentasAsync(long cajaId, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);

        var ventas = (await connection.QueryAsync<VentaDto>(new CommandDefinition(
            $"{SelectVenta}\nWHERE caja_id = @CajaId\nORDER BY creado_en DESC, id DESC",
            new { CajaId = cajaId }, cancellationToken: cancellationToken))).AsList();

        var detalle = await connection.QueryAsync<VentaDetalleDto>(new CommandDefinition(
            $"{SelectDetalle}\nJOIN gomorra.ventas v ON v.id = d.venta_id\nWHERE v.caja_id = @CajaId\nORDER BY d.id",
            new { CajaId = cajaId }, cancellationToken: cancellationToken));

        var porVenta = ventas.ToDictionary(v => v.Id);
        foreach (var item in detalle)
        {
            porVenta[item.VentaId].Items.Add(item);
        }

        return ventas;
    }

    public async Task<VentaDto?> ObtenerVentaAsync(long ventaId, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);

        var venta = await connection.QuerySingleOrDefaultAsync<VentaDto>(new CommandDefinition(
            $"{SelectVenta}\nWHERE id = @Id", new { Id = ventaId }, cancellationToken: cancellationToken));
        if (venta is null) return null;

        var detalle = await connection.QueryAsync<VentaDetalleDto>(new CommandDefinition(
            $"{SelectDetalle}\nWHERE d.venta_id = @Id\nORDER BY d.id",
            new { Id = ventaId }, cancellationToken: cancellationToken));
        venta.Items.AddRange(detalle);
        return venta;
    }

    public async Task<long> RegistrarVentaAsync(
        long cajaId, string metodoPago, IReadOnlyList<ItemVenta> items, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        // FOR SHARE: varias ventas pueden ir a la vez, pero el cierre espera a que terminen.
        await BloquearCajaAbiertaAsync(connection, transaction, cajaId, "FOR SHARE", cancellationToken);

        // Primero se validan todos los productos y su stock (bloqueando cada fila, en orden de id).
        var lineas = new List<(ItemVenta Item, decimal Precio, int StockActual)>();
        foreach (var item in items)
        {
            var producto = await connection.QuerySingleOrDefaultAsync<ProductoVenta>(
                new CommandDefinition(
                    "SELECT nombre, precio_venta, activo FROM gomorra.productos WHERE id = @Id FOR SHARE",
                    new { Id = item.ProductoId }, transaction, cancellationToken: cancellationToken))
                ?? throw new NoEncontradoException($"No existe el producto {item.ProductoId}.");

            if (!producto.Activo)
            {
                throw new ConflictoException($"{producto.Nombre} está inactivo y no se puede vender.");
            }

            var stock = await BloquearStockAsync(connection, transaction, item.ProductoId, cancellationToken);
            if (item.Cantidad > stock)
            {
                throw new ConflictoException(
                    $"No hay suficiente stock de {producto.Nombre}: solo hay {Unidades(stock)}.");
            }

            lineas.Add((item, producto.PrecioVenta, stock));
        }

        var total = lineas.Sum(l => l.Item.Cantidad * l.Precio);
        var ventaId = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            INSERT INTO gomorra.ventas (caja_id, metodo_pago, total)
            VALUES (@CajaId, @MetodoPago, @Total)
            RETURNING id
            """,
            new { CajaId = cajaId, MetodoPago = metodoPago, Total = total },
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

        await transaction.CommitAsync(cancellationToken);
        return ventaId;
    }

    public async Task AnularVentaAsync(long ventaId, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var cajaId = await connection.ExecuteScalarAsync<long?>(new CommandDefinition(
            "SELECT caja_id FROM gomorra.ventas WHERE id = @Id",
            new { Id = ventaId }, transaction, cancellationToken: cancellationToken))
            ?? throw new NoEncontradoException($"No existe la venta {ventaId}.");

        // Siempre se bloquea primero la caja y luego lo demás, igual que en las otras operaciones.
        await BloquearCajaAbiertaAsync(
            connection, transaction, cajaId, "FOR UPDATE", cancellationToken,
            mensajeCerrada: "La venta es de una caja cerrada y ya no se puede anular.");

        var venta = await connection.QuerySingleAsync<VentaBloqueada>(new CommandDefinition(
            "SELECT anulada, metodo_pago, total FROM gomorra.ventas WHERE id = @Id FOR UPDATE",
            new { Id = ventaId }, transaction, cancellationToken: cancellationToken));

        if (venta.Anulada)
        {
            throw new ConflictoException("La venta ya está anulada.");
        }

        if (venta.MetodoPago == MetodosPago.Efectivo)
        {
            var caja = (await ObtenerAsync(connection, transaction, cajaId, cancellationToken))!;
            if (venta.Total > caja.EfectivoEsperado)
            {
                throw new ConflictoException(
                    $"No hay suficiente efectivo en caja para devolver la venta: hay {Pesos(caja.EfectivoEsperado)}.");
            }
        }

        var detalle = await connection.QueryAsync<LineaAnulada>(new CommandDefinition(
            "SELECT producto_id, cantidad FROM gomorra.ventas_detalle WHERE venta_id = @Id ORDER BY producto_id",
            new { Id = ventaId }, transaction, cancellationToken: cancellationToken));

        foreach (var linea in detalle)
        {
            var stock = await BloquearStockAsync(connection, transaction, linea.ProductoId, cancellationToken);
            await MoverStockAsync(
                connection, transaction, linea.ProductoId, "anulacion_venta", linea.Cantidad, stock, ventaId, cancellationToken);
        }

        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE gomorra.ventas SET anulada = true, anulada_en = now() WHERE id = @Id",
            new { Id = ventaId }, transaction, cancellationToken: cancellationToken));

        await transaction.CommitAsync(cancellationToken);
    }

    // ---------- Movimientos de efectivo ----------

    public async Task<IReadOnlyList<MovimientoCajaDto>> ListarMovimientosAsync(long cajaId, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        var filas = await connection.QueryAsync<MovimientoCajaDto>(new CommandDefinition(
            $"{SelectMovimiento}\nWHERE caja_id = @CajaId\nORDER BY creado_en DESC, id DESC",
            new { CajaId = cajaId }, cancellationToken: cancellationToken));
        return filas.AsList();
    }

    public async Task<MovimientoCajaDto?> ObtenerMovimientoAsync(long id, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<MovimientoCajaDto>(new CommandDefinition(
            $"{SelectMovimiento}\nWHERE id = @Id", new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<long> RegistrarMovimientoAsync(
        long cajaId, string tipo, decimal monto, string motivo, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await BloquearCajaAbiertaAsync(connection, transaction, cajaId, "FOR UPDATE", cancellationToken);

        if (tipo == TiposMovimientoCaja.Salida)
        {
            var caja = (await ObtenerAsync(connection, transaction, cajaId, cancellationToken))!;
            if (monto > caja.EfectivoEsperado)
            {
                throw new ConflictoException(
                    $"No hay suficiente efectivo en caja: hay {Pesos(caja.EfectivoEsperado)}.");
            }
        }

        var id = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            INSERT INTO gomorra.movimientos_caja (caja_id, tipo, monto, motivo)
            VALUES (@CajaId, @Tipo, @Monto, @Motivo)
            RETURNING id
            """,
            new { CajaId = cajaId, Tipo = tipo, Monto = monto, Motivo = motivo },
            transaction, cancellationToken: cancellationToken));

        await transaction.CommitAsync(cancellationToken);
        return id;
    }

    // ---------- Apoyo ----------

    private static Task<CajaDto?> ObtenerAsync(
        DbConnection connection, DbTransaction? transaction, long id, CancellationToken cancellationToken) =>
        connection.QuerySingleOrDefaultAsync<CajaDto>(new CommandDefinition(
            $"{Select}\nWHERE c.id = @Id", new { Id = id }, transaction, cancellationToken: cancellationToken));

    /// <summary>Bloquea la fila de la caja (FOR SHARE / FOR UPDATE) y exige que exista y esté abierta.</summary>
    private static async Task BloquearCajaAbiertaAsync(
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

    /// <summary>Asegura la fila de inventario del producto, la bloquea y devuelve su stock.</summary>
    private static async Task<int> BloquearStockAsync(
        DbConnection connection, DbTransaction transaction, long productoId, CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            "INSERT INTO gomorra.inventario (producto_id) VALUES (@ProductoId) ON CONFLICT (producto_id) DO NOTHING",
            new { ProductoId = productoId }, transaction, cancellationToken: cancellationToken));

        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT stock FROM gomorra.inventario WHERE producto_id = @ProductoId FOR UPDATE",
            new { ProductoId = productoId }, transaction, cancellationToken: cancellationToken));
    }

    /// <summary>Aplica el cambio al stock (ya bloqueado) y deja el movimiento enlazado a la venta.</summary>
    private static async Task MoverStockAsync(
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

    private sealed class ProductoVenta
    {
        public string Nombre { get; init; } = string.Empty;
        public decimal PrecioVenta { get; init; }
        public bool Activo { get; init; }
    }

    private sealed class VentaBloqueada
    {
        public bool Anulada { get; init; }
        public string MetodoPago { get; init; } = string.Empty;
        public decimal Total { get; init; }
    }

    private sealed class LineaAnulada
    {
        public long ProductoId { get; init; }
        public int Cantidad { get; init; }
    }

    private static string Unidades(int n) => n == 1 ? "1 unidad" : $"{n} unidades";

    private static string Pesos(decimal valor) => valor.ToString("C0", Colombia);
}
