using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using PasadorScript.DbModels;
using PasadorScript.Services.Azure;
using PasadorScript.Services.Graph;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

namespace PasadorScript.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HandlerController : ControllerBase
    {
        private readonly IAzureService _serviciosAzure;
        private readonly IConfiguration _configuration;
        private readonly IMemoryCache _cache;
        private readonly IServiciosGraph _serviciosGraph;

        [HttpGet("Dummy")]
        public IActionResult GetDummy()
        {
            var data = new { mensaje = "Holis", fecha = DateTime.UtcNow };
            return Ok(data); // Devuelve JSON automáticamente
        }

        public HandlerController(IAzureService serviciosAzure, IMemoryCache cache, IConfiguration configuration, IServiciosGraph serviciosGraph)
        {
            _serviciosAzure = serviciosAzure;
            _configuration = configuration;
            _cache = cache;
            _serviciosGraph = serviciosGraph;
        }

        [HttpGet("imagen-devops")]
        public async Task<IActionResult> ObtenerImagen(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return BadRequest("URL inválida");

            // Opcional: Validar que solo se acepten URLs de Azure DevOps
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || !uri.Host.Contains("dev.azure.com"))
                return BadRequest("URL no permitida");

            using var client = new HttpClient();
            var _pat = _configuration["AzureSettings:PersonalAccessToken"];
            var credentials = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($":{_pat}"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);

            var response = await client.GetAsync(uri);
            if (!response.IsSuccessStatusCode)
                return StatusCode((int)response.StatusCode);

            var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
            var bytes = await response.Content.ReadAsByteArrayAsync();

            return File(bytes, contentType);
        }

        [HttpGet("imagen-devopsAd")]
        public async Task<IActionResult> ObtenerImagenDirectoryAlias(string? directoryAlias, string? email, string? displayName)
        {
            DTO.Graph.UserInfo? usr = null;
            if (!String.IsNullOrEmpty(directoryAlias))
                usr = await _serviciosGraph.InfoUser(directoryAlias!);
            if (usr == null && (!String.IsNullOrEmpty(email)))
                usr = await _serviciosGraph.InfoUserByEmail(email!);
            if (usr == null && (!String.IsNullOrEmpty(displayName)))
                usr = await _serviciosGraph.InfoUserByDisplayName(displayName!);

            var url = usr?._links.avatar.href!;

            if (string.IsNullOrWhiteSpace(url))
                return BadRequest("URL inválida");

            // Opcional: Validar que solo se acepten URLs de Azure DevOps
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || !uri.Host.Contains("dev.azure.com"))
                return BadRequest("URL no permitida");

            using var client = new HttpClient();
            var _pat = _configuration["AzureSettings:PersonalAccessToken"];
            var credentials = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($":{_pat}"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);

            var response = await client.GetAsync(uri);
            if (!response.IsSuccessStatusCode)
                return StatusCode((int)response.StatusCode);

            var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
            var bytes = await response.Content.ReadAsByteArrayAsync();

            return File(bytes, contentType);
        }



        [HttpPost("NotifyMergedPR")]
        public async Task<IActionResult> HandleMergedPR()
        {
            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync();

            var payload = System.Text.Json.JsonDocument.Parse(body);

            var prId = (int?)payload.RootElement.GetProperty("resource").GetProperty("pullRequestId").GetInt32();
            var repoId = (string?)payload.RootElement.GetProperty("resource").GetProperty("repository").GetProperty("id").GetString();

            if (prId == null || repoId == null)
                return Ok(); // O bad request

            var uniqueKey = $"pr:{repoId}:{prId}";

            if (_cache.TryGetValue(uniqueKey, out _))
            {
                // Ya lo procesamos antes
                return Ok();
            }

            var targetRefName = (string?)payload.RootElement.GetProperty("resource").GetProperty("targetRefName").GetString();
            var status = (string?)payload.RootElement.GetProperty("resource").GetProperty("status").GetString();
            var mergeStatus = (string?)payload.RootElement.GetProperty("resource").GetProperty("mergeStatus").GetString();
            var message = (string?)payload.RootElement.GetProperty("resource").GetProperty("message").GetProperty("text").GetString();

            if (status != "completed" || mergeStatus != "succeeded" || targetRefName != "refs/heads/QA")
                return Ok();

            var repoName = (string?)payload.RootElement.GetProperty("resource").GetProperty("repository").GetProperty("name").GetString();
            var project = (string?)payload.RootElement.GetProperty("resource").GetProperty("repository").GetProperty("project").GetProperty("name").GetString();

            await _serviciosAzure.CreateBranchAndPullRequestAsync("QA", "main", repoName!, prId.Value);
            // Guardar en cache por 10 minutos para evitar duplicados
            _cache.Set(uniqueKey, true, TimeSpan.FromMinutes(10));
            return Ok();
        }


    }
}
