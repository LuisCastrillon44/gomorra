namespace Domain.Usuarios;

public sealed class Usuario
{
    public long Id { get; set; }

    /// <summary>Nombre de usuario para iniciar sesión (columna 'usuario').</summary>
    public string NombreUsuario { get; set; } = string.Empty;

    public string? Email { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string ClaveHash { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;
    public DateTime? UltimoAcceso { get; set; }
    public DateTime CreadoEn { get; set; }
    public DateTime ActualizadoEn { get; set; }
}
