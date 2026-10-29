using System.Text.Json;
using System.Text.RegularExpressions;
using PasadorScript.ViewModels;

namespace PasadorScript.Helpers
{
    public static class TareasJsonHelper
    {
        public const int MaxFilas = 50;
        public const int MaxResponsableLength = 80;
        public const int MaxHtmlLength = 4000;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private static readonly Regex DangerousTags = new(
            @"<\s*(script|iframe|object|embed|link|meta|style|form|input|button|textarea|select|svg|math)[^>]*>.*?<\s*/\s*\1\s*>|<\s*(script|iframe|object|embed|link|meta|style|form|input|button|textarea|select|svg|math)[^>]*/?\s*>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

        private static readonly Regex EventHandlers = new(
            @"\s+on\w+\s*=\s*(""[^""]*""|'[^']*'|[^\s>]+)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex JavascriptUri = new(
            @"javascript\s*:",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex DisallowedTags = new(
            @"</?(?!/?\s*(?:p|br|b|strong|i|em|u|span)\b)[a-zA-Z][^>]*>",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex SpanOpen = new(
            @"<\s*span\b([^>]*)>",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex StyleAttr = new(
            @"style\s*=\s*(""(?<v>[^""]*)""|'(?<v>[^']*)')",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex ColorValue = new(
            @"^\s*color\s*:\s*(#[0-9a-fA-F]{3,8}|rgb\(\s*\d{1,3}\s*,\s*\d{1,3}\s*,\s*\d{1,3}\s*\)|[a-zA-Z]+)\s*;?\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Normaliza, sanitiza y re-serializa el JSON de tareas. Devuelve null si no hay filas.
        /// </summary>
        public static string? Normalize(string? tareasJson)
        {
            var list = Parse(tareasJson);
            if (list.Count == 0)
                return null;

            return JsonSerializer.Serialize(list, JsonOptions);
        }

        public static List<TareaPasajeViewModel> Parse(string? tareasJson)
        {
            if (string.IsNullOrWhiteSpace(tareasJson))
                return [];

            List<TareaPasajeViewModel>? raw;
            try
            {
                raw = JsonSerializer.Deserialize<List<TareaPasajeViewModel>>(tareasJson, JsonOptions);
            }
            catch (JsonException)
            {
                return [];
            }

            if (raw == null || raw.Count == 0)
                return [];

            var result = new List<TareaPasajeViewModel>();
            foreach (var item in raw.Take(MaxFilas))
            {
                var responsable = Truncate(item.Responsable?.Trim() ?? string.Empty, MaxResponsableLength);
                var descripcion = Truncate(SanitizeHtml(item.Descripcion), MaxHtmlLength);

                // Checkbox solo o campos incompletos no cuentan como fila válida
                if (string.IsNullOrWhiteSpace(responsable)
                    || string.IsNullOrWhiteSpace(StripTags(descripcion)))
                {
                    continue;
                }

                result.Add(new TareaPasajeViewModel
                {
                    Descripcion = descripcion,
                    Responsable = responsable,
                    InformarFinalizacion = item.InformarFinalizacion
                });
            }

            for (var i = 0; i < result.Count; i++)
                result[i].Nro = i + 1;

            return result;
        }

        public static string SanitizeHtml(string? html)
        {
            if (string.IsNullOrWhiteSpace(html))
                return string.Empty;

            var value = html.Trim();
            value = DangerousTags.Replace(value, string.Empty);
            value = EventHandlers.Replace(value, string.Empty);
            value = JavascriptUri.Replace(value, string.Empty);
            value = DisallowedTags.Replace(value, string.Empty);
            value = SpanOpen.Replace(value, m =>
            {
                var attrs = m.Groups[1].Value;
                var styleMatch = StyleAttr.Match(attrs);
                if (!styleMatch.Success)
                    return "<span>";

                var style = styleMatch.Groups["v"].Value.Trim();
                if (!ColorValue.IsMatch(style))
                    return "<span>";

                return $"<span style=\"color:{ExtractColor(style)}\">";
            });

            return value;
        }

        private static string ExtractColor(string style)
        {
            var m = Regex.Match(style, @"color\s*:\s*(#[0-9a-fA-F]{3,8}|rgb\(\s*\d{1,3}\s*,\s*\d{1,3}\s*,\s*\d{1,3}\s*\)|[a-zA-Z]+)", RegexOptions.IgnoreCase);
            return m.Success ? m.Groups[1].Value.Trim() : "inherit";
        }

        private static string StripTags(string? html) =>
            string.IsNullOrEmpty(html) ? string.Empty : Regex.Replace(html, "<[^>]+>", string.Empty).Trim();

        private static string Truncate(string value, int max)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= max)
                return value ?? string.Empty;
            return value[..max];
        }
    }
}
