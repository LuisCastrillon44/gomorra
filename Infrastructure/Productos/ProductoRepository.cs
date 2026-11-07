using Application.Abstractions.Data;
using Application.Productos;
using Dapper;
using Domain.Productos;
using Infrastructure.Data;

namespace Infrastructure.Productos;

internal sealed class ProductoRepository(IDbConnectionFactory connectionFactory) : IProductoRepository
{
    private const string Select = """
        SELECT p.id, p.categoria_id, c.nombre AS categoria_nombre, p.nombre, p.presentacion_ml,
               p.precio_venta, p.costo, p.imagen_url, p.activo, p.creado_en, p.actualizado_en
        FROM gomorra.productos p
        JOIN gomorra.categorias c ON c.id = p.categoria_id
        """;

    public async Task<IReadOnlyList<ProductoDto>> ListarAsync(ProductoFiltro filtro, CancellationToken cancellationToken)
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

        var where = condiciones.Count > 0 ? "WHERE " + string.Join(" AND ", condiciones) : string.Empty;
        var sql = $"{Select}\n{where}\nORDER BY lower(p.nombre), p.presentacion_ml";

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        var filas = await connection.QueryAsync<ProductoDto>(
            new CommandDefinition(sql, parametros, cancellationToken: cancellationToken));
        return filas.AsList();
    }

    public async Task<ProductoDto?> ObtenerAsync(long id, CancellationToken cancellationToken)
    {
        var sql = $"{Select}\nWHERE p.id = @Id";

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<ProductoDto>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<long> CrearAsync(Producto producto, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO gomorra.productos
                (categoria_id, nombre, presentacion_ml, precio_venta, costo, imagen_url, activo)
            VALUES
                (@CategoriaId, @Nombre, @PresentacionMl, @PrecioVenta, @Costo, @ImagenUrl, @Activo)
            RETURNING id
            """;

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<long>(
            new CommandDefinition(sql, producto, cancellationToken: cancellationToken));
    }

    public async Task<bool> ActualizarAsync(Producto producto, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE gomorra.productos
            SET categoria_id = @CategoriaId,
                nombre = @Nombre,
                presentacion_ml = @PresentacionMl,
                precio_venta = @PrecioVenta,
                costo = @Costo,
                imagen_url = @ImagenUrl,
                activo = @Activo,
                actualizado_en = now()
            WHERE id = @Id
            """;

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        return await connection.ExecuteAsync(
            new CommandDefinition(sql, producto, cancellationToken: cancellationToken)) > 0;
    }

    public async Task<bool> DesactivarAsync(long id, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE gomorra.productos
            SET activo = false, actualizado_en = now()
            WHERE id = @Id
            """;

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        return await connection.ExecuteAsync(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken)) > 0;
    }
}
