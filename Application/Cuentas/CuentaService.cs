using Application.Caja;
using Application.Common;

namespace Application.Cuentas;

public sealed class CuentaService(ICuentaRepository repositorio, CajaService cajas)
{
    public async Task<IReadOnlyList<CuentaDto>> ListarAbiertasAsync(long cajaId, CancellationToken cancellationToken)
    {
        await cajas.ObtenerAsync(cajaId, cancellationToken); // 404 si no existe
        return await repositorio.ListarAbiertasAsync(cajaId, cancellationToken);
    }

    public async Task<CuentaDto> ObtenerAsync(long id, CancellationToken cancellationToken) =>
        await repositorio.ObtenerAsync(id, cancellationToken)
        ?? throw new NoEncontradoException($"No existe la cuenta {id}.");

    public async Task<CuentaDto> CrearAsync(long cajaId, CrearCuentaRequest request, CancellationToken cancellationToken)
    {
        var id = await repositorio.CrearAsync(
            cajaId, NormalizarNombre(request.Nombre), CajaService.AgruparItems(request.Items), cancellationToken);
        return await ObtenerAsync(id, cancellationToken);
    }

    public async Task<CuentaDto> RenombrarAsync(long id, RenombrarCuentaRequest request, CancellationToken cancellationToken)
    {
        await repositorio.RenombrarAsync(id, NormalizarNombre(request.Nombre), cancellationToken);
        return await ObtenerAsync(id, cancellationToken);
    }

    public async Task<CuentaDto> AgregarItemsAsync(long id, AgregarItemsCuentaRequest request, CancellationToken cancellationToken)
    {
        if (request.Items.Count == 0)
        {
            throw new ValidacionException("Agrega al menos un producto.");
        }

        await repositorio.AgregarItemsAsync(id, CajaService.AgruparItems(request.Items), cancellationToken);
        return await ObtenerAsync(id, cancellationToken);
    }

    public async Task<CuentaDto> FijarCantidadAsync(
        long id, long productoId, FijarCantidadRequest request, CancellationToken cancellationToken)
    {
        await repositorio.FijarCantidadAsync(id, productoId, request.Cantidad, cancellationToken);
        return await ObtenerAsync(id, cancellationToken);
    }

    public async Task<VentaDto> PagarAsync(long id, PagarCuentaRequest request, CancellationToken cancellationToken)
    {
        CajaService.ValidarPago(request);
        var ventaId = await repositorio.PagarAsync(id, request.ComoPago(), cancellationToken);
        return await cajas.ObtenerVentaAsync(ventaId, cancellationToken);
    }

    public async Task<CuentaDto> AnularAsync(long id, CancellationToken cancellationToken)
    {
        await repositorio.AnularAsync(id, cancellationToken);
        return await ObtenerAsync(id, cancellationToken);
    }

    /// <summary>Sin espacios de más: "  mesa   4 " → "mesa 4".</summary>
    private static string NormalizarNombre(string nombre)
    {
        var limpio = string.Join(' ', nombre.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return limpio.Length > 0 ? limpio : throw new ValidacionException("El nombre de la cuenta es obligatorio.");
    }
}
