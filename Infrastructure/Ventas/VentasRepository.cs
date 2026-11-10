using Application.Abstractions.Data;
using Application.Caja;
using Application.Ventas;
using Dapper;

namespace Infrastructure.Ventas;

internal sealed class VentasRepository(IDbConnectionFactory connectionFactory) : IVentasRepository
{
    // Turnos del periodo: cajas abiertas en [Desde, Hasta), incluida la que siga abierta.
    private const string CajasDelPeriodo =
        "SELECT id FROM gomorra.cajas WHERE abierta_en >= @Desde AND abierta_en < @Hasta";

    public async Task<ResumenVentasDto> ResumenAsync(PeriodoVentas periodo, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        var parametros = new { periodo.Desde, periodo.Hasta };

        var totales = await connection.QuerySingleAsync<Totales>(new CommandDefinition(
            $"""
            SELECT COALESCE(SUM(total) FILTER (WHERE NOT anulada), 0) AS total_vendido,
                   (COUNT(*) FILTER (WHERE NOT anulada))::int AS cantidad_ventas,
                   COALESCE(SUM(pago_efectivo) FILTER (WHERE NOT anulada), 0) AS efectivo,
                   COALESCE(SUM(pago_transferencia) FILTER (WHERE NOT anulada), 0) AS transferencia,
                   (COUNT(*) FILTER (WHERE anulada))::int AS cantidad_anuladas,
                   COALESCE(SUM(total) FILTER (WHERE anulada), 0) AS total_anulado
            FROM gomorra.ventas
            WHERE caja_id IN ({CajasDelPeriodo})
            """,
            parametros, cancellationToken: cancellationToken));

        var turnos = await connection.QueryAsync<TurnoVentasDto>(new CommandDefinition(
            """
            SELECT c.id AS caja_id, c.estado, c.abierta_en, c.cerrada_en,
                   COALESCE(SUM(v.total) FILTER (WHERE NOT v.anulada), 0) AS total,
                   (COUNT(v.id) FILTER (WHERE NOT v.anulada))::int AS cantidad_ventas
            FROM gomorra.cajas c
            LEFT JOIN gomorra.ventas v ON v.caja_id = c.id
            WHERE c.abierta_en >= @Desde AND c.abierta_en < @Hasta
            GROUP BY c.id
            ORDER BY c.abierta_en, c.id
            """,
            parametros, cancellationToken: cancellationToken));

        var top = await connection.QueryAsync<ProductoVendidoDto>(new CommandDefinition(
            $"""
            SELECT p.id AS producto_id, p.nombre, p.presentacion_ml,
                   SUM(d.cantidad)::int AS unidades, SUM(d.subtotal) AS total
            FROM gomorra.ventas_detalle d
            JOIN gomorra.ventas v ON v.id = d.venta_id
            JOIN gomorra.productos p ON p.id = d.producto_id
            WHERE NOT v.anulada AND v.caja_id IN ({CajasDelPeriodo})
            GROUP BY p.id
            ORDER BY SUM(d.subtotal) DESC, SUM(d.cantidad) DESC, p.nombre
            LIMIT 5
            """,
            parametros, cancellationToken: cancellationToken));

        return new ResumenVentasDto
        {
            TotalVendido = totales.TotalVendido,
            CantidadVentas = totales.CantidadVentas,
            TicketPromedio = totales.CantidadVentas > 0 ? Math.Round(totales.TotalVendido / totales.CantidadVentas, 0) : 0,
            Efectivo = totales.Efectivo,
            Transferencia = totales.Transferencia,
            CantidadAnuladas = totales.CantidadAnuladas,
            TotalAnulado = totales.TotalAnulado,
            Turnos = turnos.AsList(),
            TopProductos = top.AsList(),
        };
    }

    public async Task<PaginaVentasDto> HistorialAsync(FiltroHistorialVentas filtro, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);

        // Se pide una de más para saber si hay otra página.
        var ventas = (await connection.QueryAsync<VentaDto>(new CommandDefinition(
            $"""
            SELECT id, caja_id, metodo_pago, total, pago_efectivo, pago_transferencia, anulada, anulada_en, creado_en
            FROM gomorra.ventas
            WHERE caja_id IN ({CajasDelPeriodo})
              AND (@CajaId::bigint IS NULL OR caja_id = @CajaId)
              AND (@Metodo::text IS NULL OR metodo_pago = @Metodo)
            ORDER BY creado_en DESC, id DESC
            LIMIT @Limite OFFSET @Offset
            """,
            new
            {
                filtro.Periodo.Desde,
                filtro.Periodo.Hasta,
                filtro.CajaId,
                filtro.Metodo,
                Limite = filtro.Limite + 1,
                filtro.Offset,
            },
            cancellationToken: cancellationToken))).AsList();

        var hayMas = ventas.Count > filtro.Limite;
        if (hayMas)
        {
            ventas.RemoveAt(ventas.Count - 1);
        }

        if (ventas.Count > 0)
        {
            var detalle = await connection.QueryAsync<VentaDetalleDto>(new CommandDefinition(
                """
                SELECT d.venta_id, d.producto_id, p.nombre AS producto_nombre, p.presentacion_ml,
                       d.cantidad, d.precio_unitario, d.subtotal
                FROM gomorra.ventas_detalle d
                JOIN gomorra.productos p ON p.id = d.producto_id
                WHERE d.venta_id = ANY(@Ids)
                ORDER BY d.id
                """,
                new { Ids = ventas.Select(v => v.Id).ToArray() }, cancellationToken: cancellationToken));

            var porVenta = ventas.ToDictionary(v => v.Id);
            foreach (var item in detalle)
            {
                porVenta[item.VentaId].Items.Add(item);
            }
        }

        return new PaginaVentasDto { Items = ventas, HayMas = hayMas };
    }

    private sealed class Totales
    {
        public decimal TotalVendido { get; init; }
        public int CantidadVentas { get; init; }
        public decimal Efectivo { get; init; }
        public decimal Transferencia { get; init; }
        public int CantidadAnuladas { get; init; }
        public decimal TotalAnulado { get; init; }
    }
}
