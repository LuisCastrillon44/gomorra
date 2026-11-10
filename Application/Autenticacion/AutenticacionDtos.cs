using System.ComponentModel.DataAnnotations;

namespace Application.Autenticacion;

public sealed class LoginRequest
{
    [Required(ErrorMessage = "El usuario es obligatorio.")]
    public string Usuario { get; init; } = string.Empty;

    [Required(ErrorMessage = "La clave es obligatoria.")]
    public string Clave { get; init; } = string.Empty;
}

public sealed class RefrescarRequest
{
    [Required(ErrorMessage = "El refresh token es obligatorio.")]
    public string RefreshToken { get; init; } = string.Empty;
}

public sealed record UsuarioSesionDto(long Id, string Usuario, string Nombre, string? Email, bool EsAdministrador);

public sealed record SesionDto(string AccessToken, DateTime ExpiraEn, string RefreshToken, UsuarioSesionDto Usuario);
