using PasadorScript.DTO.AzureDev;
using System.Text.RegularExpressions;

namespace PasadorScript.Helpers
{
    public class TemplateSqlManager
    {
        public string OriginalTemplate { get; set; }
        public StructureSQLType Origin { get; set; }
        
        public TemplateSqlManager(StructureSQLType origin) 
        {
            Origin = origin;
        }

        private void SetRequiredLines(StructureSQLContentTemplate source, string content)
        {
            string[] lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            int lineNro = 0;
            foreach (string line in lines)
            {
                if (line.Contains("#required"))
                    source.RequiredLines.Add(lineNro);
                lineNro++;
            }
        }

        private string RemoveSqlCommentsRegex(string sql)
        {
            if (string.IsNullOrEmpty(sql))
                return sql;

            // Patrón para comentarios de bloque /* */
            string blockCommentPattern = @"/\*.*?\*/";

            // Patrón para comentarios de línea --
            string lineCommentPattern = @"--.*?(\r\n|\r|\n|$)";

            // Primero eliminar comentarios de bloque
            string result = Regex.Replace(sql, blockCommentPattern, string.Empty,
                RegexOptions.Singleline);

            // Luego eliminar comentarios de línea
            result = Regex.Replace(result, lineCommentPattern, match =>
            {
                // Preservar el salto de línea si existe
                return match.Value.Contains("\n") ? "\n" : string.Empty;
            }, RegexOptions.Multiline);

            return result.Trim();
        }

        public static async Task<string> GetTemplateContentAsync(string fileName)
        {
            // Para proyectos web, WebRootPath apunta a wwwroot
            var webRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var filePath = Path.Combine(webRootPath, "Templates", fileName);

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Template file not found: {fileName}");
            }
            return await File.ReadAllTextAsync(filePath);
            

        }

        public bool TieneTemplate(string contenido)
        {
            if (string.IsNullOrWhiteSpace(contenido))
                return false;

            return contenido.Contains("alguna/s lineas del template", StringComparison.OrdinalIgnoreCase);
        }

        public async Task<StructureSQLContentTemplate> GetContentTemplate(string schema, string objName)
        {
            if (Origin == null)
            {
                throw new InvalidOperationException("Origin vacio");
            }
            StructureSQLContentTemplate res = new StructureSQLContentTemplate();
            if (!String.IsNullOrEmpty(Origin.Script))
            {
                var alterContent = await GetTemplateContentAsync(Origin.Script);
                if (alterContent != null)
                {
                    SetRequiredLines(res, alterContent);
                    res.Content = alterContent.Replace("{schema}", schema).Replace("{objectName}", objName)
                        .Replace("#required", "");
                    
                }
            }
            
            return res;
        }
    }
}
