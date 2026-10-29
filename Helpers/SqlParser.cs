using System.Text.RegularExpressions;

namespace PasadorScript.Helpers
{
    public class SqlStructureInfo
    {
        public string Type { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Schema { get; set; } = "dbo";
    }
    public static class SqlParser
    {
        public static string RemoveSqlCommentsRegex(string sql)
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
        public static SqlStructureInfo ParseStructureFromContent(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return new SqlStructureInfo();
            content = RemoveSqlCommentsRegex(content);
            var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();

                // Stored Procedure
                var spMatch = Regex.Match(line,
                    @"(?:CREATE|ALTER)\s+(?:OR\s+ALTER\s+)?PROCEDURE\s+(?:\[?(\w+)\]?\.)?(?:\[?(\w+)\]?)",
                    RegexOptions.IgnoreCase);

                if (spMatch.Success)
                {
                    return new SqlStructureInfo
                    {
                        Type = "StoredProcedure",
                        Schema = string.IsNullOrEmpty(spMatch.Groups[1].Value) ? "dbo" : spMatch.Groups[1].Value,
                        Name = spMatch.Groups[2].Value
                    };
                }

                // View
                var viewMatch = Regex.Match(line,
                    @"(?:CREATE|ALTER)\s+(?:OR\s+ALTER\s+)?VIEW\s+(?:\[?(\w+)\]?\.)?(?:\[?(\w+)\]?)",
                    RegexOptions.IgnoreCase);

                if (viewMatch.Success)
                {
                    return new SqlStructureInfo
                    {
                        Type = "View",
                        Schema = string.IsNullOrEmpty(viewMatch.Groups[1].Value) ? "dbo" : viewMatch.Groups[1].Value,
                        Name = viewMatch.Groups[2].Value
                    };
                }

                // Function
                var funcMatch = Regex.Match(line,
                    @"(?:CREATE|ALTER)\s+(?:OR\s+ALTER\s+)?FUNCTION\s+(?:\[?(\w+)\]?\.)?(?:\[?(\w+)\]?)",
                    RegexOptions.IgnoreCase);

                if (funcMatch.Success)
                {
                    return new SqlStructureInfo
                    {
                        Type = "Function",
                        Schema = string.IsNullOrEmpty(funcMatch.Groups[1].Value) ? "dbo" : funcMatch.Groups[1].Value,
                        Name = funcMatch.Groups[2].Value
                    };
                }

                // Table
                var tableMatch = Regex.Match(line,
                    @"CREATE\s+TABLE\s+(?:\[?(\w+)\]?\.)?(?:\[?(\w+)\]?)",
                    RegexOptions.IgnoreCase);

                if (tableMatch.Success)
                {
                    return new SqlStructureInfo
                    {
                        Type = "Table",
                        Schema = string.IsNullOrEmpty(tableMatch.Groups[1].Value) ? "dbo" : tableMatch.Groups[1].Value,
                        Name = tableMatch.Groups[2].Value
                    };
                }

                // Trigger
                var triggerMatch = Regex.Match(line,
                    @"(?:CREATE|ALTER)\s+(?:OR\s+ALTER\s+)?TRIGGER\s+(?:\[?(\w+)\]?\.)?(?:\[?(\w+)\]?)",
                    RegexOptions.IgnoreCase);

                if (triggerMatch.Success)
                {
                    return new SqlStructureInfo
                    {
                        Type = "Trigger",
                        Schema = string.IsNullOrEmpty(triggerMatch.Groups[1].Value) ? "dbo" : triggerMatch.Groups[1].Value,
                        Name = triggerMatch.Groups[2].Value
                    };
                }
            
                

            }
            return new SqlStructureInfo
            {
                Type = "Template",
                Schema = "",
                Name = ""
            };

        }
    }
}
