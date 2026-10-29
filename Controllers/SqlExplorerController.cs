using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using PasadorScript.Services.Azure;
using PasadorScript.ViewModels;
using System.Text.RegularExpressions;

namespace PasadorScript.Controllers
{
    public class SqlExplorerController : Controller
    {
        private readonly IAzureService _serviciosAzure;

        public SqlExplorerController(IAzureService serviciosAzure)
        {
            _serviciosAzure = serviciosAzure;
        }
        [HttpGet]
        public async Task<ActionResult> Index(string? repositoryId)
        {
            var aplicativos = (await _serviciosAzure.GetRepositories()).ToList();
            ViewBag.Aplicativos = new SelectList(aplicativos, "Name", "Name");
            DTO.AzureDev.Repository repo = null;
            if (repositoryId == null)
            {
                repo = aplicativos.First();
            }
            else
            {
                repo = aplicativos.First(x => x.Name == repositoryId);

            }
            var model = new SqlExplorerViewModel()
            {
                RepositoryId = repo.Name
            };
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> DetailSqlExplorer(string repositoryId, string filePath)
        {
            var history = await _serviciosAzure.GetFileHistoryAsync(repositoryId, filePath);
            var content = await _serviciosAzure.GetFileContentFromPathAsync(repositoryId, filePath);
            var parsed = ParseStructureFromContent(content);
            var model = new DetailSqlExplorerViewModel()
            {
                FilePath = filePath,
                RepositoryId = repositoryId,
                History = history,
                FileContent = content,
                SqlStructure = parsed
            };

            return View(model);
        }

        private SqlStructureInfo ParseStructureFromContent(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return new SqlStructureInfo { Type = "", Name = "", Schema = "dbo", IsStructure = false };

            var lines = content.Split('\n')
                              .Select(line => line.Trim().ToUpperInvariant())
                              .Where(line => !string.IsNullOrWhiteSpace(line));

            foreach (var line in lines)
            {
                // Buscar CREATE/ALTER statements
                if (line.StartsWith("CREATE") || line.StartsWith("ALTER"))
                {
                    // Stored Procedure
                    var spMatch = Regex.Match(line,
                        @"(?:CREATE|ALTER)\s+(?:OR\s+ALTER\s+)?PROCEDURE\s+(?:\[?(\w+)\]?\.)?(?:\[?(\w+)\]?)",
                        RegexOptions.IgnoreCase);

                    if (spMatch.Success && spMatch.Groups.Count >= 3)
                    {
                        return new SqlStructureInfo
                        {
                            Type = "Stored Procedure",
                            Schema = spMatch.Groups[1].Success ? spMatch.Groups[1].Value : "dbo",
                            Name = spMatch.Groups[2].Value,
                            IsStructure = true,
                            Icon = GetStructureIcon("StoredProcedure")
                        };
                    }

                    // View
                    var viewMatch = Regex.Match(line,
                        @"(?:CREATE|ALTER)\s+(?:OR\s+ALTER\s+)?VIEW\s+(?:\[?(\w+)\]?\.)?(?:\[?(\w+)\]?)",
                        RegexOptions.IgnoreCase);

                    if (viewMatch.Success && viewMatch.Groups.Count >= 3)
                    {
                        return new SqlStructureInfo
                        {
                            Type = "View",
                            Schema = viewMatch.Groups[1].Success ? viewMatch.Groups[1].Value : "dbo",
                            Name = viewMatch.Groups[2].Value,
                            IsStructure = true,
                            Icon = GetStructureIcon("View")
                        };
                    }

                    // Function
                    var funcMatch = Regex.Match(line,
                        @"(?:CREATE|ALTER)\s+(?:OR\s+ALTER\s+)?FUNCTION\s+(?:\[?(\w+)\]?\.)?(?:\[?(\w+)\]?)",
                        RegexOptions.IgnoreCase);

                    if (funcMatch.Success && funcMatch.Groups.Count >= 3)
                    {
                        return new SqlStructureInfo
                        {
                            Type = "Function",
                            Schema = funcMatch.Groups[1].Success ? funcMatch.Groups[1].Value : "dbo",
                            Name = funcMatch.Groups[2].Value,
                            IsStructure = true,
                            Icon = GetStructureIcon("Function")
                        };
                    }

                    // Table
                    var tableMatch = Regex.Match(line,
                        @"CREATE\s+TABLE\s+(?:\[?(\w+)\]?\.)?(?:\[?(\w+)\]?)",
                        RegexOptions.IgnoreCase);

                    if (tableMatch.Success && tableMatch.Groups.Count >= 3)
                    {
                        return new SqlStructureInfo
                        {
                            Type = "Table",
                            Schema = tableMatch.Groups[1].Success ? tableMatch.Groups[1].Value : "dbo",
                            Name = tableMatch.Groups[2].Value,
                            IsStructure = true,
                            Icon = GetStructureIcon("Table")
                        };
                    }

                    // Trigger
                    var triggerMatch = Regex.Match(line,
                        @"(?:CREATE|ALTER)\s+(?:OR\s+ALTER\s+)?TRIGGER\s+(?:\[?(\w+)\]?\.)?(?:\[?(\w+)\]?)",
                        RegexOptions.IgnoreCase);

                    if (triggerMatch.Success && triggerMatch.Groups.Count >= 3)
                    {
                        return new SqlStructureInfo
                        {
                            Type = "Trigger",
                            Schema = triggerMatch.Groups[1].Success ? triggerMatch.Groups[1].Value : "dbo",
                            Name = triggerMatch.Groups[2].Value,
                            IsStructure = true,
                            Icon = GetStructureIcon("Trigger")
                        };
                    }
                }
            }

            return new SqlStructureInfo { Type = "", Name = "", Schema = "dbo", IsStructure = false };
        }
        private string GetStructureIcon(string type)
        {
            var icons = new Dictionary<string, string>
            {
                { "StoredProcedure", "<i class=\"fas fa-cogs text-primary\"></i>" },
                { "View", "<i class=\"fas fa-eye text-info\"></i>" },
                { "Function", "<i class=\"fas fa-cog text-warning\"></i>" },
                { "Table", "<i class=\"fas fa-table text-success\"></i>" },
                { "Trigger", "<i class=\"fas fa-bolt text-danger\"></i>" },
                { "sql", "<i class=\"fas fa-file-code text-secondary\"></i>" }
            };

            return icons.ContainsKey(type) ? icons[type] : icons["sql"];
        }

    }
}
