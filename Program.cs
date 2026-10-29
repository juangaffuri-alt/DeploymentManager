using Microsoft.AspNetCore.Http.Features;
using PasadorScript.Extensions;
using PasadorScript.Infrastructure.DependencyInjection;
using PasadorScript.Presentation.Middleware;
using Serilog;
using LoggerConfiguration = PasadorScript.Infrastructure.Logging.LoggerConfiguration;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration
    .AddSqlTemplateSettings();  

// Configuración de Logging
Log.Logger = LoggerConfiguration.CreateLogger(builder.Configuration);
builder.Host.UseSerilog();

// Registrar servicios por capa
builder.Services.AddApplicationServices(builder.Configuration);
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddPresentationServices(builder.Configuration);
builder.Services.AddWebServices();

builder.Services.Configure<FormOptions>(options =>
{
    options.ValueLengthLimit = int.MaxValue;
    options.MultipartBodyLengthLimit = int.MaxValue;
    options.ValueCountLimit = int.MaxValue;
});

var app = builder.Build();

// Configurar pipeline HTTP
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseSession();
app.UseMiddleware<TokenExpirationMiddleware>();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

try
{
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Error fatal al iniciar la aplicación");
    throw;
}
finally
{
    Log.CloseAndFlush();
}