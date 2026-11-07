using Application.Common;
using Npgsql;

namespace Infrastructure.Data;

internal static class PostgresErrores
{
    /// <summary>
    /// Ejecuta la operación y convierte una violación de índice único (23505) en <see cref="ConflictoException"/>,
    /// usando el mensaje asociado al nombre del índice.
    /// </summary>
    public static async Task<T> TraducirUnicosAsync<T>(Func<Task<T>> operacion, IReadOnlyDictionary<string, string> mensajesPorIndice)
    {
        try
        {
            return await operacion();
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            var mensaje = ex.ConstraintName is not null && mensajesPorIndice.TryGetValue(ex.ConstraintName, out var m)
                ? m
                : "Ya existe un registro con esos datos.";
            throw new ConflictoException(mensaje);
        }
    }

    /// <summary>Escapa los comodines de LIKE para buscar el texto tal cual (contiene).</summary>
    public static string PatronContiene(string texto) =>
        "%" + texto.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_") + "%";
}
