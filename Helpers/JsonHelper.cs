using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PasadorScript.Helpers
{
    public class FlexibleIntConverter : JsonConverter<int>
    {
        public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Number)
            {
                return reader.GetInt32();
            }

            if (reader.TokenType == JsonTokenType.String)
            {
                var stringValue = reader.GetString();
                if (int.TryParse(stringValue, out var result))
                {
                    return result;
                }
                throw new JsonException($"No se pudo convertir '{stringValue}' a int.");
            }

            throw new JsonException($"Tipo de token inesperado {reader.TokenType} al parsear un int.");
        }

        public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
        {
            writer.WriteNumberValue(value);
        }
    }
}
