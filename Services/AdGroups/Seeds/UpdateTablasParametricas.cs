using PasadorScript.DbModels;
namespace PasadorScript.Services.AdGroups.Seeds
{
    public static class SeedDataExtension
    {
        public static void SeedTablasParametricas(this IServiceCollection services)
        {
            var serviceProvider = services.BuildServiceProvider();
            var context = serviceProvider.GetRequiredService<NuevoP229Context>();

            // Asegurar que las tablas existen
            context.Database.EnsureCreated();
            bool cambios = false;

            // No hacer nada si ya hay tareas
            
            if (!context.TiposCambios.Any())
            {
                cambios = true;
                context.TiposCambios.Add(new TiposCambio() { Descripcion = "GDEM" });
                context.TiposCambios.Add(new TiposCambio() { Descripcion = "SIT" });
                context.TiposCambios.Add(new TiposCambio() { Descripcion = "Otro/s" });
            }

            if (!context.Entornos.Any())
            {
                cambios = true;
                context.Entornos.Add(new Entorno() { Nombre = "DF", Descripcion = "Bantotal DF", Color="info-gradient", Icono="fas fa-database", Activo=true, FechaCreacion=DateTime.Parse("2026-02-13 00:00:00.0000000") });
                context.Entornos.Add(new Entorno() { Nombre = "QA", Descripcion = "Bantotal QA", Color="secondary-gradient", Icono="fas fa-database", Activo=true, FechaCreacion=DateTime.Parse("2026-02-13 00:00:00.0000000") });
            }

            if (cambios)
            {
                context.SaveChanges();
            }
        }
    }
}
