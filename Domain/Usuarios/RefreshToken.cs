namespace Domain.Usuarios;

public sealed class RefreshToken
{
    public long Id { get; set; }
    public long UsuarioId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiraEn { get; set; }
    public DateTime? RevocadoEn { get; set; }
    public long? ReemplazadoPor { get; set; }
    public DateTime CreadoEn { get; set; }
}
