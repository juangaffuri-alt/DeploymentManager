namespace PasadorScript.ViewModels;

/// <summary>
/// Totales de mix hotfix para el grafico del informe gerencial de releases (universo filtrado, antes de paginar).
/// En modo envios las porciones suman <see cref="Total"/>; en modo agrupado por referencia pueden superarlo.
/// </summary>
public sealed class InformeGerencialKpiHotfixViewModel
{
    /// <summary>Cantidad de envios, o de referencias distintas si el grafico esta agrupado.</summary>
    public int Total { get; init; }

    /// <summary>Cantidad hotfix (envios, o referencias con al menos un envio hotfix si esta agrupado).</summary>
    public int Hotfix { get; init; }

    /// <summary>Cantidad no hotfix (envios, o referencias con al menos un envio no hotfix si esta agrupado).</summary>
    public int NoHotfix { get; init; }
}

/// <summary>
/// Totales de destino para el grafico del informe gerencial de releases (universo filtrado, antes de paginar).
/// En modo envios las tres porciones suman <see cref="Total"/>; en modo agrupado por referencia pueden superarlo.
/// </summary>
public sealed class InformeGerencialKpiDestinoViewModel
{
    /// <summary>Cantidad de envios, o de referencias distintas si el grafico esta agrupado.</summary>
    public int Total { get; init; }

    /// <summary>Cantidad sin ultimo stage implementado (sin release), o referencias con al menos un envio en ese estado si esta agrupado.</summary>
    public int NoSalieron { get; init; }

    /// <summary>
    /// Cantidad de filas cuyo ultimo stage no esta en el bucket PROD (QA, DF u otro).
    /// </summary>
    public int EnQa { get; init; }

    /// <summary>Cantidad de filas cuyo ultimo stage esta en el bucket PROD.</summary>
    public int EnProd { get; init; }
}

/// <summary>
/// Totales de prueba acotada para el grafico del informe gerencial de releases (universo filtrado, antes de paginar).
/// En modo envios las tres porciones suman <see cref="Total"/>; en modo agrupado por referencia pueden superarlo.
/// </summary>
public sealed class InformeGerencialKpiPruebaAcotadaViewModel
{
    /// <summary>Cantidad de envios, o de referencias distintas si el grafico esta agrupado.</summary>
    public int Total { get; init; }

    /// <summary>Cantidad con prueba acotada (Sí), o referencias con al menos un envio en Sí si esta agrupado.</summary>
    public int Si { get; init; }

    /// <summary>Cantidad sin prueba acotada (No), o referencias con al menos un envio en No si esta agrupado.</summary>
    public int No { get; init; }

    /// <summary>Cantidad sin marca (Sin datos), o referencias con al menos un envio sin marca si esta agrupado.</summary>
    public int SinDatos { get; init; }
}
