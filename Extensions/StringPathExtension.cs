using PasadorScript.DTO.AzureDev;
using PasadorScript.Helpers;

namespace PasadorScript.Extensions
{
    // Extensión de string para hacer más fácil el uso
    public static class StringPathExtension
    {
        public static AzureProjectType ToAzureProjectType(this string projectTypeStr)
        {
            return projectTypeStr.ToLower() switch
            {
                "default" => AzureProjectType.Default,
                "genexus" => AzureProjectType.GeneXus,
                "scriptold" => AzureProjectType.ScriptOld,
                "genexusold" => AzureProjectType.GenexusOld,
                _ => throw new ArgumentException($"Unknown AzureProjectType: {projectTypeStr}")
            };
        }
        public static string ToProjectTypeString(this AzureProjectType projectType)
        {
            return projectType switch
            {
                AzureProjectType.Default => "default",
                AzureProjectType.GeneXus => "genexus",
                AzureProjectType.ScriptOld => "scriptold",
                AzureProjectType.GenexusOld => "genexusold",
                _ => throw new ArgumentException($"Unknown AzureProjectType: {projectType}")
            };
        }
        /// <summary>
        /// Extrae el path desde "web-inf" en adelante.
        /// </summary>
        public static string ExtractFromWebInf(this string fullPath)
        {
            return PathExtractor.ExtractPathFrom(fullPath, "web-inf");
        }

        /// <summary>
        /// Extrae el path desde la carpeta especificada.
        /// </summary>
        public static string ExtractFrom(this string fullPath, string fromFolder)
        {
            return PathExtractor.ExtractPathFrom(fullPath, fromFolder);
        }
        /// <summary>
        /// Remueve la primera carpeta de la ruta.
        /// </summary>
        public static string RemoveFirstFolder(this string fullPath)
        {
            return PathExtractor.RemoveFirstFolder(fullPath);
        }
    }
}
