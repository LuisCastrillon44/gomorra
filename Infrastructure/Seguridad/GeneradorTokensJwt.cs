using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Application.Abstractions.Seguridad;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Infrastructure.Seguridad;

internal sealed class GeneradorTokensJwt(JwtOptions opciones) : IGeneradorTokens
{
    private readonly SigningCredentials credenciales = new(
        new SymmetricSecurityKey(Encoding.UTF8.GetBytes(opciones.Clave)), SecurityAlgorithms.HmacSha256);

    private readonly JsonWebTokenHandler handler = new();

    public TokenAcceso CrearAccessToken(long usuarioId, string usuario, string nombre)
    {
        var ahora = DateTime.UtcNow;
        var expira = ahora.AddMinutes(opciones.MinutosAcceso);

        var token = handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = opciones.Emisor,
            Audience = opciones.Audiencia,
            IssuedAt = ahora,
            NotBefore = ahora,
            Expires = expira,
            SigningCredentials = credenciales,
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = usuarioId.ToString(),
                [JwtRegisteredClaimNames.PreferredUsername] = usuario,
                [JwtRegisteredClaimNames.Name] = nombre,
                [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString("N"),
            },
        });

        return new TokenAcceso(token, expira);
    }

    public string CrearRefreshToken() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(64));

    public string HashRefreshToken(string refreshToken) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));
}
