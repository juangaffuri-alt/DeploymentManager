namespace PasadorScript.DTO.Informes;

/// <summary>
/// Identificadores de tipo de envio local usados en el informe gerencial de releases.
/// </summary>
public static class TipoEnvioInforme
{
    /// <summary>Envio de script SQL.</summary>
    public const string Script = "Script";

    /// <summary>Envio de build GeneXus.</summary>
    public const string Gx = "Gx";

    /// <summary>Envio DLYA.</summary>
    public const string Dlya = "Dlya";
}
