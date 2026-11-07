namespace Application.Common;

/// <summary>El recurso pedido no existe. La API responde 404.</summary>
public sealed class NoEncontradoException(string mensaje) : Exception(mensaje);

/// <summary>La operación choca con el estado actual de los datos (duplicados, dependencias). La API responde 409.</summary>
public sealed class ConflictoException(string mensaje) : Exception(mensaje);

/// <summary>La petición no cumple una regla de negocio. La API responde 400.</summary>
public sealed class ValidacionException(string mensaje) : Exception(mensaje);
