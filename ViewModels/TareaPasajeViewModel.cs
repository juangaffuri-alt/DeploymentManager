using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace PasadorScript.ViewModels
{
    public class TareaPasajeViewModel
    {
        [JsonPropertyName("nro")]
        public int Nro { get; set; }

        [JsonPropertyName("descripcion")]
        public string? Descripcion { get; set; }

        [JsonPropertyName("responsable")]
        public string? Responsable { get; set; }

        [JsonPropertyName("informarFinalizacion")]
        [JsonConverter(typeof(FlexibleBoolJsonConverter))]
        public bool InformarFinalizacion { get; set; }
    }

    public class TablaTareasPartialModel
    {
        public string? TareasJson { get; set; }
        public bool Editable { get; set; }
        public string InputName { get; set; } = "TareasJson";
    }

    /// <summary>
    /// Acepta bool nativo o string legacy (HTML / "true") al deserializar informarFinalizacion.
    /// </summary>
    public sealed class FlexibleBoolJsonConverter : JsonConverter<bool>
    {
        public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return reader.TokenType switch
            {
                JsonTokenType.True => true,
                JsonTokenType.False => false,
                JsonTokenType.String => ParseLegacyString(reader.GetString()),
                JsonTokenType.Number => reader.TryGetInt64(out var n) && n != 0,
                _ => false
            };
        }

        public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options)
            => writer.WriteBooleanValue(value);

        private static bool ParseLegacyString(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            if (bool.TryParse(value, out var parsed))
                return parsed;

            var stripped = Regex.Replace(value, "<[^>]+>", string.Empty).Trim();
            return !string.IsNullOrWhiteSpace(stripped);
        }
    }
}
