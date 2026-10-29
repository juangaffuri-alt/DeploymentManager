using System.Text.Json;
using PasadorScript.Helpers;
using System.Text.Json.Serialization;

namespace PasadorScript.ViewModels
{
    public class Parsed
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }
        [JsonPropertyName("name")]
        public string Name { get; set; }
        [JsonPropertyName("schema")]
        public string Schema { get; set; }
        [JsonPropertyName("isStructure")]
        public bool IsStructure { get; set; }
    }
    public class VirtualFileViewModelBase
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";
        [JsonPropertyName("_fecha")]
        public DateTime Fecha { get; set; }
    }
    public class VirtualFileViewModel: VirtualFileViewModelBase
    {
        
        [JsonPropertyName("content")]
        public string Content { get; set; } = "";
        [JsonPropertyName("origin")]
        public string Origin { get; set; } = "";
        [JsonPropertyName("_estado")]
        public string Estado { get; set; } = ""; // "_estado"
        [JsonPropertyName("_originalContent")]
        public string OriginalContent { get; set; } = ""; // "_estado"
        [JsonPropertyName("_rutaOriginal")]
        public string? RutaOriginal { get; set; } = "";
        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; } = "";
        [JsonPropertyName("_esBinario")]
        public bool EsBinario { get; set; } = false; // ← Nueva propiedad
        [JsonPropertyName("parsedStructure")]
        public Parsed Metadata { get; set; } = new Parsed();
        [JsonPropertyName("templateType")]
        public string TemplateType { get; set; } = "";
        

    }

    public class VirtualFileViewModelFromDb
    {
        public string Name { get; set; } = "";
        public string Content { get; set; } = "";
        public string Origin { get; set; } = "";
        public string Estado { get; set; } = ""; // "_estado"
        public string OriginalContent { get; set; } = ""; // "_estado"
        public string? RutaOriginal { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public DateTime Fecha { get; set; }
        public DateTime _Fecha { get; set; }
    }
}
