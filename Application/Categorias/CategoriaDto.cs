using System.ComponentModel.DataAnnotations;

namespace Application.Categorias;

public sealed class CategoriaDto
{
    public long Id { get; init; }
    public string Nombre { get; init; } = string.Empty;
    public bool Activo { get; init; }
    public DateTime CreadoEn { get; init; }
    public DateTime ActualizadoEn { get; init; }
}

public sealed class GuardarCategoriaRequest
{
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(80, ErrorMessage = "El nombre admite máximo 80 caracteres.")]
    public string Nombre { get; init; } = string.Empty;

    public bool Activo { get; init; } = true;
}
