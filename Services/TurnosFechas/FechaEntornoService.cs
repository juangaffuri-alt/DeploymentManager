// Services/FechaEntornoService.cs
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using PasadorScript.Services.TurnosFechas;
using System.Data;

namespace PasadorScript.Services
{
    public class FechaEntornoService : IFechaEntornoService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<FechaEntornoService> _logger;

        public FechaEntornoService(IConfiguration configuration, ILogger<FechaEntornoService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<DateTime?> ObtenerFechaActualAsync(string entornoNombre)
        {
            string connectionStringName = entornoNombre.ToUpper() switch
            {
                "DF" => "ConexionDF",
                "QA" => "ConexionQA",
                _ => null
            };

            if (connectionStringName == null)
            {
                _logger.LogWarning("No hay connection string configurada para el entorno {Entorno}", entornoNombre);
                return null;
            }

            var connectionString = _configuration.GetConnectionString(connectionStringName);
            if (string.IsNullOrEmpty(connectionString))
            {
                _logger.LogWarning("Connection string {Name} no encontrada", connectionStringName);
                return null;
            }

            try
            {
                using var conn = new SqlConnection(connectionString);
                using var cmd = new SqlCommand("SELECT Pgfape FROM fst017", conn);
                cmd.CommandType = CommandType.Text;
                cmd.CommandTimeout = 5; // Timeout corto para no bloquear

                await conn.OpenAsync();
                var result = await cmd.ExecuteScalarAsync();

                if (result != null && result != DBNull.Value)
                {
                    // Asumiendo que Pgfape es una fecha (datetime, date, etc.)
                    return Convert.ToDateTime(result);
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener fecha actual del entorno {Entorno}", entornoNombre);
                return null;
            }
        }
    }
}