using System.Text;
using Application.Abstractions.Seguridad;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

namespace Api;

public static class Politicas
{
    /// <summary>Temporal hasta tener permisos: solo el usuario configurado en 'Admin:Usuario'.</summary>
    public const string Administrador = "Administrador";
}

internal static class Seguridad
{
    /// <summary>
    /// JWT Bearer en toda la API: cualquier endpoint sin [AllowAnonymous] exige un access token válido.
    /// </summary>
    public static IServiceCollection AddSeguridad(this IServiceCollection services, JwtOptions jwt, AdminOptions admin)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // Conserva los nombres de claim del token (sub, preferred_username, name) sin mapearlos a los de WS-Federation.
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Emisor,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audiencia,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Clave)),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = JwtRegisteredClaimNames.Name,
                };
            });

        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(Politicas.Administrador, policy => policy
                .RequireAuthenticatedUser()
                .RequireAssertion(ctx => admin.EsAdministrador(ctx.User.FindFirst(JwtRegisteredClaimNames.PreferredUsername)?.Value)));

        return services;
    }

    /// <summary>Declara el esquema Bearer en el documento OpenAPI.</summary>
    public static IServiceCollection AddOpenApiConBearer(this IServiceCollection services) =>
        services.AddOpenApi(options => options.AddDocumentTransformer((document, _, _) =>
        {
            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
            document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "Access token obtenido en POST /api/auth/login.",
            };
            document.Security = [new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("Bearer", document)] = [] }];
            return Task.CompletedTask;
        }));
}
