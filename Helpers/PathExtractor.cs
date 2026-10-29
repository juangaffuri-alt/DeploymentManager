using System;
using System.IO;
using System.Linq;

namespace PasadorScript.Helpers
{
    public static class PathExtractor
    {
        /// <summary>
        /// Remueve la primera carpeta de la ruta y retorna el resto del path incluyendo el archivo.
        /// </summary>
        /// <param name="fullPath">Ruta completa del archivo</param>
        /// <returns>Path sin la primera carpeta, o la ruta original si no se puede procesar</returns>
        public static string RemoveFirstFolder(string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath))
                return fullPath;

            try
            {
                // Normalizar las barras para trabajar con el separador correcto del sistema
                string normalizedPath = fullPath.Replace('\\', Path.DirectorySeparatorChar)
                                                .Replace('/', Path.DirectorySeparatorChar);

                // Dividir el path en partes
                string[] pathParts = normalizedPath.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);

                // Si hay menos de 2 partes, no hay nada que remover
                if (pathParts.Length < 2)
                    return fullPath;

                // Tomar todas las partes excepto la primera
                string[] resultParts = pathParts.Skip(1).ToArray();

                // Reconstruir el path
                return string.Join(Path.DirectorySeparatorChar.ToString(), resultParts);
            }
            catch (Exception ex)
            {
                // Log del error si es necesario
                Console.WriteLine($"Error al procesar el path '{fullPath}': {ex.Message}");
                return fullPath; // Retornar path original en caso de error
            }
        }

        /// <summary>
        /// Remueve la primera carpeta y convierte a un formato específico de separadores.
        /// </summary>
        /// <param name="fullPath">Ruta completa del archivo</param>
        /// <param name="separator">Separador a usar ('/', '\' o usar el del sistema)</param>
        /// <returns>Path sin la primera carpeta con el separador especificado</returns>
        public static string RemoveFirstFolderWithSeparator(string fullPath, char separator)
        {
            string processedPath = RemoveFirstFolder(fullPath);

            if (string.IsNullOrEmpty(processedPath))
                return processedPath;

            // Convertir al separador deseado
            if (separator == '/')
            {
                return processedPath.Replace(Path.DirectorySeparatorChar, '/');
            }
            else if (separator == '\\')
            {
                return processedPath.Replace(Path.DirectorySeparatorChar, '\\');
            }

            return processedPath;
        }

        /// <summary>
        /// Procesa múltiples rutas removiendo la primera carpeta de cada una.
        /// </summary>
        /// <param name="paths">Array de rutas a procesar</param>
        /// <returns>Array con los paths procesados</returns>
        public static string[] RemoveFirstFolderFromPaths(string[] paths)
        {
            if (paths == null)
                return null;

            return paths.Select(path => RemoveFirstFolder(path)).ToArray();
        }

        /// <summary>
        /// Extrae el path desde "web-inf" en adelante, independientemente de la ruta base.
        /// </summary>
        /// <param name="fullPath">Ruta completa del archivo</param>
        /// <param name="fromFolder">Carpeta desde la cual extraer (por defecto "web-inf")</param>
        /// <returns>Path desde la carpeta especificada, o null si no se encuentra</returns>
        public static string ExtractPathFrom(string fullPath, string fromFolder = "web-inf")
        {
            if (string.IsNullOrEmpty(fullPath) || string.IsNullOrEmpty(fromFolder))
                return null;

            try
            {
                // Normalizar las barras para trabajar con el separador correcto del sistema
                string normalizedPath = fullPath.Replace('\\', Path.DirectorySeparatorChar)
                                                .Replace('/', Path.DirectorySeparatorChar);

                string normalizedFromFolder = fromFolder.Replace('\\', Path.DirectorySeparatorChar)
                                                       .Replace('/', Path.DirectorySeparatorChar);

                // Dividir el path en partes
                string[] pathParts = normalizedPath.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);

                // Buscar el índice donde aparece la carpeta especificada (case-insensitive)
                int fromIndex = -1;
                for (int i = 0; i < pathParts.Length; i++)
                {
                    if (string.Equals(pathParts[i], normalizedFromFolder, StringComparison.OrdinalIgnoreCase))
                    {
                        fromIndex = i;
                        break;
                    }
                }

                // Si no se encuentra la carpeta, retornar null
                if (fromIndex == -1)
                    return null;

                // Extraer las partes desde la carpeta especificada
                string[] resultParts = pathParts.Skip(fromIndex).ToArray();

                // Reconstruir el path
                return string.Join(Path.DirectorySeparatorChar.ToString(), resultParts);
            }
            catch (Exception ex)
            {
                // Log del error si es necesario
                Console.WriteLine($"Error al procesar el path '{fullPath}': {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Extrae el path desde "web-inf" y lo convierte a un formato específico de separadores.
        /// </summary>
        /// <param name="fullPath">Ruta completa del archivo</param>
        /// <param name="separator">Separador a usar ('/', '\' o usar el del sistema)</param>
        /// <param name="fromFolder">Carpeta desde la cual extraer (por defecto "web-inf")</param>
        /// <returns>Path formateado desde la carpeta especificada</returns>
        public static string ExtractPathFromWithSeparator(string fullPath, char separator, string fromFolder = "web-inf")
        {
            string extractedPath = ExtractPathFrom(fullPath, fromFolder);

            if (string.IsNullOrEmpty(extractedPath))
                return null;

            // Convertir al separador deseado
            if (separator == '/')
            {
                return extractedPath.Replace(Path.DirectorySeparatorChar, '/');
            }
            else if (separator == '\\')
            {
                return extractedPath.Replace(Path.DirectorySeparatorChar, '\\');
            }

            return extractedPath;
        }

        /// <summary>
        /// Procesa múltiples rutas y extrae el path desde la carpeta especificada.
        /// </summary>
        /// <param name="paths">Array de rutas a procesar</param>
        /// <param name="fromFolder">Carpeta desde la cual extraer (por defecto "web-inf")</param>
        /// <returns>Array con los paths extraídos (null para rutas que no contengan la carpeta)</returns>
        public static string[] ExtractPathsFrom(string[] paths, string fromFolder = "web-inf")
        {
            if (paths == null)
                return null;

            return paths.Select(path => ExtractPathFrom(path, fromFolder)).ToArray();
        }

        /// <summary>
        /// Valida si un path contiene la carpeta especificada.
        /// </summary>
        /// <param name="fullPath">Ruta completa a validar</param>
        /// <param name="folder">Carpeta a buscar (por defecto "web-inf")</param>
        /// <returns>True si contiene la carpeta, false en caso contrario</returns>
        public static bool ContainsFolder(string fullPath, string folder = "web-inf")
        {
            return !string.IsNullOrEmpty(ExtractPathFrom(fullPath, folder));
        }
    }
}
