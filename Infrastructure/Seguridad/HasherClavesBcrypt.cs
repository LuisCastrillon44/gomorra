using Application.Abstractions.Seguridad;

namespace Infrastructure.Seguridad;

internal sealed class HasherClavesBcrypt : IHasherClaves
{
    private const int Costo = 11;

    public string Hash(string clave) => BCrypt.Net.BCrypt.HashPassword(clave, Costo);

    public bool Verificar(string clave, string hash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(clave, hash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            return false; // Hash corrupto en BD: se trata como clave incorrecta.
        }
    }
}
