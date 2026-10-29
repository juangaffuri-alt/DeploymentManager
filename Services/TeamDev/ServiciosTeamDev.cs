using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using PasadorScript.DbModels;
using PasadorScript.DTO.TeamDev;
using PasadorScript.ViewModels;
using System.Net.Http;

namespace PasadorScript.Services.TeamDev
{

    public class LogQueryParameters
    {
        public string? KbName { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string? RevisionId { get; set; }
        public string? SearchQuery { get; set; }
        public string? ServerKbVersion { get; set; }
        public int? Page { get; set; }
        public int? PageSize { get; set; }
    }

    // Añade esta clase para el resultado paginado
    public class PaginatedResult<T>
    {
        public ICollection<T> Items { get; set; }
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }

    public class BuildInCommit
    {
        public int IdBuildGx { get; set; }
        public int? IdPr { get; set; }
    }

    [Route("teamDev")]
    public class ServiciosTeamDev : ControllerBase, IServiciosTeamDev
    {
        private readonly ILogger<ServiciosTeamDev> _logger;
        private readonly IConfiguration _configuration;
        private readonly NuevoP229Context _context;

        public ServiciosTeamDev(ILogger<ServiciosTeamDev> logger, IConfiguration configuration, NuevoP229Context context)
        {
            _logger = logger;
            _configuration = configuration;
            _context = context;
        }

        private async Task<List<BuildGXViewModel>> GetBuildsByRevisions(List<int> revisions)
        {
            // Crear una lista de strings para buscar en el JSON
            var revisionStrings = revisions.Select(r => $@"""revision"": {r}").ToList();

            var builds = await _context.BuildsGxes
                .Where(b => revisionStrings.Any(rs => b.CommitListJson.Contains(rs)))
                .Select(b => new BuildGXViewModel
                {
                    IdBuildGx = b.IdBuildGx,
                    CommitListJson = b.CommitListJson,
                    IdPr = b.IdPr
                    // otras propiedades
                })
                .ToListAsync();

            return builds;
        }

        [HttpGet("GetCommitsFromServer")]
        public async Task<ICollection<CommitsTeamDev>> GetCommitsFromServer(LogQueryParameters parameters)
        {

            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
            };
            var httpClient = new HttpClient(handler);

            var baseUrl = _configuration["TeamDevSettings:BaseHost"];

            var query = new List<string>();

            if (!string.IsNullOrEmpty(parameters.KbName))
                query.Add($"kB={Uri.EscapeDataString(parameters.KbName)}");

            if (parameters.FromDate.HasValue)
                query.Add($"fromDate={Uri.EscapeDataString(parameters.FromDate.Value.ToString("yyyy-MM-ddTHH:mm:ss"))}"); // ISO 8601

            if (parameters.ToDate.HasValue)
                query.Add($"toDate={Uri.EscapeDataString(parameters.ToDate.Value.ToString("yyyy-MM-ddTHH:mm:ss"))}");

            if (!string.IsNullOrEmpty(parameters.RevisionId))
                query.Add($"revisionId={Uri.EscapeDataString(parameters.RevisionId)}");

            if (!string.IsNullOrEmpty(parameters.SearchQuery))
                query.Add($"searchQuery={Uri.EscapeDataString(parameters.SearchQuery)}");

            if (!string.IsNullOrEmpty(parameters.ServerKbVersion))
                query.Add($"serverKbVersion={Uri.EscapeDataString(parameters.ServerKbVersion)}");

            var fullUrl = query.Count > 0 ? $"{baseUrl}?{string.Join("&", query)}" : baseUrl;

            var response = await httpClient.GetAsync(fullUrl);

            response.EnsureSuccessStatusCode(); // Lanza excepción si falla

            var content = await response.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<ICollection<CommitsTeamDev>>(content);
            foreach (var r in result!)
            {
                var buildRevisions = await _context.Database
                    .SqlQueryRaw<BuildInCommit>($@"
                    SELECT b.idBuildGx, b.IdPr
                    FROM buildsGX b
                    CROSS APPLY OPENJSON(b.commitListJson) WITH (
                        revision int '$.revision'
                    ) j
                    WHERE j.revision = ({r.revision}) and not b.idPr is null")
                .ToListAsync();
                r.Builds = buildRevisions;
            }

            return result;

        }

        [HttpGet("GetCommitsFromServerPaginated")]
        public async Task<PaginatedResult<CommitsTeamDev>> GetCommitsFromServerPaginated(LogQueryParameters parameters)
        {
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
            };
            var httpClient = new HttpClient(handler);

            var baseUrl = _configuration["TeamDevSettings:BaseHost"];

            var query = new List<string>();

            if (!string.IsNullOrEmpty(parameters.KbName))
                query.Add($"kB={Uri.EscapeDataString(parameters.KbName)}");

            if (parameters.FromDate.HasValue)
                query.Add($"fromDate={Uri.EscapeDataString(parameters.FromDate.Value.ToString("yyyy-MM-ddTHH:mm:ss"))}");

            if (parameters.ToDate.HasValue)
                query.Add($"toDate={Uri.EscapeDataString(parameters.ToDate.Value.ToString("yyyy-MM-ddTHH:mm:ss"))}");

            if (!string.IsNullOrEmpty(parameters.RevisionId))
                query.Add($"revisionId={Uri.EscapeDataString(parameters.RevisionId)}");

            if (!string.IsNullOrEmpty(parameters.SearchQuery))
                query.Add($"searchQuery={Uri.EscapeDataString(parameters.SearchQuery)}");

            if (!string.IsNullOrEmpty(parameters.ServerKbVersion))
                query.Add($"serverKbVersion={Uri.EscapeDataString(parameters.ServerKbVersion)}");

            var fullUrl = query.Count > 0 ? $"{baseUrl}?{string.Join("&", query)}" : baseUrl;

            var response = await httpClient.GetAsync(fullUrl);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var allResults = JsonConvert.DeserializeObject<ICollection<CommitsTeamDev>>(content);

            // Aplicar paginación
            var totalCount = allResults.Count;
            var page = parameters.Page ?? 1;
            var pageSize = parameters.PageSize ?? 10;
            var skip = (page - 1) * pageSize;

            var paginatedResults = allResults
                .Skip(skip)
                .Take(pageSize)
                .ToList();

            // Buscar builds para los commits paginados
            foreach (var r in paginatedResults!)
            {
                var buildRevisions = await _context.Database
                    .SqlQueryRaw<BuildInCommit>($@"
                SELECT b.idBuildGx, b.IdPr
                FROM buildsGX b
                CROSS APPLY OPENJSON(b.commitListJson) WITH (
                    revision int '$.revision'
                ) j
                WHERE j.revision = ({r.revision}) and not b.idPr is null")
                    .ToListAsync();
                r.Builds = buildRevisions;
            }

            return new PaginatedResult<CommitsTeamDev>
            {
                Items = paginatedResults,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            };
        }
    }
}
