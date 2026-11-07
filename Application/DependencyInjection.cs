using Application.Caja;
using Application.Categorias;
using Application.Inventario;
using Application.Productos;
using Microsoft.Extensions.DependencyInjection;

namespace Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<CategoriaService>();
        services.AddScoped<ProductoService>();
        services.AddScoped<InventarioService>();
        services.AddScoped<CajaService>();

        return services;
    }
}
