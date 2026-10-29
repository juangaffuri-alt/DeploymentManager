using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace PasadorScript.Helpers.Sql
{
    public class SqlStatementParser
    {
        public static List<string> SplitSqlStatements(string sqlScript)
        {
            var statements = new List<string>();

            if (string.IsNullOrWhiteSpace(sqlScript))
                return statements;

            // Patrón para identificar el delimitador GO en líneas separadas
            // Considera GO solo cuando está en una línea por sí mismo (opcionalmente con espacios)
            var goPattern = @"^\s*GO\s*$";
            var goRegex = new Regex(goPattern, RegexOptions.Multiline | RegexOptions.IgnoreCase);

            // Encontrar todas las posiciones de GO
            var goMatches = goRegex.Matches(sqlScript);

            if (goMatches.Count == 0)
            {
                // Si no hay GO, el script completo es una sola sentencia
                if (!string.IsNullOrWhiteSpace(sqlScript.Trim()))
                    statements.Add(sqlScript.Trim());
                return statements;
            }

            int currentPosition = 0;

            foreach (Match goMatch in goMatches)
            {
                // Extraer la sentencia desde la posición actual hasta el GO
                int goStart = goMatch.Index;
                int goEnd = goMatch.Index + goMatch.Length;

                string statement = sqlScript.Substring(currentPosition, goStart - currentPosition).Trim();

                if (!string.IsNullOrEmpty(statement))
                {
                    statements.Add(statement);
                }

                // Actualizar la posición actual después del GO
                currentPosition = goEnd;
            }

            // Agregar la última sentencia después del último GO (si existe)
            if (currentPosition < sqlScript.Length)
            {
                string lastStatement = sqlScript.Substring(currentPosition).Trim();
                if (!string.IsNullOrEmpty(lastStatement))
                {
                    statements.Add(lastStatement);
                }
            }

            return statements;
        }

        // Versión alternativa que también maneja comentarios y es más robusta
        public static List<string> SplitSqlStatementsAdvanced(string sqlScript)
        {
            var statements = new List<string>();

            if (string.IsNullOrWhiteSpace(sqlScript))
                return statements;

            // Normalizar saltos de línea
            sqlScript = sqlScript.Replace("\r\n", "\n").Replace("\r", "\n");

            // Dividir por líneas para procesar más fácilmente
            var lines = sqlScript.Split('\n');
            var currentStatement = new StringBuilder();
            bool inMultiLineComment = false;
            bool inSingleQuotes = false;
            bool inDoubleQuotes = false;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                string trimmedLine = line.Trim();

                // Verificar si es un GO en su propia línea (sin estar dentro de comillas o comentarios)
                if (!inMultiLineComment && !inSingleQuotes && !inDoubleQuotes &&
                    string.Equals(trimmedLine, "GO", StringComparison.OrdinalIgnoreCase))
                {
                    // Finalizar la sentencia actual si tiene contenido
                    string current = currentStatement.ToString().Trim();
                    if (!string.IsNullOrEmpty(current))
                    {
                        statements.Add(current);
                        currentStatement.Clear();
                    }
                    continue;
                }

                // Procesar la línea carácter por carácter para detectar comentarios y comillas
                for (int j = 0; j < line.Length; j++)
                {
                    char currentChar = line[j];
                    char nextChar = j < line.Length - 1 ? line[j + 1] : '\0';

                    if (!inMultiLineComment && !inSingleQuotes && !inDoubleQuotes)
                    {
                        // Inicio de comentario de una línea
                        if (currentChar == '-' && nextChar == '-')
                        {
                            // El resto de la línea es comentario, pero lo mantenemos
                            currentStatement.Append(line.Substring(j));
                            break;
                        }
                        // Inicio de comentario multi-línea
                        else if (currentChar == '/' && nextChar == '*')
                        {
                            inMultiLineComment = true;
                            currentStatement.Append(currentChar);
                            j++; // Saltar el siguiente carácter
                            continue;
                        }
                        // Comillas simples
                        else if (currentChar == '\'')
                        {
                            inSingleQuotes = true;
                        }
                        // Comillas dobles
                        else if (currentChar == '"')
                        {
                            inDoubleQuotes = true;
                        }
                    }
                    else if (inMultiLineComment)
                    {
                        // Fin de comentario multi-línea
                        if (currentChar == '*' && nextChar == '/')
                        {
                            inMultiLineComment = false;
                            currentStatement.Append(currentChar);
                            j++; // Saltar el siguiente carácter
                            continue;
                        }
                    }
                    else if (inSingleQuotes && currentChar == '\'')
                    {
                        // Escapar comillas simples duplicadas
                        if (nextChar == '\'')
                        {
                            currentStatement.Append(currentChar);
                            j++; // Saltar la comilla escapada
                            continue;
                        }
                        else
                        {
                            inSingleQuotes = false;
                        }
                    }
                    else if (inDoubleQuotes && currentChar == '"')
                    {
                        // Escapar comillas dobles duplicadas
                        if (nextChar == '"')
                        {
                            currentStatement.Append(currentChar);
                            j++; // Saltar la comilla escapada
                            continue;
                        }
                        else
                        {
                            inDoubleQuotes = false;
                        }
                    }

                    currentStatement.Append(currentChar);
                }

                currentStatement.Append('\n');
            }

            // Agregar la última sentencia si existe
            string finalStatement = currentStatement.ToString().Trim();
            if (!string.IsNullOrEmpty(finalStatement))
            {
                statements.Add(finalStatement);
            }

            return statements;
        }
    }

}
