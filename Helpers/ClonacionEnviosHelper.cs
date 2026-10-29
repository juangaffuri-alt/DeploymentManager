using System.Text.Json;
using PasadorScript.ViewModels;

namespace PasadorScript.Helpers
{
    /// <summary>
    /// Normaliza el payload de archivos persistido de un envío para poder reutilizarlo
    /// como payload de un envío nuevo (clon), que se sube a una rama recién creada.
    /// </summary>
    public static class ClonacionEnviosHelper
    {
        private const string OriginDevops = "devops";
        private const string EstadoNuevo = "nuevo";
        private const string EstadoModificado = "modificado";
        private const string EstadoEliminado = "eliminado";

        /// <summary>
        /// Deserializa el JSON de archivos del envío original y lo deja listo para el push del clon:
        /// descarta los archivos indicados y los eliminados, y reasigna el estado según dónde
        /// se va a escribir cada archivo (carpeta de la rama nueva o raíz del repo).
        /// </summary>
        /// <param name="jsonOriginal">JSON persistido (<c>Script.Files</c> o <c>EnviosDlya.CommitListJson</c>).</param>
        /// <param name="archivosAExcluir">Nombres de archivo que regenera el flujo de liberación (por ejemplo <c>timestamps.json</c>).</param>
        public static List<VirtualFileViewModel> NormalizarParaClon(string? jsonOriginal, params string[] archivosAExcluir)
        {
            if (string.IsNullOrWhiteSpace(jsonOriginal))
                return [];

            var opciones = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var archivos = JsonSerializer.Deserialize<List<VirtualFileViewModel>>(jsonOriginal, opciones) ?? [];

            var excluidos = new HashSet<string>(archivosAExcluir ?? [], StringComparer.OrdinalIgnoreCase);
            var normalizados = new List<VirtualFileViewModel>();

            foreach (var archivo in archivos)
            {
                if (archivo.Estado == EstadoEliminado || string.IsNullOrEmpty(archivo.Content))
                    continue;

                if (excluidos.Contains(NombreArchivo(archivo.Name)))
                    continue;

                archivo.Estado = EstadoParaClon(archivo);
                normalizados.Add(archivo);
            }

            return normalizados;
        }

        /// <summary>
        /// La carpeta de la rama nueva está vacía, así que todo lo que va ahí se agrega.
        /// Lo que va a la raíz del repo ya existe en la rama base, así que se edita.
        /// </summary>
        private static string EstadoParaClon(VirtualFileViewModel archivo)
        {
            var vaALaCarpetaDeLaRama = archivo.Origin != OriginDevops && archivo.Metadata?.IsStructure != true;
            return vaALaCarpetaDeLaRama || archivo.Estado == EstadoNuevo ? EstadoNuevo : EstadoModificado;
        }

        private static string NombreArchivo(string? nombreCompleto)
        {
            if (string.IsNullOrEmpty(nombreCompleto))
                return string.Empty;

            var normalizado = nombreCompleto.Replace('\\', '/');
            var separador = normalizado.LastIndexOf('/');
            return separador >= 0 ? normalizado[(separador + 1)..] : normalizado;
        }
    }
}
