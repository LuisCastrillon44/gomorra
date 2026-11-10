using Application.Common;

namespace Application.Caja;

public sealed class CajaService(ICajaRepository repositorio)
{
    public const int LimiteHistorialMaximo = 100;

    public Task<CajaDto?> ObtenerAbiertaAsync(CancellationToken cancellationToken) =>
        repositorio.ObtenerAbiertaAsync(cancellationToken);

    public async Task<CajaDto> ObtenerAsync(long id, CancellationToken cancellationToken) =>
        await repositorio.ObtenerAsync(id, cancellationToken)
        ?? throw new NoEncontradoException($"No existe la caja {id}.");

    public Task<IReadOnlyList<CajaDto>> ListarCerradasAsync(int limite, CancellationToken cancellationToken) =>
        repositorio.ListarCerradasAsync(Math.Clamp(limite, 1, LimiteHistorialMaximo), cancellationToken);

    public async Task<CajaDto> AbrirAsync(AbrirCajaRequest request, CancellationToken cancellationToken)
    {
        var id = await repositorio.AbrirAsync(request.BaseInicial!.Value, cancellationToken);
        return await ObtenerAsync(id, cancellationToken);
    }

    public async Task<CajaDto> CerrarAsync(long id, CerrarCajaRequest request, CancellationToken cancellationToken)
    {
        var nota = string.IsNullOrWhiteSpace(request.Nota) ? null : request.Nota.Trim();
        await repositorio.CerrarAsync(id, request.EfectivoContado!.Value, nota, cancellationToken);
        return await ObtenerAsync(id, cancellationToken);
    }

    public async Task<IReadOnlyList<VentaDto>> ListarVentasAsync(long cajaId, CancellationToken cancellationToken)
    {
        await ObtenerAsync(cajaId, cancellationToken); // 404 si no existe
        return await repositorio.ListarVentasAsync(cajaId, cancellationToken);
    }

    public async Task<VentaDto> ObtenerVentaAsync(long ventaId, CancellationToken cancellationToken) =>
        await repositorio.ObtenerVentaAsync(ventaId, cancellationToken)
        ?? throw new NoEncontradoException($"No existe la venta {ventaId}.");

    public async Task<VentaDto> RegistrarVentaAsync(long cajaId, RegistrarVentaRequest request, CancellationToken cancellationToken)
    {
        if (request.Items.Count == 0)
        {
            throw new ValidacionException("La venta debe tener al menos un producto.");
        }

        ValidarPago(request);

        var ventaId = await repositorio.RegistrarVentaAsync(cajaId, request.ComoPago(), AgruparItems(request.Items), cancellationToken);
        return await ObtenerVentaAsync(ventaId, cancellationToken);
    }

    /// <summary>
    /// Un producto repetido se suma en una sola línea; el orden por id evita bloqueos cruzados entre ventas.
    /// </summary>
    public static List<ItemVenta> AgruparItems(IEnumerable<ItemVentaRequest> items) =>
        items
            .GroupBy(i => i.ProductoId)
            .Select(g => new ItemVenta(g.Key, g.Sum(i => i.Cantidad)))
            .OrderBy(i => i.ProductoId)
            .ToList();

    /// <summary>
    /// Reglas del pago que no dependen del total: en mixto van los dos montos; si no, ninguno.
    /// Que el mixto sume el total se valida al registrar la venta (el total lo calcula el servidor).
    /// </summary>
    public static void ValidarPago(PagoRequest request)
    {
        var mixto = request.MetodoPago == MetodosPago.Mixto;
        if (mixto && (request.PagoEfectivo is null || request.PagoTransferencia is null))
        {
            throw new ValidacionException("En el pago mixto indica cuánto se paga en efectivo y cuánto por transferencia.");
        }

        if (!mixto && (request.PagoEfectivo is not null || request.PagoTransferencia is not null))
        {
            throw new ValidacionException("Los montos por medio de pago solo se indican en el pago mixto.");
        }
    }

    public async Task<VentaDto> AnularVentaAsync(long ventaId, CancellationToken cancellationToken)
    {
        await repositorio.AnularVentaAsync(ventaId, cancellationToken);
        return await ObtenerVentaAsync(ventaId, cancellationToken);
    }

    public async Task<IReadOnlyList<MovimientoCajaDto>> ListarMovimientosAsync(long cajaId, CancellationToken cancellationToken)
    {
        await ObtenerAsync(cajaId, cancellationToken); // 404 si no existe
        return await repositorio.ListarMovimientosAsync(cajaId, cancellationToken);
    }

    public async Task<MovimientoCajaDto> RegistrarMovimientoAsync(
        long cajaId, RegistrarMovimientoCajaRequest request, CancellationToken cancellationToken)
    {
        var motivo = request.Motivo.Trim();
        if (motivo.Length == 0)
        {
            throw new ValidacionException("El motivo es obligatorio.");
        }

        var id = await repositorio.RegistrarMovimientoAsync(
            cajaId, request.Tipo, request.Monto!.Value, motivo, cancellationToken);

        return await repositorio.ObtenerMovimientoAsync(id, cancellationToken)
            ?? throw new NoEncontradoException($"No existe el movimiento {id}.");
    }
}
