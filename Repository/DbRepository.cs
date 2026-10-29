using Mapster;
using MapsterMapper;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PasadorScript.DbModels;
using PasadorScript.DbModels.Constants;
using PasadorScript.DTO.AzureDev;
using PasadorScript.DTO.Graph;
using PasadorScript.DTO.Informes;
using PasadorScript.DTO.TeamDev;
using PasadorScript.Extensions;
using PasadorScript.Helpers;
using PasadorScript.ViewModels;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Text;
using System.Text.Json;
using X.PagedList;
using X.PagedList.Extensions;
using static System.Net.Mime.MediaTypeNames;

namespace PasadorScript.Repository
{
    public class DbRepository : IDbRepository, IEventObserver<PullRequestUpdated>
    {
        private readonly NuevoP229Context _context;
        private readonly IConfiguration _configuration;
        private readonly IMapper _mapper;
        private readonly ILogger<DbRepository> _logger;

        public DbRepository(NuevoP229Context context, IConfiguration configuration, IMapper mapper, ILogger<DbRepository> logger)
        {
            _context = context;
            _configuration = configuration;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<Script> AddPasaje(ScriptRequestViewModel model)
        {
            var script = new Script()
            {
                Aplicacion = model.RepositoryId,
                AzureProjectType = string.IsNullOrWhiteSpace(model.AzureProjectType)
                    ? AzureProjectType.Default.ToProjectTypeString()
                    : model.AzureProjectType,
                Descripcion = model.Descripcion,
                CommitId = model.CommitId,
                BranchNameDesa = model.FolderAndBranchNameDesa!,
                Usuario = model.Usuario!,
                NroIncidente = model.Numero,
                IdTipoCambio = model.Tipo,
                FechaCreacion = DateTime.Now,
                PullRequestIdQa = model.PullRequestIdQA,
                Files = model.JsonVirtualFiles,
                IsHotfix = model.IsHotfix,
                TareasJson = TareasJsonHelper.Normalize(model.TareasJson)
            };
            _context.Scripts.Add(script);
            await _context.SaveChangesAsync();
            await _context.Entry(script)
                .Reference(p => p.IdTipoCambioNavigation)
                .LoadAsync();
            return script;

        }

        public async Task<Script> UpdatePasaje(ScriptRequestViewModel model)
        {
            var script = await _context.Scripts.FirstOrDefaultAsync(x => x.IdScript == model.IdScript);
            if (script != null)
            {
                script.AzureProjectType = model.AzureProjectType;
                script.Aplicacion = model.RepositoryId;
                script.Descripcion = model.Descripcion;
                script.CommitId = model.CommitId;
                script.BranchNameDesa = model.FolderAndBranchNameDesa!;
                script.Usuario = model.Usuario!;
                script.NroIncidente = model.Numero;
                script.IdTipoCambio = model.Tipo;
                script.FechaCreacion = DateTime.Now;
                script.PullRequestIdQa = model.PullRequestIdQA;
                script.Files = model.JsonVirtualFiles;
                script.IsHotfix = model.IsHotfix;
                script.TareasJson = TareasJsonHelper.Normalize(model.TareasJson);
                await _context.SaveChangesAsync();
            }
            ;

            return script;

        }        

        public async Task<Script> GetScriptByPullRequest(int pullRequestId)
        {
            var s = await _context.Scripts.FirstOrDefaultAsync(x => x.PullRequestIdQa == pullRequestId);
            return s;
        }

        /// <inheritdoc />
        public async Task<bool?> GetIsHotfixByPullRequestAsync(int pullRequestId)
        {
            var envio = await BuscarEnvioPorPullRequestAsync(pullRequestId, soloLectura: true);
            return envio == null ? null : envio.IsHotfix;
        }

        /// <inheritdoc />
        public async Task<bool> GuardarSinPruebaPorPullRequestAsync(int pullRequestId, bool sinPrueba)
        {
            var envio = await BuscarEnvioPorPullRequestAsync(pullRequestId, soloLectura: false);
            if (envio == null)
                return false;

            envio.SinPrueba = sinPrueba;
            await _context.SaveChangesAsync();
            return true;
        }

        /// <summary>
        /// Scripts por PullRequestIdQa o PullRequestIdprd, después BuildsGx por IdPr, después EnviosDlya por IdPr.
        /// </summary>
        private async Task<EnvioPorPullRequest?> BuscarEnvioPorPullRequestAsync(int pullRequestId, bool soloLectura)
        {
            if (pullRequestId <= 0)
                return null;

            IQueryable<Script> scripts = _context.Scripts;
            IQueryable<BuildsGx> builds = _context.BuildsGxes;
            IQueryable<EnviosDlya> envios = _context.EnviosDlyas;
            if (soloLectura)
            {
                scripts = scripts.AsNoTracking();
                builds = builds.AsNoTracking();
                envios = envios.AsNoTracking();
            }

            var script = await scripts.FirstOrDefaultAsync(x =>
                x.PullRequestIdQa == pullRequestId || x.PullRequestIdprd == pullRequestId);
            if (script != null)
                return new EnvioPorPullRequest(script);

            var buildGx = await builds.FirstOrDefaultAsync(x => x.IdPr == pullRequestId);
            if (buildGx != null)
                return new EnvioPorPullRequest(buildGx);

            var envioDlya = await envios.FirstOrDefaultAsync(x => x.IdPr == pullRequestId);
            if (envioDlya != null)
                return new EnvioPorPullRequest(envioDlya);

            return null;
        }

        private sealed class EnvioPorPullRequest
        {
            private readonly Script? _script;
            private readonly BuildsGx? _buildGx;
            private readonly EnviosDlya? _envioDlya;

            public EnvioPorPullRequest(Script script) => _script = script;

            public EnvioPorPullRequest(BuildsGx buildGx) => _buildGx = buildGx;

            public EnvioPorPullRequest(EnviosDlya envioDlya) => _envioDlya = envioDlya;

            public bool IsHotfix => _script?.IsHotfix ?? _buildGx?.IsHotfix ?? _envioDlya!.IsHotfix;

            public bool SinPrueba
            {
                set
                {
                    if (_script != null)
                        _script.SinPrueba = value;
                    else if (_buildGx != null)
                        _buildGx.SinPrueba = value;
                    else
                        _envioDlya!.SinPrueba = value;
                }
            }
        }


        public async Task<DbAction> UpdatePasaje(ContentFileView model)
        {
            var s = await _context.Scripts.FirstOrDefaultAsync(x => x.IdScript == model.IdScript);
            if (s != null)
            {
                if (s.PullRequestIdQa.HasValue && s.PullRequestIdQa.Value != 0 && model.PullRequestIdQA == null)
                {
                    //histórico de pr QA
                    s.PullRequests.Add(new PullRequest()
                    {
                        IdPr = s.PullRequestIdQa.Value,
                        QaStage = true
                    });
                }
                if (s.PullRequestIdprd.HasValue && s.PullRequestIdprd.Value != 0 && model.PullRequestIdPR == null)
                {
                    s.PullRequests.Add(new PullRequest()
                    {
                        IdPr = s.PullRequestIdprd.Value,
                        QaStage = false
                    });
                }

                s.BranchNamePr = model.BranchNamePR;
                s.BranchNameQa = model.BranchNameQA;
                s.BranchNameDesa = model.BranchNameDesa;
                s.Aplicacion = model.RepositoryId;
                s.PullRequestIdQa = model.PullRequestIdQA;
                s.PullRequestIdprd = model.PullRequestIdPR;

                try
                {
                    await _context.SaveChangesAsync();
                    return new DbAction() { Result = true };
                }
                catch (Exception ex)
                {
                    return new DbAction() { Message = ex.Message, Result = false };
                }
            }
            return null;
        }

        public async Task<DbAction> UpdatePasaje(BuildGXViewModelAndCommitsViewModel model)
        {
            var s = await _context.BuildsGxes.FirstOrDefaultAsync(x => x.IdBuildGx == model.modelCambio.IdBuildGx);
            if (s != null)
            {
                if (s.IdPr.HasValue && s.IdPr.Value != 0 && !model.modelCambio.IdPr.HasValue)
                {
                    //histórico de pr QA
                    s.PullRequestsBuilds.Add(new PullRequestsBuild()
                    {
                        IdPr = s.IdPr.Value
                    });
                }
                s.IdPr = model.modelCambio.IdPr;
                s.BranchName = model.modelCambio.BranchName;
                try
                {
                    await _context.SaveChangesAsync();
                    return new DbAction() { Result = true };
                }
                catch (Exception ex)
                {
                    return new DbAction() { Message = ex.Message, Result = false };
                }
            }
            return null;
        }

        public async Task<DbAction> UpdatePasaje(BuildsGx model)
        {
            var s = await _context.BuildsGxes.FirstOrDefaultAsync(x => x.IdBuildGx == model.IdBuildGx);
            if (s != null)
            {
                if (s.IdPr.HasValue && s.IdPr.Value != 0 && !model.IdPr.HasValue)
                {
                    //histórico de pr QA
                    s.PullRequestsBuilds.Add(new PullRequestsBuild()
                    {
                        IdPr = s.IdPr.Value
                    });
                }
                s.IdPr = model.IdPr;
                s.BranchName = model.BranchName;
                try
                {
                    await _context.SaveChangesAsync();
                    return new DbAction() { Result = true };
                }
                catch (Exception ex)
                {
                    return new DbAction() { Message = ex.Message, Result = false };
                }
            }
            return null;
        }

        public async Task<ICollection<TiposCambio>> GetTiposCambios()
        {
            return await _context.TiposCambios.ToListAsync();
        }

        public async Task<ICollection<Script>> GetScripts(string? username)
        {
            return await _context.Scripts.Where(x => x.Usuario == username).ToListAsync();
        }

        public async Task<SearchScripts> GetScripts(bool? deleted, string? username, string? descripcion, string? repositoryId = null, int page = 1, int idEstado = 0, List<int>? prIds = null, int? nroIncidente = null)
        {
            // Configuración de paginación
            var registrosPorPagina = int.Parse(_configuration["Pagination:RegistrosPorPagina"]!);
            var skip = (page - 1) * registrosPorPagina;

            // Construir query base para los datos
            IQueryable<Script> query = _context.Scripts;

            // Aplicar filtros
            if (!string.IsNullOrWhiteSpace(descripcion))
                query = query.Where(s => EF.Functions.Like(s.Descripcion, $"%{descripcion}%"));

            if (!string.IsNullOrWhiteSpace(username))
                query = query.Where(s => s.Usuario == username);

            if (!string.IsNullOrWhiteSpace(repositoryId))
                query = query.Where(s => s.Aplicacion == repositoryId);

            if (prIds != null)
                query = query.Where(s => prIds.Contains(s.PullRequestIdQa ?? 0));

            if (deleted.HasValue)
                query = query.Where(s => s.Deleted == deleted.Value);

            if (nroIncidente.HasValue && nroIncidente.Value > 0)
                query = query.Where(s => s.NroIncidente == nroIncidente.Value);

            // Obtener el total de registros ANTES de aplicar paginación
            var totalRegistros = await query.CountAsync();

            // Aplicar ordenamiento y paginación para los datos
            var pagedQuery = query.OrderByDescending(s => s.IdScript)
                                 .Skip(skip)
                                 .Take(registrosPorPagina);

            // Ejecutar solo la consulta paginada
            var list = await pagedQuery.ToListAsync();

            // Calcular total de páginas
            int totalPaginas = (int)Math.Ceiling((double)totalRegistros / registrosPorPagina);

            // Mapear y retornar
            var listVm = _mapper.Map<List<ScriptViewModel>>(list);

            // Crear PagedList con el total de registros
            var pagedList = new StaticPagedList<ScriptViewModel>(
                listVm,
                page,
                registrosPorPagina,
                totalRegistros
            );

            return new SearchScripts()
            {
                PaginaActual = page,
                RegistrosPorPagina = registrosPorPagina,
                IdEstado = idEstado,
                TotalRegistros = totalRegistros,
                Resultado = pagedList  // Usar el StaticPagedList creado
            };
        }

        public async Task<SearchCommits> GetBuildsGx(bool? deleted, string? repositoryId, string? username, string? descripcion, int page = 1, List<int>? prIds = null, int? nroIncidente = null, string? objetoGx = null)
        {

            // Configuración de paginación
            var registrosPorPagina = int.Parse(_configuration["Pagination:RegistrosPorPagina"]!);
            var skip = (page - 1) * registrosPorPagina;

            // Construir query base para los datos. Sin texto no se toca commitListJson.
            var terminoObjeto = objetoGx?.Trim();
            if (terminoObjeto?.Length > 100)
                terminoObjeto = terminoObjeto[..100];

            IQueryable<BuildsGx> query = string.IsNullOrEmpty(terminoObjeto)
                ? _context.BuildsGxes
                : BuildsGxPorObjeto(terminoObjeto);

            // Aplicar filtros
            if (!string.IsNullOrWhiteSpace(descripcion))
                query = query.Where(s => EF.Functions.Like(s.Descripcion, $"%{descripcion}%"));

            if (!string.IsNullOrWhiteSpace(username))
                query = query.Where(s => s.Usuario == username);

            if (!string.IsNullOrWhiteSpace(repositoryId))
                query = query.Where(s => s.RepositoryId == repositoryId);

            if (prIds != null)
                query = query.Where(s => prIds.Contains(s.IdPr ?? 0));

            if (deleted.HasValue)
                query = query.Where(s => s.Deleted == deleted.Value);

            if (nroIncidente.HasValue && nroIncidente.Value > 0)
                query = query.Where(s => s.NroIncidente == nroIncidente.Value);

            // Obtener el total de registros ANTES de aplicar paginación
            var totalRegistros = await query.CountAsync();

            // Aplicar ordenamiento y paginación para los datos
            var pagedQuery = query.OrderByDescending(s => s.IdBuildGx)
                                 .Skip(skip)
                                 .Take(registrosPorPagina);

            // Ejecutar solo la consulta paginada
            var list = await pagedQuery.ToListAsync();

            // Calcular total de páginas
            int totalPaginas = (int)Math.Ceiling((double)totalRegistros / registrosPorPagina);

            // Mapear y retornar
            var listVm = _mapper.Map<List<BuildGXViewModel>>(list);

            // Crear PagedList con el total de registros
            var pagedList = new StaticPagedList<BuildGXViewModel>(
                listVm,
                page,
                registrosPorPagina,
                totalRegistros
            );

            return new SearchCommits()
            {
                PaginaActual = page,
                RegistrosPorPagina = registrosPorPagina,
                TotalRegistros = totalRegistros,
                Resultado = pagedList  // Usar el StaticPagedList creado
            };
        }

        private IQueryable<BuildsGx> BuildsGxPorObjeto(string termino)
        {
            return _context.BuildsGxes.FromSqlInterpolated($"""
                SELECT *
                FROM [BuildsGX] AS b
                WHERE ISJSON(b.[commitListJson]) = 1
                  AND EXISTS (
                      SELECT 1
                      FROM OPENJSON(
                          CASE
                              WHEN LEFT(LTRIM(b.[commitListJson]), 1) = N'[' THEN b.[commitListJson]
                          END
                      ) AS commits
                      CROSS APPLY OPENJSON(
                          CASE
                              WHEN commits.[type] = 5 THEN commits.[value]
                          END
                      ) AS commitProps
                      CROSS APPLY OPENJSON(
                          CASE
                              WHEN commitProps.[type] = 4 AND LOWER(commitProps.[key]) = N'actions' THEN commitProps.[value]
                          END
                      ) AS actions
                      CROSS APPLY OPENJSON(
                          CASE
                              WHEN actions.[type] = 5 THEN actions.[value]
                          END
                      ) AS actionProps
                      WHERE actionProps.[type] = 1
                        AND LOWER(actionProps.[key]) = N'objectname'
                        AND LTRIM(RTRIM(actionProps.[value])) <> N''
                        AND CHARINDEX(LOWER({termino}), LOWER(LTRIM(RTRIM(actionProps.[value])))) > 0
                  )
                """);
        }

        public async Task<BuildGXViewModel> BuildGxToViewModel(int id)
        {
            var build = await _context.BuildsGxes.FirstOrDefaultAsync(x => x.IdBuildGx == id);
            BuildGXViewModel r = null;
            if (build != null)
            {
                r = new BuildGXViewModel()
                {
                    IdBuildGx = build.IdBuildGx,
                    BranchName = build.BranchName,
                    CommitListJson = build.CommitListJson,
                    Descripcion = build.Descripcion,
                    DescripcionTipoCambio = build.IdTipoCambioNavigation.Descripcion,
                    FechaCreacion = build.FechaCreacion,
                    IdTipoCambio = build.IdTipoCambio,
                    NroIncidente = build.NroIncidente,
                    Usuario = build.Usuario,
                    IdPr = build.IdPr,
                    BtRepo = build.RepositoryId
                };
                var commits = JsonSerializer.Deserialize<ICollection<CommitsTeamDev>>(build.CommitListJson);
            }
            return r;
        }

        public async Task<ScriptRequestViewModel> ScriptToRequestViewModel(int id)
        {
            var script = await _context.Scripts.FirstOrDefaultAsync(x => x.IdScript == id);
            ScriptRequestViewModel r = null;
            if (script != null)
            {
                r = new ScriptRequestViewModel()
                {
                    IdScript = script.IdScript,
                    Descripcion = script.Descripcion,
                    Numero = script.NroIncidente,
                    RepositoryId = script.Aplicacion,
                    Tipo = script.IdTipoCambio,
                    FolderAndBranchNameDesa = script.BranchNameDesa,
                    Usuario = script.Usuario,
                    CommitId = script.CommitId,
                    PullRequestIdPR = script.PullRequestIdprd,
                    PullRequestIdQA = script.PullRequestIdQa,
                    JsonVirtualFiles = script.Files,
                    IsHotfix = script.IsHotfix,
                    TareasJson = script.TareasJson
                };
            }
            return r;
        }

        public async Task<Script?> GetScriptById(int id)
        {
            return await _context.Scripts.FirstOrDefaultAsync(x => x.IdScript == id);
        }

        public async Task<ContentFileView> ScriptToViewModel(int id)
        {
            var script = await _context.Scripts.FirstOrDefaultAsync(x => x.IdScript == id);
            ContentFileView r = null;
            if (script != null)
            {
                r = new ContentFileView()
                {
                    IdScript = id,
                    DescripcionScript = script.Descripcion,
                    DescripcionTipoPasaje = script.IdTipoCambioNavigation.Descripcion,
                    NumeroIncidente = script.NroIncidente,
                    RepositoryId = script.Aplicacion,
                    PullRequestIdPR = script.PullRequestIdprd,
                    PullRequestIdQA = script.PullRequestIdQa,
                    BranchNameDesa = script.BranchNameDesa,
                    BranchNameQA = script.BranchNameQa,
                    BranchNamePR = script.BranchNamePr,
                    FilesJson = script.Files,
                    Usuario = script.Usuario,
                    AzureProjectType = script.AzureProjectType,
                    TareasJson = script.TareasJson
                };

                foreach (var hpr in script.PullRequests)
                {
                    r.PullRequestHistoricos.Add(hpr);
                }
            }
            return r;
        }

        public async Task<PasajeConPullRequest> IdToGenericModel(bool gx, int id)
        {
            PasajeConPullRequest r = null;
            if (!gx)
            {
                var script = await _context.Scripts.FirstOrDefaultAsync(x => x.IdScript == id);

                if (script != null)
                {
                    r = new PasajeConPullRequest()
                    {
                        IdScript = id,
                        DescripcionScript = script.Descripcion,
                        DescripcionTipoPasaje = script.IdTipoCambioNavigation.Descripcion,
                        NumeroIncidente = script.NroIncidente,
                        RepositoryId = script.Aplicacion,
                        AzureProjectType = script.AzureProjectType,
                        PullRequestId = script.PullRequestIdQa,
                        BranchNamePR = script.BranchNameDesa,

                    };

                    foreach (var hpr in script.PullRequests)
                    {
                        r.PullRequestHistoricos.Add(new GenericPrHist()
                        {
                            Id = hpr.IdScript,
                            IdPr = hpr.IdPr,
                        });
                    }
                }
            }
            else
            {
                var build = await _context.BuildsGxes.FirstOrDefaultAsync(x => x.IdBuildGx == id);
                if (build != null)
                {
                    r = new PasajeConPullRequest()
                    {
                        IdScript = id,
                        DescripcionScript = build.Descripcion,
                        DescripcionTipoPasaje = build.IdTipoCambioNavigation.Descripcion,
                        NumeroIncidente = build.NroIncidente,
                        RepositoryId = build.RepositoryId,
                        PullRequestId = build.IdPr,
                        BranchNamePR = build.BranchName,
                        AzureProjectType = build.AzureProjectType
                    };

                    foreach (var hpr in build.PullRequestsBuilds)
                    {
                        r.PullRequestHistoricos.Add(new GenericPrHist()
                        {
                            Id = hpr.IdBuild,
                            IdPr = hpr.IdPr,
                        });
                    }
                }
            }
            if (r != null)
            {
                r.EsGx = gx;
            }
            return r;
        }

        public async Task<string> GetDescripcionPerfil(int id)
        {
            var r = await _context.RolesMaps.FirstOrDefaultAsync(x => x.IdRol == id);
            if (r != null)
            {
                return r.Descripcion;
            }
            return "";
        }

        public async Task OnEventAsync(PullRequestUpdated evento)
        {
            var s = await _context.Scripts.Where(x => x.PullRequestIdQa == evento.PullRequestId).FirstOrDefaultAsync();
            if (s != null)
            {
                s.PullRequestIdprd = evento.NewPullRequestId;
                s.BranchNameQa = evento.BranchName;
                await _context.SaveChangesAsync();
            }
        }

        public async Task<IEnumerable<Pat>> GetPats(string? username)
        {
            username = username.ToLower();
            if (username == null)
                return await _context.Pats.ToListAsync();
            else
                return await _context.Pats.Where(x => x.Username == username).ToListAsync();
        }

        public async Task SaveNewPat(string username, string pat)
        {
            if (!String.IsNullOrEmpty(username))
            {
                var p = await _context.Pats.FirstOrDefaultAsync(x => x.Username == username);
                if (p != null)
                {
                    p.Pat1 = pat;
                    p.FechaCreacion = DateTime.Now;
                }
                else
                {
                    _context.Pats.Add(new Pat()
                    {
                        Username = username,
                        Pat1 = pat,
                        FechaCreacion = DateTime.Now
                    });
                }
                await _context.SaveChangesAsync();
            }
        }

        public async Task<BuildsGx> GetBuildById(int id)
        {
            return await _context.BuildsGxes.FirstOrDefaultAsync(x => x.IdBuildGx == id);
        }

        public async Task<BuildsGx> GetBuildByIdPullRequest(int id)
        {
            return await _context.BuildsGxes.FirstOrDefaultAsync(x => x.IdPr == id);
        }

        public async Task<BuildsGx> AddBuild(BuildGXViewModelAndCommitsViewModel model)
        {
            var nuevo = new BuildsGx()
            {
                CommitListJson = JsonSerializer.Serialize(model.commits),
                Descripcion = model.modelCambio.Descripcion,
                NroIncidente = model.modelCambio.NroIncidente,
                IdTipoCambio = model.modelCambio.IdTipoCambio,
                Usuario = model.modelCambio.Usuario,
                BranchName = model.modelCambio.BranchName,
                IdPr = model.modelCambio.IdPr,
                AzureProjectType = AzureProjectType.GeneXus.ToProjectTypeString(),
                RepositoryId = model.modelCambio.BtRepo,
                IsHotfix = model.modelCambio.IsHotfix,
                TareasJson = TareasJsonHelper.Normalize(model.modelCambio.TareasJson)
            };
            _context.BuildsGxes.Add(nuevo);
            await _context.SaveChangesAsync();
            await _context.Entry(nuevo)
                .Reference(p => p.IdTipoCambioNavigation)
                .LoadAsync();
            return nuevo;
        }

        public async Task<BuildsGx> Edit(BuildGXViewModelAndCommitsViewModel model)
        {
            var b = await _context.BuildsGxes.FirstOrDefaultAsync(x => x.IdBuildGx == model.modelCambio.IdBuildGx);
            if (b != null)
            {
                b.Descripcion = model.modelCambio.Descripcion;
                b.IdTipoCambio = model.modelCambio.IdTipoCambio;
                b.CommitListJson = JsonSerializer.Serialize(model.commits);
                b.BranchName = model.modelCambio.BranchName;
                b.IdPr = model.modelCambio.IdPr;
                b.IsHotfix = model.modelCambio.IsHotfix;
                b.TareasJson = TareasJsonHelper.Normalize(model.modelCambio.TareasJson);
                await _context.SaveChangesAsync();
            }
            return b;
        }

        public async Task<bool> DeleteScript(int id)
        {
            // 1. Obtener la entidad
            var registro = await _context.Scripts.FindAsync(id); // Reemplaza TuEntidad con el nombre de tu clase entidad

            if (registro == null)
            {
                return false; // O lanzar una excepción, etc.
            }

            _context.Scripts.Remove(registro); // Reemplaza TuEntidad con el nombre de tu clase entidad

            // 3. Guardar los cambios
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteGx(int idBuild)
        {
            // 1. Obtener la entidad
            var registro = await _context.BuildsGxes.FindAsync(idBuild); // Reemplaza TuEntidad con el nombre de tu clase entidad

            if (registro == null)
            {
                return false; // O lanzar una excepción, etc.
            }

            _context.BuildsGxes.Remove(registro); // Reemplaza TuEntidad con el nombre de tu clase entidad

            // 3. Guardar los cambios
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeletePat(string username)
        {
            var reg = await _context.Pats.FirstOrDefaultAsync(x => x.Username == username);
            if (reg == null)
                return false;
            _context.Pats.Remove(reg);
            await _context.SaveChangesAsync();
            return true;
        }



        public async Task<ICollection<string>> GetUsuarios(TablasType tablasType, string term)
        {
            List<string> usuarios = new List<string>();
            if (tablasType == TablasType.Scripts)
            {
                usuarios = await _context.Scripts.Where(s => EF.Functions.Like(s.Usuario, $"%{term}%")).Select(x => x.Usuario).Distinct().ToListAsync();
            }
            else if (tablasType == TablasType.Dlya)
            {
                usuarios = await _context.EnviosDlyas.Where(s => EF.Functions.Like(s.Usuario, $"%{term}%")).Select(x => x.Usuario).Distinct().ToListAsync();
            }
            else if (tablasType == TablasType.Builds)
            {
                usuarios = await _context.BuildsGxes.Where(s => EF.Functions.Like(s.Usuario, $"%{term}%")).Select(x => x.Usuario).Distinct().ToListAsync();
            }
            else if (tablasType == TablasType.Todos)
            {
                var scripts = _context.Scripts.Where(s => EF.Functions.Like(s.Usuario, $"%{term}%")).Select(x => x.Usuario);
                var gx = _context.BuildsGxes.Where(s => EF.Functions.Like(s.Usuario, $"%{term}%")).Select(x => x.Usuario);
                var dlya = _context.EnviosDlyas.Where(s => EF.Functions.Like(s.Usuario, $"%{term}%")).Select(x => x.Usuario);
                usuarios = await scripts.Concat(gx).Concat(dlya).Distinct().ToListAsync();
            }
            return usuarios;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<EnvioInformeReleaseDto>> GetEnviosInformeReleasesAsync(
            DateTime desde,
            DateTime hasta,
            IReadOnlyCollection<string>? repoKeys,
            bool incluirScript,
            bool incluirGx,
            bool incluirDlya,
            bool? hotfix,
            string? usuario,
            int? nroIncidente,
            string? tipoReferencia)
        {
            if (!incluirScript && !incluirGx && !incluirDlya)
                return Array.Empty<EnvioInformeReleaseDto>();

            var keys = repoKeys?.Where(k => !string.IsNullOrWhiteSpace(k)).Distinct().ToList();
            var filtrarRepos = keys is { Count: > 0 };
            var filtrarUsuario = !string.IsNullOrWhiteSpace(usuario);
            var filtrarReferencia = nroIncidente.HasValue && nroIncidente.Value > 0;
            var tipoRef = string.IsNullOrWhiteSpace(tipoReferencia) ? null : tipoReferencia.Trim();
            var filtrarTipoReferencia = tipoRef != null;

            var parametros = new List<SqlParameter>
            {
                new("@desde", desde),
                new("@hasta", hasta)
            };
            var reposIn = AgregarParametrosRepos(parametros, filtrarRepos ? keys : null);
            if (filtrarUsuario)
                parametros.Add(new SqlParameter("@usuario", usuario));
            if (hotfix.HasValue)
                parametros.Add(new SqlParameter("@hotfix", hotfix.Value));
            if (filtrarReferencia)
                parametros.Add(new SqlParameter("@nroIncidente", nroIncidente!.Value));
            if (filtrarTipoReferencia)
                parametros.Add(new SqlParameter("@tipoReferencia", tipoRef));

            var sql = new StringBuilder();
            var esPrimera = true;
            if (incluirScript)
            {
                AgregarRamaInforme(sql, esPrimera, RamaScript(reposIn, filtrarUsuario, hotfix, filtrarReferencia, filtrarTipoReferencia));
                esPrimera = false;
            }

            if (incluirGx)
            {
                AgregarRamaInforme(sql, esPrimera, RamaGx(reposIn, filtrarUsuario, hotfix, filtrarReferencia, filtrarTipoReferencia));
                esPrimera = false;
            }

            if (incluirDlya)
                AgregarRamaInforme(sql, esPrimera, RamaDlya(reposIn, filtrarUsuario, hotfix, filtrarReferencia, filtrarTipoReferencia));

            // El default de EF (30s) corta rangos de varios meses por el OPENJSON de cada fila.
            const int timeoutInformeSegundos = 180;
            var timeoutAnterior = _context.Database.GetCommandTimeout();
            _context.Database.SetCommandTimeout(timeoutInformeSegundos);
            try
            {
                return await _context.Database
                    .SqlQueryRaw<EnvioInformeReleaseDto>(sql.ToString(), parametros.Cast<object>().ToArray())
                    .OrderByDescending(x => x.FechaCreacion)
                    .ToListAsync();
            }
            finally
            {
                _context.Database.SetCommandTimeout(timeoutAnterior);
            }
        }

        /// <summary>
        /// Arma la lista <c>IN</c> de repositorios con un parametro por valor.
        /// </summary>
        private static string? AgregarParametrosRepos(List<SqlParameter> parametros, IReadOnlyList<string>? keys)
        {
            if (keys is not { Count: > 0 })
                return null;

            var nombres = new string[keys.Count];
            for (var i = 0; i < keys.Count; i++)
            {
                var nombre = "@repo" + i;
                nombres[i] = nombre;
                parametros.Add(new SqlParameter(nombre, keys[i]));
            }

            return string.Join(", ", nombres);
        }

        private static void AgregarRamaInforme(StringBuilder sql, bool esPrimera, string rama)
        {
            if (!esPrimera)
                sql.AppendLine("UNION ALL");

            sql.AppendLine(rama);
        }

        private static string RamaScript(
            string? reposIn,
            bool filtrarUsuario,
            bool? hotfix,
            bool filtrarReferencia,
            bool filtrarTipoReferencia)
        {
            return $"""
                SELECT
                    N'{TipoEnvioInforme.Script}' AS Tipo,
                    s.idScript AS IdCambio,
                    s.descripcion AS Descripcion,
                    s.isHotfix AS IsHotfix,
                    s.sinPrueba AS SinPrueba,
                    s.fechaCreacion AS FechaCreacion,
                    s.usuario AS Usuario,
                    s.pullRequestIdQA AS PullRequestId,
                    s.aplicacion AS RepoKey,
                    s.AzureProjectType AS AzureProjectType,
                    s.nroIncidente AS NroIncidente,
                    t.descripcion AS TipoCambioDescripcion,
                    {SqlNombresArchivo("s.[files]")} AS ObjetosArchivos
                FROM Scripts AS s
                INNER JOIN TiposCambios AS t ON t.idTipoCambio = s.idTipoCambio
                {WhereInforme("s.aplicacion", false, reposIn, filtrarUsuario, hotfix, filtrarReferencia, filtrarTipoReferencia)}
                """;
        }

        private static string RamaGx(
            string? reposIn,
            bool filtrarUsuario,
            bool? hotfix,
            bool filtrarReferencia,
            bool filtrarTipoReferencia)
        {
            return $"""
                SELECT
                    N'{TipoEnvioInforme.Gx}' AS Tipo,
                    s.idBuildGx AS IdCambio,
                    s.descripcion AS Descripcion,
                    s.isHotfix AS IsHotfix,
                    s.sinPrueba AS SinPrueba,
                    s.fechaCreacion AS FechaCreacion,
                    s.usuario AS Usuario,
                    s.idPr AS PullRequestId,
                    ISNULL(s.repositoryId, N'') AS RepoKey,
                    s.AzureProjectType AS AzureProjectType,
                    s.nroIncidente AS NroIncidente,
                    t.descripcion AS TipoCambioDescripcion,
                    {SqlNombresObjetoGx("s.[commitListJson]")} AS ObjetosArchivos
                FROM BuildsGX AS s
                INNER JOIN TiposCambios AS t ON t.idTipoCambio = s.idTipoCambio
                {WhereInforme("s.repositoryId", true, reposIn, filtrarUsuario, hotfix, filtrarReferencia, filtrarTipoReferencia)}
                """;
        }

        private static string RamaDlya(
            string? reposIn,
            bool filtrarUsuario,
            bool? hotfix,
            bool filtrarReferencia,
            bool filtrarTipoReferencia)
        {
            return $"""
                SELECT
                    N'{TipoEnvioInforme.Dlya}' AS Tipo,
                    s.idEnvioDlya AS IdCambio,
                    s.descripcion AS Descripcion,
                    s.isHotfix AS IsHotfix,
                    s.sinPrueba AS SinPrueba,
                    s.fechaCreacion AS FechaCreacion,
                    s.usuario AS Usuario,
                    s.idPr AS PullRequestId,
                    ISNULL(s.repositoryId, N'') AS RepoKey,
                    s.AzureProjectType AS AzureProjectType,
                    s.nroIncidente AS NroIncidente,
                    t.descripcion AS TipoCambioDescripcion,
                    {SqlNombresArchivo("s.[commitListJson]")} AS ObjetosArchivos
                FROM EnviosDlya AS s
                INNER JOIN TiposCambios AS t ON t.idTipoCambio = s.idTipoCambio
                {WhereInforme("s.repositoryId", true, reposIn, filtrarUsuario, hotfix, filtrarReferencia, filtrarTipoReferencia)}
                """;
        }

        /// <summary>
        /// Filtros compartidos. <paramref name="columnaRepo"/> es una columna fija de la rama, no un valor del usuario.
        /// </summary>
        private static string WhereInforme(
            string columnaRepo,
            bool exigirRepoNoNulo,
            string? reposIn,
            bool filtrarUsuario,
            bool? hotfix,
            bool filtrarReferencia,
            bool filtrarTipoReferencia)
        {
            var sql = new StringBuilder("WHERE s.deleted = 0 AND s.fechaCreacion >= @desde AND s.fechaCreacion <= @hasta");
            if (reposIn != null)
            {
                if (exigirRepoNoNulo)
                    sql.Append(" AND ").Append(columnaRepo).Append(" IS NOT NULL");

                sql.Append(" AND ").Append(columnaRepo).Append(" IN (").Append(reposIn).Append(')');
            }

            if (filtrarUsuario)
                sql.Append(" AND s.usuario = @usuario");
            if (hotfix.HasValue)
                sql.Append(" AND s.isHotfix = @hotfix");
            if (filtrarReferencia)
                sql.Append(" AND s.nroIncidente = @nroIncidente");
            if (filtrarTipoReferencia)
                sql.Append(" AND t.descripcion = @tipoReferencia");

            return sql.ToString();
        }

        /// <summary>
        /// Nombres de archivo en el orden del array.
        /// <c>JSON_VALUE</c> toma solo <c>name</c> o <c>Name</c>. El contenido no es una columna del resultado.
        /// </summary>
        private static string SqlNombresArchivo(string columnaJson)
        {
            return $"""
                (
                    SELECT COALESCE(
                        STRING_AGG(CAST(n.segmento AS nvarchar(max)), N', ')
                            WITHIN GROUP (ORDER BY n.orden),
                        N'')
                    FROM (
                        SELECT
                            TRY_CAST(elemento.[key] AS int) AS orden,
                            {SqlSegmentoFinal("ruta.texto")} AS segmento
                        FROM OPENJSON({SqlJsonArray(columnaJson)}) AS elemento
                        CROSS APPLY (
                            SELECT LTRIM(RTRIM(REPLACE(
                                COALESCE(
                                    NULLIF(LTRIM(RTRIM(JSON_VALUE(elemento.[value], '$.name'))), N''),
                                    JSON_VALUE(elemento.[value], '$.Name')),
                                N'\', N'/'))) AS texto
                        ) AS ruta
                        WHERE elemento.[type] = 5
                          AND ruta.texto <> N''
                    ) AS n
                    WHERE n.segmento <> N''
                )
                """;
        }

        /// <summary>
        /// Nombres de objeto GX, en el orden de commits y de acciones.
        /// De cada accion solo se lee <c>objectName</c>.
        /// </summary>
        private static string SqlNombresObjetoGx(string columnaJson)
        {
            return $"""
                (
                    SELECT COALESCE(
                        STRING_AGG(CAST(n.segmento AS nvarchar(max)), N', ')
                            WITHIN GROUP (ORDER BY n.commitOrden, n.accionOrden),
                        N'')
                    FROM (
                        SELECT
                            TRY_CAST(commits.[key] AS int) AS commitOrden,
                            TRY_CAST(accion.[key] AS int) AS accionOrden,
                            {SqlSegmentoFinal("ruta.texto")} AS segmento
                        FROM OPENJSON({SqlJsonArray(columnaJson)}) AS commits
                        CROSS APPLY OPENJSON(
                            CASE
                                WHEN commits.[type] = 5 THEN COALESCE(
                                    JSON_QUERY(commits.[value], '$.actions'),
                                    JSON_QUERY(commits.[value], '$.Actions'))
                            END
                        ) AS accion
                        CROSS APPLY (
                            SELECT LTRIM(RTRIM(REPLACE(
                                COALESCE(
                                    NULLIF(LTRIM(RTRIM(JSON_VALUE(accion.[value], '$.objectName'))), N''),
                                    JSON_VALUE(accion.[value], '$.ObjectName')),
                                N'\', N'/'))) AS texto
                        ) AS ruta
                        WHERE accion.[type] = 5
                          AND ruta.texto <> N''
                    ) AS n
                    WHERE n.segmento <> N''
                )
                """;
        }

        /// <summary>
        /// Devuelve el JSON solo cuando la raiz es un array. Un JSON nulo o invalido no corta la consulta.
        /// </summary>
        private static string SqlJsonArray(string columnaJson)
            => $"""
                CASE
                    WHEN ISJSON({columnaJson}) = 1 AND LEFT(LTRIM({columnaJson}), 1) = N'[' THEN {columnaJson}
                END
                """;

        /// <summary>
        /// Ultimo segmento de la ruta. Un nombre sin barra queda igual.
        /// </summary>
        private static string SqlSegmentoFinal(string expresion)
            => $"""
                CASE
                    WHEN CHARINDEX(N'/', {expresion}) = 0 THEN {expresion}
                    ELSE RIGHT({expresion}, CHARINDEX(N'/', REVERSE({expresion})) - 1)
                END
                """;

        #region Envios Dlya
        public async Task<SearchEnviosDlya> GetEnviosDlyaAsync(bool? deleted, string? username, string? descripcion, string? repositoryId = null, int page = 1, int? nroIncidente = null)
        {

            // Configuración de paginación
            var registrosPorPagina = int.Parse(_configuration["Pagination:RegistrosPorPagina"]!);
            var skip = (page - 1) * registrosPorPagina;

            // Construir query base para los datos
            IQueryable<EnviosDlya> query = _context.EnviosDlyas;

            // Aplicar filtros
            if (!string.IsNullOrWhiteSpace(descripcion))
                query = query.Where(s => EF.Functions.Like(s.Descripcion, $"%{descripcion}%"));

            if (!string.IsNullOrWhiteSpace(username))
                query = query.Where(s => s.Usuario == username);

            if (!string.IsNullOrWhiteSpace(repositoryId))
                query = query.Where(s => s.RepositoryId == repositoryId);

            if (deleted.HasValue)
                query = query.Where(s => s.Deleted == deleted.Value);

            if (nroIncidente.HasValue && nroIncidente.Value > 0)
                query = query.Where(s => s.NroIncidente == nroIncidente.Value);

            // Obtener el total de registros ANTES de aplicar paginación
            var totalRegistros = await query.CountAsync();

            // Aplicar ordenamiento y paginación para los datos
            var pagedQuery = query.OrderByDescending(s => s.IdEnvioDlya)
                                 .Skip(skip)
                                 .Take(registrosPorPagina);

            // Ejecutar solo la consulta paginada
            var list = await pagedQuery.Select(x => new { x.BranchName, x.Descripcion, x.IdEnvioDlya, x.IdPr, x.FechaCreacion, x.IdTipoCambio, x.RepositoryId, x.NroIncidente, x.Usuario }).ToListAsync();

            // Calcular total de páginas
            int totalPaginas = (int)Math.Ceiling((double)totalRegistros / registrosPorPagina);

            // Mapear y retornar
            var listVm = await pagedQuery
    .ProjectToType<EnvioDlyaViewModel>()   // Mapster reemplazo de ProjectTo
    .ToListAsync();

            // Crear PagedList con el total de registros
            var pagedList = new StaticPagedList<EnvioDlyaViewModel>(
                listVm,
                page,
                registrosPorPagina,
                totalRegistros
            );

            return new SearchEnviosDlya()
            {
                PaginaActual = page,
                RegistrosPorPagina = registrosPorPagina,
                TotalRegistros = totalRegistros,
                Resultado = pagedList  // Usar el StaticPagedList creado
            };
        }

        public async Task<EnviosDlya?> GetEnvioDlya(int id)
        {
            return await _context.EnviosDlyas.FirstOrDefaultAsync(x => x.IdEnvioDlya == id);
        }

        public async Task<EnviosDlya?> GetEnvioDlyaByIdPullRequest(int id)
        {
            return await _context.EnviosDlyas.FirstOrDefaultAsync(x => x.IdPr == id);
        }

        public async Task<EnviosDlya> AddEnvioDlya(EnvioDlyaRequestViewModel model)
        {
            var nuevo = new EnviosDlya()
            {
                CommitListJson = JsonSerializer.Serialize(model.FilesRequest),
                Descripcion = model.Descripcion,
                NroIncidente = model.Numero,
                IdTipoCambio = model.Tipo,
                Usuario = model.Usuario!,
                BranchName = model.FolderAndBranchNameDesa,
                IdPr = model.PullRequestId,
                AzureProjectType = AzureProjectType.GeneXus.ToProjectTypeString(),
                RepositoryId = model.RepositoryId,
                IsHotfix = model.IsHotfix,
                TareasJson = TareasJsonHelper.Normalize(model.TareasJson)
            };
            _context.EnviosDlyas.Add(nuevo);
            await _context.SaveChangesAsync();
            await _context.Entry(nuevo)
                .Reference(e => e.IdTipoCambioNavigation)
                .LoadAsync();
            return nuevo;
        }

        public async Task<EnviosDlya?> Edit(EnvioDlyaRequestViewModel model)
        {
            var b = await _context.EnviosDlyas.FirstOrDefaultAsync(x => x.IdEnvioDlya == model.IdEnvioDlya);
            if (b != null)
            {
                b.Descripcion = model.Descripcion;
                b.IdTipoCambio = model.Tipo;
                b.CommitListJson = JsonSerializer.Serialize(model.JsonVirtualFiles);
                b.BranchName = model.FolderAndBranchNameDesa;
                b.IdPr = model.PullRequestId;
                b.IsHotfix = model.IsHotfix;
                b.TareasJson = TareasJsonHelper.Normalize(model.TareasJson);
                await _context.SaveChangesAsync();
            }
            return b;
        }

        public async Task<EnvioDlyaRequestViewModel?> EnvioDlyaToRequestViewModel(int id)
        {
            var envioDlya = await _context.EnviosDlyas.FirstOrDefaultAsync(x => x.IdEnvioDlya == id);
            EnvioDlyaRequestViewModel? r = null;
            if (envioDlya != null)
            {

                r = new EnvioDlyaRequestViewModel()
                {
                    IdEnvioDlya = envioDlya.IdEnvioDlya,
                    Descripcion = envioDlya.Descripcion,
                    Numero = envioDlya.NroIncidente,
                    RepositoryId = envioDlya.RepositoryId!,
                    AzureProjectType = envioDlya.AzureProjectType,
                    Tipo = envioDlya.IdTipoCambio,
                    FolderAndBranchNameDesa = envioDlya.BranchName,
                    Usuario = envioDlya.Usuario,
                    PullRequestId = envioDlya.IdPr,
                    JsonVirtualFiles = envioDlya.CommitListJson,
                    IsHotfix = envioDlya.IsHotfix,
                    TareasJson = envioDlya.TareasJson
                };
            }
            return r;
        }

        public async Task<EnviosDlya> UpdatePasaje(EnvioDlyaRequestViewModel model)
        {
            var script = await _context.EnviosDlyas.FirstOrDefaultAsync(x => x.IdEnvioDlya == model.IdEnvioDlya);
            if (script != null)
            {
                script.RepositoryId = model.RepositoryId;
                script.Descripcion = model.Descripcion;
                script.BranchName = model.FolderAndBranchNameDesa;
                script.Usuario = model.Usuario;
                script.NroIncidente = model.Numero;
                script.IdTipoCambio = model.Tipo;
                script.FechaCreacion = DateTime.Now;
                script.IdPr = model.PullRequestId;
                script.IsHotfix = model.IsHotfix;
                script.TareasJson = TareasJsonHelper.Normalize(model.TareasJson);
                script.CommitListJson = JsonSerializer.Serialize(model.FilesRequest);
                await _context.SaveChangesAsync();
                await _context.Entry(script)
                .Reference(e => e.IdTipoCambioNavigation)
                .LoadAsync();
            }
            ;

            return script;

        }
        public async Task<DbAction> UpdatePasaje(EnvioDlyaViewModelAndCommitsViewModel model)
        {
            var s = await _context.EnviosDlyas.FirstOrDefaultAsync(x => x.IdEnvioDlya == model.modelCambio.IdEnvioDlya);
            if (s != null)
            {
                if (s.IdPr.HasValue && s.IdPr.Value != 0 && !model.modelCambio.IdPr.HasValue)
                {
                    //histórico de pr QA
                    s.PullRequestsEnviosDlyas.Add(new PullRequestsEnviosDlya()
                    {
                        IdPr = s.IdPr.Value
                    });
                }
                s.IdPr = model.modelCambio.IdPr;
                s.BranchName = model.modelCambio.BranchName;
                s.IsHotfix = model.modelCambio.IsHotfix;
                try
                {
                    await _context.SaveChangesAsync();
                    return new DbAction() { Result = true };
                }
                catch (Exception ex)
                {
                    return new DbAction() { Message = ex.Message, Result = false };
                }
            }
            return null;
        }

        public async Task<bool> DeleteEnvioDlya(int idEnvio)
        {
            // 1. Obtener la entidad
            var registro = await _context.EnviosDlyas.FindAsync(idEnvio); // Reemplaza TuEntidad con el nombre de tu clase entidad

            if (registro == null)
            {
                return false; // O lanzar una excepción, etc.
            }

            _context.EnviosDlyas.Remove(registro); // Reemplaza TuEntidad con el nombre de tu clase entidad

            // 3. Guardar los cambios
            await _context.SaveChangesAsync();
            return true;
        }


        public async Task<bool> Descartar(int id, string tableName, bool deleted)
        {
            try
            {
                if (tableName == Helpers.Constants.Tables.Scripts)
                {
                    var script = await _context.Scripts.FindAsync(id);
                    if (script is null)
                    {
                        _logger.LogWarning("No se encontró Script {Id} para descartar", id);
                        return false;
                    }
                    script.Deleted = deleted;
                }
                else if (tableName == Helpers.Constants.Tables.BuildsGX)
                {
                    var build = await _context.BuildsGxes.FindAsync(id);
                    if (build is null)
                    {
                        _logger.LogWarning("No se encontró BuildsGX {Id} para descartar", id);
                        return false;
                    }
                    build.Deleted = deleted;
                }
                else if (tableName == Helpers.Constants.Tables.EnviosDlya)
                {
                    var envio = await _context.EnviosDlyas.FindAsync(id);
                    if (envio is null)
                    {
                        _logger.LogWarning("No se encontró EnviosDlya {Id} para descartar", id);
                        return false;
                    }
                    envio.Deleted = deleted;
                }
                else
                {
                    _logger.LogWarning("Tabla no soportada para Descartar: {TableName}", tableName);
                    return false;
                }

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al descartar {TableName} {Id} (deleted={Deleted})", tableName, id, deleted);
                return false;
            }
        }


        #endregion
    }
}
