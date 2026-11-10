using Api;
using Application;
using Application.Usuarios;
using Infrastructure;
using Infrastructure.Database;

const string FrontendCorsPolicy = "Frontend";

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod());
});

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSeguridad(
    Infrastructure.DependencyInjection.GetJwtOptions(builder.Configuration),
    Infrastructure.DependencyInjection.GetAdminOptions(builder.Configuration));

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ManejadorExcepciones>();

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApiConBearer();

var app = builder.Build();

DatabaseMigrator.Migrate(Infrastructure.DependencyInjection.GetConnectionString(app.Configuration));

// Crea el administrador configurado en Admin:Usuario / Admin:Clave (user-secrets o variables de entorno) si todavía no existe.
await using (var scope = app.Services.CreateAsyncScope())
{
    if (await scope.ServiceProvider.GetRequiredService<UsuarioService>().SembrarAdministradorAsync(CancellationToken.None))
    {
        app.Logger.LogInformation("Administrador inicial creado.");
    }
}

app.UseExceptionHandler();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}
else
{
    // En desarrollo el front llama a http://localhost:5286. Si redirigiéramos a https, el navegador
    // rechazaría la respuesta 307 (no lleva cabeceras CORS) y las llamadas fallarían con "CORS error".
    app.UseHttpsRedirection();
}

app.UseCors(FrontendCorsPolicy);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapHealthChecks("/health").AllowAnonymous();

app.Run();
