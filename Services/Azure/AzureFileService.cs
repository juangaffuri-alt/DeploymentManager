using PasadorScript.DTO.AzureDev;
using PasadorScript.ViewModels;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PasadorScript.Services.Azure
{
    public class AzureFileService: AzureBaseService, IAzureFileService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<AzureFileService> _logger;
        private readonly AzureHttpClientConfigured _httpConfigured;
        private readonly AzureHttpClientUser _httpUser;

        public AzureFileService(IConfiguration configuration, ILogger<AzureFileService> logger,
            AzureHttpClientConfigured azureHttpClientConfigured, AzureHttpClientUser httpUser) : base(configuration)
        {
            _config = configuration;
            _logger = logger;
            _httpConfigured = azureHttpClientConfigured;
            _httpUser = httpUser;
        }
        public async Task<List<SqlStructure>> SearchSqlStructuresAsync(string repositoryId, string structureType = null, string searchTerm = null)
        {
            try
            {

                // 1. Obtener archivos del repositorio
                var files = await GetRepositoryFilesAsync(repositoryId, structureType);

                // 2. Filtrar archivos SQL que probablemente contengan estructuras
                var sqlFiles = files.Where(f =>
                    f.Path.EndsWith(".sql", StringComparison.OrdinalIgnoreCase) &&
                    IsLikelyStructureFile(f.Path) &&
                    !f.IsFolder
                ).ToList();

                _logger.LogInformation("Found {Count} SQL files to analyze", sqlFiles.Count);

                // 3. Aplicar filtros por nombre de archivo ANTES de analizar contenido
                var filteredFiles = sqlFiles.Where(file =>
                {
                    var fileName = Path.GetFileNameWithoutExtension(file.Path);

                    // Filtro por término de búsqueda en el nombre del archivo
                    bool matchesSearchByName = string.IsNullOrEmpty(searchTerm) ||
                                             fileName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                                             file.Path.Contains(searchTerm, StringComparison.OrdinalIgnoreCase);

                    return matchesSearchByName;
                }).ToList();
                _logger.LogInformation("Filtered to {Count} files to analyze based on name patterns", filteredFiles.Count);

                // 4. Analizar solo los archivos filtrados en paralelo (con límite para no sobrecargar)
                var semaphore = new SemaphoreSlim(5, 5); // Máximo 5 archivos simultáneos
                var tasks = filteredFiles.Select(async file =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        return await AnalyzeFileAsync(repositoryId, file);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });

                var results = await Task.WhenAll(tasks);
                var structures = results.Where(s => s != null).ToList();

                return structures.OrderBy(s => s.Type).ThenBy(s => s.Name).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching SQL structures in repository {RepositoryId}", repositoryId);
                throw;
            }
        }
        public async Task<string> PushFiles(string repositoryId, string branchName, List<VirtualFileViewModel> files, bool firstCommit)
        {
            try
            {
                string uniqueId = branchName;
                var branch = branchName;
                // 1. Obtener el último commit de la rama
                var baseUrl = Ctx(ProjectType).BaseUrl;
                string refsUrl = $"{baseUrl}/_apis/git/repositories/{repositoryId}/refs?filter=heads/{branch}&api-version=7.0";
                var refsResponse = await _httpConfigured.GetAsync(refsUrl);
                refsResponse.EnsureSuccessStatusCode();
                var refsJson = await refsResponse.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(refsJson);

                JsonElement valueElement = doc.RootElement.GetProperty("value");
                string oldObjectId = valueElement.GetArrayLength() > 0
                    ? valueElement[0].GetProperty("objectId").GetString()!
                    : "0000000000000000000000000000000000000000";


                // 2. Construir los cambios
                var changes = new List<object>();
                string folderPath = $"/{uniqueId}/";
                var states = new List<string>() { "nuevo", "modificado", "eliminado" };

                foreach (var file in files)
                {
                    if (file.Content.Length > 0 && states.Contains(file.Estado))
                    {
                        var changeType = file.Estado == "nuevo" ? "add"
                                        : file.Estado == "modificado" ? "edit"
                                        : file.Estado == "eliminado" ? "delete"
                                        : "edit";

                        var fullPath = file.Origin != "devops" && !file.Metadata.IsStructure ? folderPath : "";
                        var fullFileName = $"{fullPath}{file.Name}";

                        // MODIFICACIÓN: Siempre usar base64 para preservar encoding
                        object? newContent = null;

                        if (file.Estado != "eliminado")
                        {
                            if (file.EsBinario)
                            {
                                // Si ya es binario (base64), mantener como está
                                newContent = new
                                {
                                    content = file.Content,
                                    contentType = "base64encoded"
                                };
                            }
                            else
                            {
                                // Para texto: convertir a UTF-8 y luego a base64
                                var utf8Bytes = Encoding.UTF8.GetBytes(file.Content);
                                var base64Content = Convert.ToBase64String(utf8Bytes);

                                newContent = new
                                {
                                    content = base64Content,
                                    contentType = "base64encoded"
                                };
                            }
                        }

                        var change = new
                        {
                            changeType,
                            item = new { path = fullFileName },
                            newContent = newContent
                        };

                        changes.Add(change);
                    }
                }

                // 3. Armar el body del push
                var body = new
                {
                    refUpdates = new[]
                    {
                    new {
                        name = $"refs/heads/{branch}",
                        oldObjectId = oldObjectId
                    }
                },
                    commits = new[]
                    {
                    new {
                        comment = "Actualización automática de archivos",
                        changes
                    }
                }
                };

                // 4. Enviar el push
                string pushUrl = $"{baseUrl}/_apis/git/repositories/{repositoryId}/pushes?api-version=7.0";
                var pushResponse = await _httpUser.SendAsync(HttpMethod.Post, pushUrl, body);

                if (!pushResponse.IsSuccessStatusCode)
                {
                    var errorContent = await pushResponse.Content.ReadAsStringAsync();
                    _logger.LogError($"Error al realizar el push: {pushResponse.StatusCode} - {errorContent}");
                    return await PushFilesForce(repositoryId, branchName, files);
                }

                var json = await pushResponse.Content.ReadAsStringAsync();
                using var docj = JsonDocument.Parse(json);

                return docj.RootElement.GetProperty("commits")[0].GetProperty("commitId").GetString()!;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en PushFiles");
                throw; // Re-lanza para que el controller lo capture
            }
        }

        public async Task<string> PushFiles(string repositoryId, string branchName, string nameFile, string contentFile)
        {
            var branch = branchName;

            // 1. Obtener el último commit de la rama
            var baseUrl = Ctx(ProjectType).BaseUrl;
            string refsUrl = $"{baseUrl}/_apis/git/repositories/{repositoryId}/refs?filter=heads/{branch}&api-version=7.0";
            var refsResponse = await _httpConfigured.GetAsync(refsUrl);

            refsResponse.EnsureSuccessStatusCode();
            var refsJson = await refsResponse.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(refsJson);

            JsonElement valueElement = doc.RootElement.GetProperty("value");
            string oldObjectId = valueElement.GetArrayLength() > 0
                ? valueElement[0].GetProperty("objectId").GetString()!
                : "0000000000000000000000000000000000000000";

            // 2. Construir los cambios
            var changes = new List<object>();
            var changeType = "add";

            // MODIFICACIÓN: Convertir a UTF-8 y luego a base64
            var utf8Bytes = Encoding.UTF8.GetBytes(contentFile);
            var base64Content = Convert.ToBase64String(utf8Bytes);

            var change = new
            {
                changeType,
                item = new { path = $"/{nameFile}" },
                newContent = new
                {
                    content = base64Content,
                    contentType = "base64encoded"
                }
            };

            changes.Add(change);
            // 3. Armar el body del push
            var body = new
            {
                refUpdates = new[]
                {
                    new {
                        name = $"refs/heads/{branch}",
                        oldObjectId
                    }
                },
                commits = new[]
                {
                    new {
                        comment = "Actualización automática de archivos",
                        changes
                    }
                }
            };

            // 4. Enviar el push
            string pushUrl = $"{baseUrl}/_apis/git/repositories/{repositoryId}/pushes?api-version=7.0";
            var pushResponse = await _httpUser.SendAsync(HttpMethod.Post, pushUrl, body);


            if (!pushResponse.IsSuccessStatusCode)
            {
                var errorContent = await pushResponse.Content.ReadAsStringAsync();
                throw new Exception($"Error al realizar el push: {pushResponse.StatusCode} - {errorContent}");
            }

            var json = await pushResponse.Content.ReadAsStringAsync();
            using var docj = JsonDocument.Parse(json);

            return docj.RootElement.GetProperty("commits")[0].GetProperty("commitId").GetString()!;
        }
        public async Task<string> GetFileContentAsync(string repositoryId, string filePath, bool? desdeBranchDesa, string? branchSource)
        {
            var branch = _config["AzureSettings:BranchPR"];
            if (branchSource != null)
            {
                branch = branchSource;
            }
            else if (desdeBranchDesa.HasValue && desdeBranchDesa.Value)
            {
                branch = _config["AzureSettings:BranchDV"];
            }
            else if (desdeBranchDesa.HasValue && !desdeBranchDesa.Value)
            {
                branch = _config["AzureSettings:BranchQA"];
            }

            // Construcción del URL
            var nFile = filePath.StartsWith('/') ? filePath.TrimStart('/') : filePath;
            string url = $"{Ctx(ProjectType).BaseUrl}/_apis/git/repositories/{repositoryId}/items?path={Uri.EscapeDataString(nFile)}&includeContent=true&versionType=branch&version={branch}&api-version=7.1-preview.1";

            var response = await _httpConfigured.GetAsync(url);
            response.EnsureSuccessStatusCode();

            // Leer como bytes y luego decodificar como UTF-8
            var bytes = await response.Content.ReadAsByteArrayAsync();
            string content = Encoding.UTF8.GetString(bytes);
            return content;
        }

        public async Task<string> GetFileContentAsync(string repositoryId, string filePath, string commitId)
        {
            try
            {
                var baseUrl = Ctx(ProjectType).BaseUrl;
                var encodedPath = Uri.EscapeDataString(filePath);
                var response = await _httpConfigured.GetAsync($"{baseUrl}/_apis/git/repositories/{repositoryId}/items?path={encodedPath}&versionType=commit&version={commitId}&api-version=7.0");

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsStringAsync();
                }

                return string.Empty;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        public async Task<string> GetFileContentFromPathAsync(string repositoryId, string filePath)
        {
            try
            {
                var encodedPath = Uri.EscapeDataString(filePath);
                var urlBase = Ctx(ProjectType).BaseUrl;
                var url = $"{urlBase}/_apis/git/repositories/{repositoryId}/items?path={encodedPath}&download=true&api-version=7.1";

                var response = await _httpConfigured.GetAsync(url);

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    _logger.LogWarning("File not found: {FilePath}", filePath);
                    return null;
                }

                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting file content for {FilePath}", filePath);
                throw;
            }
        }

        
        public async Task<string> GetActualPathAzureDevOps(string repositoryId, string path)
        {
            try
            {
                var baseUrl = Ctx(ProjectType).BaseUrl;

                // Usar el endpoint de TREES que solo devuelve metadata
                var directoryPath = System.IO.Path.GetDirectoryName(path)!.Replace('\\', '/');
                var fileName = System.IO.Path.GetFileName(path);

                // Construir URL
                var url = $"{baseUrl}/_apis/git/repositories/{repositoryId}/items?api-version=7.1&scopePath={Uri.EscapeDataString(directoryPath)}&recursionLevel=OneLevel";

                var response = await _httpUser.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();

                    using (JsonDocument doc = JsonDocument.Parse(content))
                    {
                        var root = doc.RootElement;

                        if (root.TryGetProperty("value", out JsonElement valueElement) && valueElement.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var item in valueElement.EnumerateArray())
                            {
                                if (item.TryGetProperty("path", out JsonElement pathElement))
                                {
                                    var itemPath = pathElement.GetString();
                                    var itemFileName = System.IO.Path.GetFileName(itemPath);

                                    // Comparar case insensitive pero retornar el path real
                                    if (string.Equals(itemFileName, fileName, StringComparison.OrdinalIgnoreCase))
                                    {
                                        return itemPath!;
                                    }
                                }
                            }
                        }
                    }
                }

                return null;

            }
            catch (Exception ex)
            {
                _logger.LogError($"Error en GetActualPathAzureDevOps: {ex.Message}");
                return null;
            }
        }

        public async Task<List<ArchivoConFecha>> GetArchivosEnCarpeta(string repositoryId, string branchName, string folderPath)
        {
            var scopePath = Uri.EscapeDataString(folderPath.StartsWith("/") ? folderPath : "/" + folderPath);
            var url = $"{0}/{1}/{2}/_apis/git/repositories/{repositoryId}/items?scopePath={scopePath}&recursionLevel=Full&versionType=branch&version={branchName}&api-version=7.1-preview.1";

            var response = await _httpConfigured.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            var archivosBlob = doc.RootElement.GetProperty("value")
                .EnumerateArray()
                .Where(item => item.GetProperty("gitObjectType").GetString() == "blob")
                .Select(item => item.GetProperty("path").GetString())
                .ToList();

            var tareas = archivosBlob.Select(async path => new ArchivoConFecha
            {
                Path = path!,
                FileName = Path.GetFileName(path!.Replace('/', '\\')),
                FechaModificacion = await GetLastModifiedDateAsync(repositoryId, path, branchName)
            });

            var resultados = await Task.WhenAll(tareas);
            return resultados.ToList();
        }

        public async Task<DateTime?> GetLastModifiedDateAsync(string repositoryId, string filePath, string branchName)
        {
            var scopePath = Uri.EscapeDataString(filePath.StartsWith("/") ? filePath.TrimStart('/') : filePath);

            var url = $"{Ctx(ProjectType).BaseUrl}/_apis/git/repositories/{repositoryId}/commits?searchCriteria.itemPath={filePath}&searchCriteria.itemVersion.version={branchName}&searchCriteria.itemVersion.versionType=branch&$top=1&api-version=7.1-preview.1";//GetUriFormatted("ApiFilesDateInBranch", );

            var response = await _httpConfigured.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            using var json = JsonDocument.Parse(content);

            var commits = json.RootElement.GetProperty("value");
            if (commits.GetArrayLength() == 0)
                return null;

            var dateStr = commits[0].GetProperty("committer").GetProperty("date").GetString();
            return DateTime.TryParse(dateStr, out var date) ? date : null;
        }
        private async Task<string> PushFilesForce(string repositoryId, string branchName, List<VirtualFileViewModel> files)
        {
            string uniqueId = branchName;
            var branch = branchName;
            var baseUrl = Ctx(ProjectType).BaseUrl;
            // 1. Obtener el último commit de la rama
            string refsUrl = $"{baseUrl}/_apis/git/repositories/{repositoryId}/refs?filter=heads/{branch}&api-version=7.0";
            var refsResponse = await _httpConfigured.GetAsync(refsUrl);
            refsResponse.EnsureSuccessStatusCode();
            var refsJson = await refsResponse.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(refsJson);

            JsonElement valueElement = doc.RootElement.GetProperty("value");
            string oldObjectId = valueElement.GetArrayLength() > 0
                ? valueElement[0].GetProperty("objectId").GetString()!
                : "0000000000000000000000000000000000000000";

            // 2. Construir los cambios
            var changes = new List<object>();
            string folderPath = $"/{uniqueId}/";
            var states = new List<string>() { "nuevo", "modificado", "eliminado" };
            
            foreach (var file in files)
            {
                if (file.Content.Length > 0 && states.Contains(file.Estado))
                {
                    var changeType = file.Estado == "nuevo" ? "add"
                                    : file.Estado == "modificado" ? "edit"
                                    : file.Estado == "eliminado" ? "delete"
                                    : "edit";

                    var fullPath = file.Origin != "devops" && !file.Metadata.IsStructure ? folderPath : "";
                    var fullFileName = $"{fullPath}{file.Name}";

                    if (file.Origin == "devops")
                    {
                        changeType = "add";
                    }

                    // MODIFICACIÓN: Usar base64 para preservar encoding
                    object? newContent = null;

                    if (file.Estado != "eliminado")
                    {
                        if (file.EsBinario)
                        {
                            newContent = new
                            {
                                content = file.Content,
                                contentType = "base64encoded"
                            };
                        }
                        else
                        {
                            var utf8Bytes = Encoding.UTF8.GetBytes(file.Content);
                            var base64Content = Convert.ToBase64String(utf8Bytes);

                            newContent = new
                            {
                                content = base64Content,
                                contentType = "base64encoded"
                            };
                        }
                    }

                    var change = new
                    {
                        changeType,
                        item = new { path = fullFileName },
                        newContent = newContent
                    };

                    changes.Add(change);
                }
            }

            // 3. Armar el body del push
            var body = new
            {
                refUpdates = new[]
                {
                    new {
                        name = $"refs/heads/{branch}",
                        oldObjectId = oldObjectId
                    }
                },
                commits = new[]
                {
                    new {
                        comment = "Actualización automática de archivos",

                        changes
                    }
                }
            };

            // 4. Enviar el push
            string pushUrl = $"{baseUrl}/_apis/git/repositories/{repositoryId}/pushes?api-version=7.0";
            var pushResponse = await _httpUser.SendAsync(HttpMethod.Post, pushUrl, body);

            if (!pushResponse.IsSuccessStatusCode)
            {
                var errorContent = await pushResponse.Content.ReadAsStringAsync();
                throw new Exception($"Error al realizar el push: {pushResponse.StatusCode} - {errorContent}");
            }

            var json = await pushResponse.Content.ReadAsStringAsync();
            using var docj = JsonDocument.Parse(json);

            return docj.RootElement.GetProperty("commits")[0].GetProperty("commitId").GetString()!;
        }
        private async Task<List<GitFileItem>> GetRepositoryFilesAsync(string repositoryId, string structureType)
        {
            try
            {
                var urlBase = Ctx(AzureProjectType.Default).BaseUrl;
                var structure = structureType ?? "";

                var url = $"{urlBase}/_apis/git/repositories/{repositoryId}/items?scopePath=/database/{structure}&recursionLevel=Full&includeContentMetadata=true&api-version=7.1";

                var response = await _httpConfigured.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var jsonResponse = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonSerializer.Deserialize<GitItemsResponse>(jsonResponse, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                var items = apiResponse!.Value ?? new List<GitFileItem>();

                // Obtener fechas de modificación
                // await SetLastModifiedAsync(repositoryId, items);

                return items;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting repository files for {RepositoryId}", repositoryId);
                throw;
            }
        }
        private async Task SetLastModifiedAsync(string repositoryId, List<GitFileItem> items)
        {
            // Solo archivos
            var files = items.Where(i => !i.IsFolder).ToList();

            var semaphore = new SemaphoreSlim(5); // Máximo 5 requests simultáneos

            var tasks = files.Select(async file =>
            {
                await semaphore.WaitAsync();
                try
                {
                    file.Author = await GetLastModifiedAsync(repositoryId, file.Path);
                    
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);
        }
        public async Task<GitCommit?> GetLastModifiedAsync(string repositoryId, string filePath)
        {
            var urlBase = Ctx(AzureProjectType.Default).BaseUrl;

            var url = $"{urlBase}/_apis/git/repositories/{repositoryId}/commits" +
                      $"?searchCriteria.itemPath={Uri.EscapeDataString(filePath)}" +
                      $"&$top=1&api-version=7.1";

            var response = await _httpConfigured.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var commitResponse = JsonSerializer.Deserialize<GitCommitsResponse>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            var commit = commitResponse?.Value?.FirstOrDefault();
            
            return commit!;
        }

        private async Task<SqlStructure> AnalyzeFileAsync(string repositoryId, GitFileItem file)
        {
            try
            {
                var content = await GetFileContentFromPathAsync(repositoryId, file.Path);

                if (string.IsNullOrEmpty(content))
                    return null;

                var structure = AnalyzeSqlContent(content, file.Path);
                if (structure != null)
                {
                    structure.LastModified = file.LastModified;
                    structure.Size = file.Size;
                }

                return structure;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing file {FilePath}", file.Path);
                return null;
            }
        }

        private bool IsLikelyStructureFile(string path)
        {
            var structureIndicators = new[]
            {
            "/procedures/", "/views/", "/functions/", "/tables/", "/triggers/",
            "/schema/", "/database/", "/db/", "/migrations/", "/scripts/",
            "stored_procedures", "sp_", "vw_", "fn_", "udf_", "tr_"
        };

            var lowerPath = path.ToLower();

            // Excluir archivos que probablemente no sean estructuras
            var excludePatterns = new[] { "/test/", "/backup/", "/temp/", ".bak", ".old" };
            if (excludePatterns.Any(pattern => lowerPath.Contains(pattern)))
                return false;

            return structureIndicators.Any(indicator => lowerPath.Contains(indicator));
        }
        private SqlStructure AnalyzeSqlContent(string content, string filePath)
        {
            if (string.IsNullOrEmpty(content))
                return null;

            var structure = new SqlStructure
            {
                Path = filePath,
                Name = Path.GetFileNameWithoutExtension(filePath),
                Type = "Script" // Valor por defecto
            };

            // Limpiar y normalizar contenido para análisis
            var cleanContent = RemoveSqlComments(content);
            var normalizedContent = cleanContent.ToUpper().Replace("\r\n", " ").Replace("\n", " ");

            // Detectar tipo de estructura con patrones más precisos
            var detectionPatterns = new Dictionary<string, string[]>
            {
                ["StoredProcedure"] = new[] {
                @"CREATE\s+(?:OR\s+ALTER\s+)?PROC\s+(?:\[?[\w\d_]+\]?\.)?\[?([\w\d_]+)\]?",
                @"CREATE\s+(?:OR\s+ALTER\s+)?PROCEDURE\s+(?:\[?[\w\d_]+\]?\.)?\[?([\w\d_]+)\]?",
                @"ALTER\s+PROCEDURE\s+(?:\[?[\w\d_]+\]?\.)?\[?([\w\d_]+)\]?"
            },
                ["View"] = new[] {
                @"CREATE\s+(?:OR\s+ALTER\s+)?VIEW\s+(?:\[?[\w\d_]+\]?\.)?\[?([\w\d_]+)\]?",
                @"ALTER\s+VIEW\s+(?:\[?[\w\d_]+\]?\.)?\[?([\w\d_]+)\]?"
            },
                ["Function"] = new[] {
                @"CREATE\s+(?:OR\s+ALTER\s+)?FUNCTION\s+(?:\[?[\w\d_]+\]?\.)?\[?([\w\d_]+)\]?",
                @"ALTER\s+FUNCTION\s+(?:\[?[\w\d_]+\]?\.)?\[?([\w\d_]+)\]?"
            },
                ["Table"] = new[] {
                @"CREATE\s+TABLE\s+(?:\[?[\w\d_]+\]?\.)?\[?([\w\d_]+)\]?",
                @"ALTER\s+TABLE\s+(?:\[?[\w\d_]+\]?\.)?\[?([\w\d_]+)\]?"
            },
                ["Trigger"] = new[] {
                @"CREATE\s+(?:OR\s+ALTER\s+)?TRIGGER\s+(?:\[?[\w\d_]+\]?\.)?\[?([\w\d_]+)\]?",
                @"ALTER\s+TRIGGER\s+(?:\[?[\w\d_]+\]?\.)?\[?([\w\d_]+)\]?"
            }
            };

            // Buscar coincidencias
            foreach (var (type, patterns) in detectionPatterns)
            {
                foreach (var pattern in patterns)
                {
                    var match = Regex.Match(normalizedContent, pattern, RegexOptions.IgnoreCase);
                    if (match.Success && match.Groups.Count > 1)
                    {
                        structure.Type = type;
                        structure.Name = match.Groups[1].Value;
                        structure.Schema = ExtractSchema(content);
                        return structure;
                    }
                }
            }

            return structure;
        }

        private string RemoveSqlComments(string sql)
        {
            // Remover comentarios de línea (--) y de bloque (/* */)
            var withoutLineComments = Regex.Replace(sql, @"--.*$", "", RegexOptions.Multiline);
            var withoutBlockComments = Regex.Replace(withoutLineComments, @"/\*.*?\*/", "", RegexOptions.Singleline);
            return withoutBlockComments;
        }

        private string ExtractSchema(string content)
        {
            // Buscar patrones de schema en el contenido
            var schemaPatterns = new[]
            {
            @"\[(\w+)\]\.\[\w+\]",  // [schema].[object]
            @"(\w+)\.\w+",          // schema.object
            @"USE\s+\[?(\w+)\]?"    // USE [database]
        };

            foreach (var pattern in schemaPatterns)
            {
                var match = Regex.Match(content, pattern, RegexOptions.IgnoreCase);
                if (match.Success && match.Groups.Count > 1)
                {
                    var schemaName = match.Groups[1].Value;
                    // Filtrar nombres que no son schemas típicos
                    if (!IsSystemSchema(schemaName))
                    {
                        return schemaName;
                    }
                }
            }

            return "dbo"; // Schema por defecto
        }

        private bool IsSystemSchema(string schemaName)
        {
            var systemSchemas = new[] { "sys", "information_schema", "master", "model", "msdb", "tempdb" };
            return systemSchemas.Contains(schemaName.ToLower());
        }

        public async Task<List<StructureInfo>> SearchStructures(string repositoryId, string structureType, string searchTerm)
        {
            try
            {
                var baseUrl = Ctx(ProjectType).BaseUrl;

                // Mapear tipos de estructura a carpetas en DevOps
                var typeToFolder = new Dictionary<string, string>
        {
            { "StoredProcedure", "stored_procedures" },
            { "stored_procedures", "stored_procedures" },
            { "View", "views" },
            { "views", "views" },
            { "Function", "functions" },
            { "functions", "functions" },
            { "Table", "tables" },
            { "tables", "tables" },
            { "Trigger", "triggers" },
            { "triggers", "triggers" }
        };

                // Obtener la carpeta base según el tipo de estructura
                string folderPath = "database";
                if (!string.IsNullOrEmpty(structureType) && typeToFolder.TryGetValue(structureType, out var folder))
                {
                    folderPath = $"database/{folder}";
                }

                // Llamar a la API de Azure DevOps para listar archivos en la carpeta
                string itemsUrl = $"{baseUrl}/_apis/git/repositories/{repositoryId}/items?scopePath={folderPath}&recursionLevel=Full&api-version=7.0";

                var itemsResponse = await _httpConfigured.GetAsync(itemsUrl);
                itemsResponse.EnsureSuccessStatusCode();

                var itemsJson = await itemsResponse.Content.ReadAsStringAsync();
                using var itemsDoc = JsonDocument.Parse(itemsJson);

                var structures = new List<StructureInfo>();

                // Procesar los elementos encontrados
                if (itemsDoc.RootElement.TryGetProperty("value", out JsonElement valueElement))
                {
                    foreach (var item in valueElement.EnumerateArray())
                    {
                        // Solo procesar archivos SQL
                        if (item.TryGetProperty("path", out JsonElement pathElement) &&
                            pathElement.GetString()?.EndsWith(".sql", StringComparison.OrdinalIgnoreCase) == true)
                        {
                            var path = pathElement.GetString()!;

                            // Extraer información del archivo
                            var fileName = Path.GetFileNameWithoutExtension(path);
                            var directory = Path.GetDirectoryName(path)?.Replace("\\", "/");

                            // Determinar el tipo de estructura basado en la carpeta
                            string detectedType = "sql";
                            if (directory?.Contains("/stored_procedures/", StringComparison.OrdinalIgnoreCase) == true)
                                detectedType = "StoredProcedure";
                            else if (directory?.Contains("/views/", StringComparison.OrdinalIgnoreCase) == true)
                                detectedType = "View";
                            else if (directory?.Contains("/functions/", StringComparison.OrdinalIgnoreCase) == true)
                                detectedType = "Function";
                            else if (directory?.Contains("/tables/", StringComparison.OrdinalIgnoreCase) == true)
                                detectedType = "Table";
                            else if (directory?.Contains("/triggers/", StringComparison.OrdinalIgnoreCase) == true)
                                detectedType = "Trigger";

                            // Extraer el nombre de la estructura (quitando schema si existe)
                            string structureName = fileName;
                            if (fileName.Contains('.'))
                            {
                                // Formato: schema.structurename
                                var parts = fileName.Split('.');
                                structureName = parts.Length > 1 ? parts[1] : parts[0];
                            }

                            // Filtrar por término de búsqueda si se proporciona
                            if (string.IsNullOrEmpty(searchTerm) ||
                                structureName.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                structures.Add(new StructureInfo
                                {
                                    Name = structureName,
                                    Type = detectedType,
                                    Path = path
                                });
                            }
                        }
                    }
                }

                return structures;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching structures in repository {RepositoryId}", repositoryId);
                throw;
            }
        }
    }
}
