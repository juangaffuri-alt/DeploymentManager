namespace PasadorScript.Services.TurnosFechas
{
    public interface IFechaEntornoService
    {
        Task<DateTime?> ObtenerFechaActualAsync(string entornoNombre);
    }
}
