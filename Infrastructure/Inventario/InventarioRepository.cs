using Application.Abstractions.Data;
using Application.Inventario;
using Dapper;
using Infrastructure.Data;

namespace Infrastructure.Inventario;

internal sealed class InventarioRepository(IDbConnectionFactory connectionFactory) : IInventarioRepository
{
    // Un producto sin fila en inventario cuenta como stock 0 y mínimo 0.
    private const string Select = """
        SELECT p.id AS producto_id, p.nombre, p.categoria_id, c.nombre AS categoria_nombre,
               p.presentacion_ml, p.imagen_url, p.precio_venta, p.activo,
               COALESCE(i.stock, 0) AS stock,
               COALESCE(i.stock_minimo, 0) AS stock_minimo,
               COALESCE(ec.en_cuentas, 0) AS en_cuentas,
               COALESCE(i.stock, 0) - COALESCE(ec.en_cuentas, 0) AS disponible,
               (COALESCE(i.stock, 0) = 0
                OR (COALESCE(i.stock_minimo, 0) > 0 AND i.stock <= i.stock_minimo)) AS por_reponer
        FROM gomorra.productos p
        JOIN gomorra.categorias c ON c.id = p.categoria_id
        LEFT JOIN gomorra.inventario i ON i.producto_id = p.id
        LEFT JOIN LATERAL (
            SELECT SUM(d.cantidad)::int AS en_cuentas
            FROM gomorra.cuentas_detalle d
            JOIN gomorra.cuentas cu ON cu.id = d.cuenta_id
            WHERE cu.estado = 'abierta' AND d.producto_id = p.id
        ) ec ON true
        """;

    public async Task<IReadOnlyList<InventarioItemDto>> ListarAsync(InventarioFiltro filtro, CancellationToken cancellationToken)
    {
        var condiciones = new List<string>();
        var parametros = new DynamicParameters();

        if (!filtro.IncluirInactivos)
        {
            condiciones.Add("p.activo");
        }

        if (filtro.CategoriaId is { } categoriaId)
        {
            condiciones.Add("p.categoria_id = @CategoriaId");
            parametros.Add("CategoriaId", categoriaId);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Buscar))
        {
            condiciones.Add("p.nombre ILIKE @Patron");
            parametros.Add("Patron", PostgresErrores.PatronContiene(filtro.Buscar.Trim()));
        }

        if (filtro.SoloPorReponer)
        {
            condiciones.Add("(COALESCE(i.stock, 0) = 0 OR (COALESCE(i.stock_minimo, 0) > 0 AND i.stock <= i.stock_minimo))");
        }

        var where = condiciones.Count > 0 ? "WHERE " + string.Join(" AND ", condiciones) : string.Empty;
        var sql = $"{Select}\n{where}\nORDER BY lower(p.nombre), p.presentacion_ml";

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        var filas = await connection.QueryAsync<InventarioItemDto>(
            new CommandDefinition(sql, parametros, cancellationToken: cancellationToken));
        return filas.AsList();
    }

    public async Task<InventarioItemDto?> ObtenerAsync(long productoId, CancellationToken cancellationToken)
    {
        var sql = $"{Select}\nWHERE p.id = @ProductoId";

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<InventarioItemDto>(
            new CommandDefinition(sql, new { ProductoId = productoId }, cancellationToken: cancellationToken));
    }

    public async Task ActualizarStockMinimoAsync(long productoId, int stockMinimo, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO gomorra.inventario (producto_id, stock_minimo)
            VALUES (@ProductoId, @StockMinimo)
            ON CONFLICT (producto_id) DO UPDATE
            SET stock_minimo = EXCLUDED.stock_minimo, actualizado_en = now()
            """;

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            sql, new { ProductoId = productoId, StockMinimo = stockMinimo }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<MovimientoDto>> ListarMovimientosAsync(long productoId, int limite, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id, tipo, cantidad, stock_resultante, costo_unitario, creado_en
            FROM gomorra.movimientos_inventario
            WHERE producto_id = @ProductoId
            ORDER BY creado_en DESC, id DESC
            LIMIT @Limite
            """;

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        var filas = await connection.QueryAsync<MovimientoDto>(new CommandDefinition(
            sql, new { ProductoId = productoId, Limite = limite }, cancellationToken: cancellationToken));
        return filas.AsList();
    }

    public async Task RegistrarMovimientoAsync(
        long productoId,
        string tipo,
        Func<int, int> calcularDelta,
        decimal? costoUnitario,
        CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        // Asegura la fila y la bloquea: dos movimientos simultáneos del mismo producto se ejecutan en fila.
        await connection.ExecuteAsync(new CommandDefinition(
            "INSERT INTO gomorra.inventario (producto_id) VALUES (@ProductoId) ON CONFLICT (producto_id) DO NOTHING",
            new { ProductoId = productoId }, transaction, cancellationToken: cancellationToken));

        var stockActual = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT stock FROM gomorra.inventario WHERE producto_id = @ProductoId FOR UPDATE",
            new { ProductoId = productoId }, transaction, cancellationToken: cancellationToken));

        // Si las reglas rechazan el movimiento, la excepción sale de aquí y la transacción se revierte al liberarse.
        var delta = calcularDelta(stockActual);
        var stockResultante = stockActual + delta;

        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE gomorra.inventario
            SET stock = @StockResultante, actualizado_en = now()
            WHERE producto_id = @ProductoId
            """,
            new { ProductoId = productoId, StockResultante = stockResultante }, transaction, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO gomorra.movimientos_inventario (producto_id, tipo, cantidad, stock_resultante, costo_unitario)
            VALUES (@ProductoId, @Tipo, @Cantidad, @StockResultante, @CostoUnitario)
            """,
            new
            {
                ProductoId = productoId,
                Tipo = tipo,
                Cantidad = delta,
                StockResultante = stockResultante,
                CostoUnitario = costoUnitario,
            },
            transaction, cancellationToken: cancellationToken));

        await transaction.CommitAsync(cancellationToken);
    }
}
