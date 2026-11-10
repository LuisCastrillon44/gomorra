using System.ComponentModel.DataAnnotations;

namespace Application.Usuarios;

public sealed class UsuarioDto
{
    public long Id { get; init; }
    public string Usuario { get; init; } = string.Empty;
    public string? Email { get; init; }
    public string Nombre { get; init; } = string.Empty;
    public bool Activo { get; init; }
    public bool EsAdministrador { get; set; }
    public DateTime? UltimoAcceso { get; init; }
    public DateTime CreadoEn { get; init; }
    public DateTime ActualizadoEn { get; init; }
}

public sealed class CrearUsuarioRequest
{
    [Required(ErrorMessage = "El usuario es obligatorio.")]
    [RegularExpression(Reglas.PatronUsuario, ErrorMessage = Reglas.MensajeUsuario)]
    public string Usuario { get; init; } = string.Empty;

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(80, ErrorMessage = "El nombre admite máximo 80 caracteres.")]
    public string Nombre { get; init; } = string.Empty;

    [EmailAddress(ErrorMessage = "El correo no es válido.")]
    [StringLength(120, ErrorMessage = "El correo admite máximo 120 caracteres.")]
    public string? Email { get; init; }

    [Required(ErrorMessage = "La clave es obligatoria.")]
    [StringLength(Reglas.ClaveMaximo, MinimumLength = Reglas.ClaveMinimo, ErrorMessage = Reglas.MensajeClave)]
    public string Clave { get; init; } = string.Empty;
}

public sealed class ActualizarUsuarioRequest
{
    [Required(ErrorMessage = "El usuario es obligatorio.")]
    [RegularExpression(Reglas.PatronUsuario, ErrorMessage = Reglas.MensajeUsuario)]
    public string Usuario { get; init; } = string.Empty;

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(80, ErrorMessage = "El nombre admite máximo 80 caracteres.")]
    public string Nombre { get; init; } = string.Empty;

    [EmailAddress(ErrorMessage = "El correo no es válido.")]
    [StringLength(120, ErrorMessage = "El correo admite máximo 120 caracteres.")]
    public string? Email { get; init; }

    public bool Activo { get; init; } = true;
}

public sealed class CambiarClaveRequest
{
    [Required(ErrorMessage = "La clave es obligatoria.")]
    [StringLength(Reglas.ClaveMaximo, MinimumLength = Reglas.ClaveMinimo, ErrorMessage = Reglas.MensajeClave)]
    public string Clave { get; init; } = string.Empty;
}

internal static class Reglas
{
    /// <summary>3 a 40 letras, números, punto, guion o guion bajo; sin espacios.</summary>
    public const string PatronUsuario = @"^\s*[A-Za-z0-9._-]{3,40}\s*$";

    public const string MensajeUsuario =
        "El usuario debe tener entre 3 y 40 caracteres: letras, números, punto, guion o guion bajo, sin espacios.";

    public const int ClaveMinimo = 4;

    // BCrypt solo usa los primeros 72 bytes.
    public const int ClaveMaximo = 72;

    public const string MensajeClave = "La clave debe tener entre 4 y 72 caracteres.";
}
