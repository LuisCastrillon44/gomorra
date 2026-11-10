using Application.Autenticacion;
using Application.Caja;
using Application.Usuarios;
using Application.Categorias;
using Application.Cuentas;
using Application.Inventario;
using Application.Productos;
using Application.Ventas;
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
        services.AddScoped<CuentaService>();
        services.AddScoped<VentasService>();
        services.AddScoped<UsuarioService>();
        services.AddScoped<AutenticacionService>();

        return services;
    }
}
