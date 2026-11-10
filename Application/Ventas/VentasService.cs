using Application.Caja;
using Application.Common;

namespace Application.Ventas;

public sealed class VentasService(IVentasRepository repositorio)
{
    public const int LimiteMaximo = 100;
    private const int DiasMaximos = 400;

    public Task<ResumenVentasDto> ResumenAsync(DateTimeOffset desde, DateTimeOffset hasta, CancellationToken cancellationToken) =>
        repositorio.ResumenAsync(Periodo(desde, hasta), cancellationToken);

    public Task<PaginaVentasDto> HistorialAsync(
        DateTimeOffset desde,
        DateTimeOffset hasta,
        long? cajaId,
        string? metodo,
        int limite,
        int offset,
        CancellationToken cancellationToken)
    {
        metodo = string.IsNullOrWhiteSpace(metodo) ? null : metodo.Trim().ToLowerInvariant();
        if (metodo is not null and not (MetodosPago.Efectivo or MetodosPago.Transferencia or MetodosPago.Mixto))
        {
            throw new ValidacionException(MetodosPago.MensajePatron);
        }

        var filtro = new FiltroHistorialVentas(
            Periodo(desde, hasta), cajaId, metodo, Math.Clamp(limite, 1, LimiteMaximo), Math.Max(offset, 0));
        return repositorio.HistorialAsync(filtro, cancellationToken);
    }

    private static PeriodoVentas Periodo(DateTimeOffset desde, DateTimeOffset hasta)
    {
        if (desde >= hasta)
        {
            throw new ValidacionException("La fecha inicial debe ser anterior a la final.");
        }

        if ((hasta - desde).TotalDays > DiasMaximos)
        {
            throw new ValidacionException($"El periodo no puede superar {DiasMaximos} días.");
        }

        return new PeriodoVentas(desde.UtcDateTime, hasta.UtcDateTime);
    }
}
