using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PasadorScript.Helpers;
using PasadorScript.Services.Informes;
using PasadorScript.ViewModels;

namespace PasadorScript.Controllers;

/// <summary>
/// Consultas gerenciales de solo lectura. Esta version expone unicamente el informe de releases.
/// </summary>
[Authorize]
public class InformesGerencialesController : Controller
{
    private readonly IInformeGerencialReleasesService _informeReleasesService;
    private readonly ILogger<InformesGerencialesController> _logger;

    /// <summary>
    /// Inicializa una nueva instancia de la clase <see cref="InformesGerencialesController"/>.
    /// </summary>
    public InformesGerencialesController(
        IInformeGerencialReleasesService informeReleasesService,
        ILogger<InformesGerencialesController> logger)
    {
        _informeReleasesService = informeReleasesService ?? throw new ArgumentNullException(nameof(informeReleasesService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Muestra el informe gerencial de releases: envios locales del periodo con el ultimo stage implementado.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Releases(
        DateTime? fechaDesde,
        DateTime? fechaHasta,
        DateTime? fechaImplementadoDesde,
        DateTime? fechaImplementadoHasta,
        string? repoKey,
        string? tipo,
        bool? hotfix,
        string? usuario,
        string? ultimoStage,
        int? nroIncidente,
        string? tipoReferencia,
        string? graficoHotfix,
        string? graficoDestino,
        string? graficoPruebaAcotada,
        string? agruparHotfix,
        string? agruparDestino,
        string? agruparPruebaAcotada,
        int? page)
    {
        var filtros = CrearFiltros(
            fechaDesde, fechaHasta, fechaImplementadoDesde, fechaImplementadoHasta,
            repoKey, tipo, hotfix, usuario, ultimoStage, nroIncidente, tipoReferencia,
            graficoHotfix, graficoDestino, graficoPruebaAcotada,
            agruparHotfix, agruparDestino, agruparPruebaAcotada);

        try
        {
            var model = await _informeReleasesService.ObtenerAsync(filtros, fechaHasta.HasValue, page ?? 1);
            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generando el informe gerencial de releases");
            filtros.Advertencia = "No se pudo generar el informe. Intente nuevamente.";
            return View(filtros);
        }
    }

    /// <summary>
    /// Descarga el informe gerencial de releases a Excel con los mismos filtros de la grilla (todas las filas coincidentes).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ExportToExcel(
        DateTime? fechaDesde,
        DateTime? fechaHasta,
        DateTime? fechaImplementadoDesde,
        DateTime? fechaImplementadoHasta,
        string? repoKey,
        string? tipo,
        bool? hotfix,
        string? usuario,
        string? ultimoStage,
        int? nroIncidente,
        string? tipoReferencia,
        string? graficoHotfix,
        string? graficoDestino,
        string? graficoPruebaAcotada,
        string? agruparHotfix,
        string? agruparDestino,
        string? agruparPruebaAcotada)
    {
        var filtros = CrearFiltros(
            fechaDesde, fechaHasta, fechaImplementadoDesde, fechaImplementadoHasta,
            repoKey, tipo, hotfix, usuario, ultimoStage, nroIncidente, tipoReferencia,
            graficoHotfix, graficoDestino, graficoPruebaAcotada,
            agruparHotfix, agruparDestino, agruparPruebaAcotada);

        try
        {
            var filas = await _informeReleasesService.ObtenerFilasAsync(filtros, fechaHasta.HasValue);
            var content = GenerarExcel(filas);
            var stamp = DateTimeHelper.GetArgentinaNow();
            return File(
                content,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"Informe_Envios_{stamp:yyyyMMdd_HHmmss}.xlsx");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exportando el informe gerencial de releases a Excel");
            return RedirectToAction(nameof(Releases), new
            {
                fechaDesde,
                fechaHasta,
                fechaImplementadoDesde,
                fechaImplementadoHasta,
                repoKey,
                tipo,
                hotfix,
                usuario,
                ultimoStage,
                nroIncidente,
                tipoReferencia,
                graficoHotfix = FlagEncendido(graficoHotfix) ? "true" : null,
                graficoDestino = FlagEncendido(graficoDestino) ? "true" : null,
                graficoPruebaAcotada = FlagEncendido(graficoPruebaAcotada) ? "true" : null,
                agruparHotfix = FlagEncendido(agruparHotfix) ? "true" : null,
                agruparDestino = FlagEncendido(agruparDestino) ? "true" : null,
                agruparPruebaAcotada = FlagEncendido(agruparPruebaAcotada) ? "true" : null
            });
        }
    }

    private static InformeGerencialReleasesFilterViewModel CrearFiltros(
        DateTime? fechaDesde,
        DateTime? fechaHasta,
        DateTime? fechaImplementadoDesde,
        DateTime? fechaImplementadoHasta,
        string? repoKey,
        string? tipo,
        bool? hotfix,
        string? usuario,
        string? ultimoStage,
        int? nroIncidente,
        string? tipoReferencia,
        string? graficoHotfix,
        string? graficoDestino,
        string? graficoPruebaAcotada,
        string? agruparHotfix,
        string? agruparDestino,
        string? agruparPruebaAcotada)
    {
        return new InformeGerencialReleasesFilterViewModel
        {
            FechaDesde = fechaDesde,
            FechaHasta = fechaHasta,
            FechaImplementadoDesde = fechaImplementadoDesde,
            FechaImplementadoHasta = fechaImplementadoHasta,
            RepoKey = repoKey,
            Tipo = tipo,
            Hotfix = hotfix,
            Usuario = usuario,
            UltimoStage = ultimoStage,
            NroIncidente = nroIncidente is > 0 ? nroIncidente : null,
            TipoReferencia = string.IsNullOrWhiteSpace(tipoReferencia) ? null : tipoReferencia.Trim(),
            MostrarGraficoHotfix = FlagEncendido(graficoHotfix),
            MostrarGraficoDestino = FlagEncendido(graficoDestino),
            MostrarGraficoPruebaAcotada = FlagEncendido(graficoPruebaAcotada),
            AgruparGraficoHotfix = FlagEncendido(agruparHotfix),
            AgruparGraficoDestino = FlagEncendido(agruparDestino),
            AgruparGraficoPruebaAcotada = FlagEncendido(agruparPruebaAcotada)
        };
    }

    /// <summary>
    /// Devuelve <see langword="true" /> si el valor de query es <c>true</c>, <c>True</c> o <c>1</c>; en caso contrario, <see langword="false" />.
    /// </summary>
    private static bool FlagEncendido(string? value)
        => string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
           || value == "1";

    private static byte[] GenerarExcel(IReadOnlyList<InformeGerencialReleaseRowViewModel> filas)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Envios");

        worksheet.Cell(1, 1).Value = "ID cambio";
        worksheet.Cell(1, 2).Value = "Tipo";
        worksheet.Cell(1, 3).Value = "Aplicativo";
        worksheet.Cell(1, 4).Value = "Referencia";
        worksheet.Cell(1, 5).Value = "Hotfix";
        worksheet.Cell(1, 6).Value = "Prueba acotada";
        worksheet.Cell(1, 7).Value = "Descripción";
        worksheet.Cell(1, 8).Value = "Usuario";
        worksheet.Cell(1, 9).Value = "Fecha/hora creación";
        worksheet.Cell(1, 10).Value = "Fecha/hora ingreso QA";
        worksheet.Cell(1, 11).Value = "Fecha/hora egreso QA";
        worksheet.Cell(1, 12).Value = "Resultado QA";
        worksheet.Cell(1, 13).Value = "Superv.";
        worksheet.Cell(1, 14).Value = "IyC";
        worksheet.Cell(1, 15).Value = "Fecha implementado";
        worksheet.Cell(1, 16).Value = "Último stage implementado";
        worksheet.Cell(1, 17).Value = "Objetos / archivos";

        var headerRow = worksheet.Row(1);
        headerRow.Style.Font.Bold = true;
        headerRow.Style.Fill.BackgroundColor = XLColor.LightGray;

        var row = 2;
        foreach (var item in filas)
        {
            worksheet.Cell(row, 1).Value = item.IdCambio;
            worksheet.Cell(row, 2).Value = item.Tipo;
            worksheet.Cell(row, 3).Value = item.Aplicativo;
            worksheet.Cell(row, 4).Value = item.NroIncidente > 0 ? item.Referencia : string.Empty;
            worksheet.Cell(row, 5).Value = item.IsHotfix ? "Sí" : "No";
            worksheet.Cell(row, 6).Value = item.PruebaAcotadaTexto;
            worksheet.Cell(row, 7).Value = item.Descripcion ?? string.Empty;
            worksheet.Cell(row, 8).Value = item.Usuario;
            worksheet.Cell(row, 9).Value = item.FechaCreacionLocal.ToString("dd/MM/yyyy, HH:mm");
            worksheet.Cell(row, 10).Value = item.FechaIngresoQaLocal?.ToString("dd/MM/yyyy, HH:mm") ?? string.Empty;
            worksheet.Cell(row, 11).Value = item.FechaEgresoQaLocal?.ToString("dd/MM/yyyy, HH:mm") ?? string.Empty;
            worksheet.Cell(row, 12).Value = item.ResultadoQa ?? string.Empty;
            worksheet.Cell(row, 13).Value = item.PreAprobo ?? string.Empty;
            worksheet.Cell(row, 14).Value = item.PreAproboIyc ?? string.Empty;
            worksheet.Cell(row, 15).Value = item.FechaImplementadoLocal?.ToString("dd/MM/yyyy, HH:mm") ?? string.Empty;
            worksheet.Cell(row, 16).Value = item.UltimoStageImplementado ?? string.Empty;
            worksheet.Cell(row, 17).Value = item.ObjetosArchivos;
            row++;
        }

        worksheet.Columns(1, 16).AdjustToContents();
        var columnaObjetos = worksheet.Column(17);
        columnaObjetos.Width = 40;
        columnaObjetos.Style.Alignment.WrapText = true;

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
