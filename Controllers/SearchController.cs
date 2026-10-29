using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PasadorScript.DbModels;
using PasadorScript.DTO.TeamDev;
using PasadorScript.ViewModels;
using System.Collections.Generic;
using X.PagedList;
using X.PagedList.Extensions;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace PasadorScript.Controllers
{
    public class SearchController : Controller
    {
        private readonly NuevoP229Context _context;
        private readonly IConfiguration _configuration;

        public SearchController(NuevoP229Context context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }
        [HttpGet]
        public async Task<IActionResult> Resultados(string q, int page = 1)
        {
            if (!ModelState.IsValid)
            {
                return View("ResultadosVacios");
            }
            if (string.IsNullOrWhiteSpace(q))
            {
                return View("ResultadosVacios"); // o podrías redirigir a Index
            }

            // Acá hacés la lógica de búsqueda en tu base de datos o servicio
            var resultados = await BuscarEnBDAsync(q);

            // Configuración de paginación
            var registrosPorPagina = int.Parse(_configuration["Pagination:RegistrosPorPagina"]!);
            var skip = (page - 1) * registrosPorPagina;

            // Obtener el total de registros ANTES de aplicar paginación
            var totalRegistros = resultados.Count;

            // Aplicar ordenamiento y paginación para los datos
            var pagedQuery = resultados.OrderByDescending(s => s.Date)
                                 .Skip(skip)
                                 .Take(registrosPorPagina);

            // Ejecutar solo la consulta paginada
            var list = pagedQuery.ToList();

            // Crear PagedList con el total de registros
            var pagedList = new StaticPagedList<SearchTerm>(
                list,
                page,
                registrosPorPagina,
                totalRegistros
            );

            var r = new SearchAll()
            {
                Expression = q,
                PaginaActual = page,
                RegistrosPorPagina = registrosPorPagina,
                TotalRegistros = totalRegistros,
                Resultado = pagedList  // Usar el StaticPagedList creado
            };

            return View(r);
        }

        
        private async Task<ICollection<SearchTerm>> BuscarEnBDAsync(string termino)
        {
            int topeDesc = 80;
            var resultados = new List<SearchTerm>();

            // Ejecutar consultas asíncronas en paralelo
            var taskScripts = await BuscarScriptsAsync(termino, topeDesc);
            var taskBuilds = await BuscarBuildsAsync(termino, topeDesc);
            var taskEnvios = await BuscarEnviosAsync(termino, topeDesc);

            resultados.AddRange(taskScripts);
            resultados.AddRange(taskBuilds);
            resultados.AddRange(taskEnvios);

            return resultados;
        }



        private async Task<List<SearchTerm>> BuscarScriptsAsync(string termino, int topeDesc)
        {
            bool esNumero = int.TryParse(termino, out int nroIncidente);

            var query = _context.Scripts
                .AsNoTracking()
                .Include(c => c.IdTipoCambioNavigation).AsQueryable();

            if (esNumero)
            {
                query = query.Where(c =>
                    c.Descripcion!.Contains(termino) ||
                    c.NroIncidente == nroIncidente ||
                    c.Files!.Contains(termino) );
            }
            else
            {
                query = query.Where(c => c.Descripcion!.Contains(termino) ||
                    c.Files!.Contains(termino));
            }

            return await query
                .Select(c => new SearchTerm
                {
                    Type = SearchTermType.Script,
                    Id = c.IdScript,
                    Title = c.Descripcion!.Length > topeDesc
                        ? c.Descripcion.Substring(0, topeDesc) + "..."
                        : c.Descripcion,
                    Description = c.Descripcion,
                    Autor = c.Usuario,
                    TipoCambio = c.IdTipoCambio,
                    NroIncidente = c.NroIncidente,
                    PullRequestId = c.PullRequestIdQa ?? 0,
                    Date = c.FechaCreacion,
                    DescripcionTipoCambio = c.IdTipoCambioNavigation.Descripcion,
                    AzureProjectType = c.AzureProjectType,
                })
                .ToListAsync(); // Usar ToListAsync en lugar de ToList
        }


        private async Task<List<SearchTerm>> BuscarBuildsAsync(string termino, int topeDesc)
        {
            bool esNumero = int.TryParse(termino, out int nroIncidente);

            var query = _context.BuildsGxes
                .AsNoTracking()
                .Include(b => b.IdTipoCambioNavigation).AsQueryable();

            if (esNumero)
            {
                query = query.Where(b =>
                    b.Descripcion!.Contains(termino) ||
                    b.NroIncidente == nroIncidente ||
                    b.CommitListJson.Contains(termino));
            }
            else
            {
                query = query.Where(b => b.Descripcion!.Contains(termino) ||
                b.CommitListJson.Contains(termino));
            }

            return await query
                .Select(b => new SearchTerm
                {
                    Type = SearchTermType.Genexus,
                    Id = b.IdBuildGx,
                    Title = b.Descripcion!.Length > topeDesc
                        ? b.Descripcion.Substring(0, topeDesc)+"..."
                        : b.Descripcion,
                    Description = b.Descripcion,
                    Autor = b.Usuario,
                    TipoCambio = b.IdTipoCambio,
                    NroIncidente = b.NroIncidente,
                    PullRequestId = b.IdPr ?? 0,
                    Date = b.FechaCreacion,
                    DescripcionTipoCambio = b.IdTipoCambioNavigation.Descripcion,
                    AzureProjectType = b.AzureProjectType,
                })
                .ToListAsync();
        }

        private async Task<List<SearchTerm>> BuscarEnviosAsync(string termino, int topeDesc)
        {
            bool esNumero = int.TryParse(termino, out int nroIncidente);

            var query = _context.EnviosDlyas
                .AsNoTracking()
                .Include(e => e.IdTipoCambioNavigation).AsQueryable();

            if (esNumero)
            {
                query = query.Where(e =>
                    e.Descripcion.Contains(termino) ||
                    e.NroIncidente == nroIncidente ||
                    e.CommitListJson.Contains(termino));
            }
            else
            {
                query = query.Where(e => e.Descripcion.Contains(termino) || e.CommitListJson.Contains(termino));
            }

            return await query
                .Select(e => new SearchTerm
                {
                    Type = SearchTermType.Dlya,
                    Id = e.IdEnvioDlya,
                    Title = e.Descripcion!.Length > topeDesc
                        ? e.Descripcion.Substring(0, topeDesc) + "..."
                        : e.Descripcion,
                    Description = e.Descripcion,
                    Autor = e.Usuario,
                    TipoCambio = e.IdTipoCambio,
                    NroIncidente = e.NroIncidente,
                    PullRequestId = e.IdPr ?? 0,
                    Date = e.FechaCreacion,
                    DescripcionTipoCambio = e.IdTipoCambioNavigation.Descripcion,
                    AzureProjectType = e.AzureProjectType
                })
                .ToListAsync();
        }
    }
}
