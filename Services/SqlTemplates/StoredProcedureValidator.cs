using PasadorScript.DTO.Sql;
using System.Text.RegularExpressions;

namespace PasadorScript.Services.SqlTemplates
{
    public class StoredProcedureValidator : ITemplateValidator
    {
        public ScriptType ScriptType => ScriptType.AlterStoredProcedure;
        public string GetTemplateName() => "Alter Stored Procedure";

        public bool CanHandle(string sqlScript)
        {
            var script = sqlScript.ToUpperInvariant();
            return script.Contains("ALTER PROCEDURE") &&
                   script.Contains("OBJECT_ID") &&
                   script.Contains("CREATE PROCEDURE");
        }

        public SqlValidationResult ValidateTemplate(string sqlScript)
        {
            var result = new SqlValidationResult { DetectedTemplateType = GetTemplateName() };
            var errors = new List<string>();

            var cleanScript = RemoveHeader(sqlScript);

            try
            {
                // 1. Validar IF OBJECT_ID('dbo.NOMBRE_SP') IS NULL
                var objectIdMatch = Regex.Match(cleanScript,
                    @"IF\s+OBJECT_ID\s*\(\s*'(\w+)\.(\w+)'\s*\)\s+IS\s+NULL",
                    RegexOptions.IgnoreCase);

                if (!objectIdMatch.Success)
                {
                    errors.Add("Debe incluir: IF OBJECT_ID('dbo.NOMBRE_SP') IS NULL");
                    result.Errors = errors;
                    return result;
                }

                var spName = objectIdMatch.Groups[1].Value;

                // 2. Validar EXEC('CREATE PROCEDURE...')
                var execPattern = $@"EXEC\s*\(\s*'CREATE\s+PROCEDURE\s+dbo\.{spName}\s+AS\s+SET\s+NOCOUNT\s+ON;'\s*\)";
                if (!Regex.IsMatch(cleanScript, execPattern, RegexOptions.IgnoreCase))
                {
                    errors.Add($"Debe incluir: EXEC('CREATE PROCEDURE dbo.{spName} AS SET NOCOUNT ON;')");
                }

                // 3. Validar GO
                if (!Regex.IsMatch(cleanScript, @"^\s*GO\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline))
                {
                    errors.Add("Debe incluir la instrucción GO");
                }

                // 4. Validar ALTER PROCEDURE
                var alterMatch = Regex.Match(cleanScript, @"ALTER\s+PROCEDURE\s+dbo\.(\w+)", RegexOptions.IgnoreCase);
                if (!alterMatch.Success)
                {
                    errors.Add("Debe incluir: ALTER PROCEDURE dbo.NOMBRE_SP");
                }
                else if (alterMatch.Groups[1].Value.ToUpper() != spName.ToUpper())
                {
                    errors.Add("El nombre del SP debe ser consistente en IF OBJECT_ID y ALTER PROCEDURE");
                }

                // 5. Validar AS y BEGIN/END
                if (!Regex.IsMatch(cleanScript, @"ALTER\s+PROCEDURE.*AS\s+BEGIN.*END",
                    RegexOptions.IgnoreCase | RegexOptions.Singleline))
                {
                    errors.Add("El SP debe tener la estructura: AS BEGIN...END");
                }

                // Extraer información
                result.ExtractedObjectName = spName;
                result.ExtractedParameters = ExtractParameters(cleanScript);

                result.IsValid = errors.Count == 0;
                result.Errors = errors;

                return result;
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Error validando SP: {ex.Message}");
                return result;
            }
        }

        private string RemoveHeader(string script)
        {
            // Remover el encabezado hasta encontrar la primera línea que no sea comentario
            var lines = script.Split('\n');
            var startIndex = 0;

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (!string.IsNullOrEmpty(line) && !line.StartsWith("--") && !line.StartsWith("-"))
                {
                    startIndex = i;
                    break;
                }
            }

            return string.Join("\n", lines.Skip(startIndex));
        }

        private List<string> ExtractParameters(string script)
        {
            var parameters = new List<string>();
            var match = Regex.Match(script, @"ALTER\s+PROCEDURE\s+dbo\.\w+(.*?)AS\s+BEGIN",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            if (match.Success)
            {
                var paramSection = match.Groups[1].Value;
                var paramMatches = Regex.Matches(paramSection, @"@(\w+)\s+\w+", RegexOptions.IgnoreCase);

                foreach (Match paramMatch in paramMatches)
                {
                    parameters.Add($"@{paramMatch.Groups[1].Value}");
                }
            }
            return parameters;
        }
    }
}
