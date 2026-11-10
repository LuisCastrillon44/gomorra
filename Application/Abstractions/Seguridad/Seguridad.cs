namespace Application.Abstractions.Seguridad;

/// <summary>Hashea y verifica claves de usuario.</summary>
public interface IHasherClaves
{
    string Hash(string clave);
    bool Verificar(string clave, string hash);
}

/// <summary>Genera los tokens de sesión.</summary>
public interface IGeneradorTokens
{
    /// <summary>Access token JWT firmado con los claims sub, preferred_username y name.</summary>
    TokenAcceso CrearAccessToken(long usuarioId, string usuario, string nombre);

    /// <summary>Refresh token aleatorio (opaco) para enviar al cliente.</summary>
    string CrearRefreshToken();

    /// <summary>Hash que se guarda en BD en lugar del refresh token.</summary>
    string HashRefreshToken(string refreshToken);
}

public sealed record TokenAcceso(string Token, DateTime ExpiraEn);

/// <summary>Configuración de los tokens (sección "Jwt").</summary>
public sealed class JwtOptions
{
    public string Emisor { get; init; } = "gomorra-api";
    public string Audiencia { get; init; } = "gomorra-ui";
    public string Clave { get; init; } = string.Empty;
    public int MinutosAcceso { get; init; } = 15;
    public int DiasRefresh { get; init; } = 7;
}

/// <summary>
/// Administrador semilla (sección "Admin"). Se crea al arrancar si no existe y, mientras no haya permisos,
/// es el único que puede administrar usuarios.
/// </summary>
public sealed class AdminOptions
{
    public string? Usuario { get; init; }
    public string? Clave { get; init; }
    public string Nombre { get; init; } = "Administrador";

    public bool EsAdministrador(string? usuario) =>
        !string.IsNullOrWhiteSpace(Usuario) && string.Equals(usuario, Usuario.Trim(), StringComparison.OrdinalIgnoreCase);
}
