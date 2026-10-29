using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PasadorScript.DbModels;
using PasadorScript.ViewModels;

namespace PasadorScript.Controllers
{
    public class ReporteController : Controller
    {
        private readonly NuevoP229Context _context;

        public ReporteController(NuevoP229Context context)
        {
            _context = context;
        }
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            // Obtener todos los roles activos
            var roles = await _context.AdGroupMappings
                .Where(r => r.IsActive)
                .ToListAsync();

            var rolesConPermisos = new List<RolPermisosViewModel>();

            foreach (var rol in roles)
            {
                // Obtener las acciones asociadas a este rol
                var acciones = await (from ra in _context.RolAccions
                                      join a in _context.Acciones on ra.AccionId equals a.AccionId
                                      where ra.GroupGuid == rol.GroupGuid
                                      orderby a.Categoria, a.Orden
                                      select new AccionViewModel
                                      {
                                          AccionId = a.AccionId,
                                          Nombre = a.Nombre,
                                          Descripcion = a.Descripcion!,
                                          Categoria = a.Categoria!
                                      }).ToListAsync();

                rolesConPermisos.Add(new RolPermisosViewModel
                {
                    GroupGuid = rol.GroupGuid,
                    GroupName = rol.GroupName!,
                    Description = rol.Description!,
                    Email = rol.Email!,
                    AccionesPermitidas = acciones
                });
            }

            return View(rolesConPermisos);
        }
        public async Task<ActionResult> ExportarExcel()
        {
            // 1. Obtener los mismos datos que en PermisosPorRol()
            var roles = await _context.AdGroupMappings
                .Where(r => r.IsActive)
                .ToListAsync();

            var rolesConPermisos = new List<RolPermisosViewModel>();

            foreach (var rol in roles)
            {
                var acciones = await (from ra in _context.RolAccions
                                      join a in _context.Acciones on ra.AccionId equals a.AccionId
                                      where ra.GroupGuid == rol.GroupGuid
                                      orderby a.Categoria, a.Orden
                                      select new AccionViewModel
                                      {
                                          AccionId = a.AccionId,
                                          Nombre = a.Nombre,
                                          Descripcion = a.Descripcion!,
                                          Categoria = a.Categoria!
                                      }).ToListAsync();

                rolesConPermisos.Add(new RolPermisosViewModel
                {
                    GroupGuid = rol.GroupGuid,
                    GroupName = rol.GroupName!,
                    Description = rol.Description!,
                    Email = rol.Email!,
                    AccionesPermitidas = acciones
                });
            }

            // 2. Generar el archivo Excel con ClosedXML
            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Permisos por rol");

                // Encabezados
                worksheet.Cell(1, 1).Value = "Rol";
                worksheet.Cell(1, 2).Value = "Descripción Rol";
                worksheet.Cell(1, 3).Value = "Email contacto";
                worksheet.Cell(1, 4).Value = "Categoría";
                worksheet.Cell(1, 5).Value = "Acción";
                worksheet.Cell(1, 6).Value = "Descripción de la acción";

                // Formato de encabezados (negrita, fondo gris suave)
                var headerRange = worksheet.Range(1, 1, 1, 6);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.FromArgb(229, 223, 240);
                headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                int row = 2;
                foreach (var rol in rolesConPermisos)
                {
                    string rolMostrar = rol.GroupName; // como pediste, usamos Description

                    if (rol.AccionesPermitidas.Any())
                    {
                        foreach (var accion in rol.AccionesPermitidas)
                        {
                            worksheet.Cell(row, 1).Value = rolMostrar;
                            worksheet.Cell(row, 2).Value = rol.Description; // o podrías poner rol.GroupName si querés
                            worksheet.Cell(row, 3).Value = rol.Email ?? "Sin email";
                            worksheet.Cell(row, 4).Value = accion.Categoria;
                            worksheet.Cell(row, 5).Value = accion.Nombre;
                            worksheet.Cell(row, 6).Value = accion.Descripcion;
                            row++;
                        }
                    }
                    else
                    {
                        worksheet.Cell(row, 1).Value = rolMostrar;
                        worksheet.Cell(row, 2).Value = rol.Description;
                        worksheet.Cell(row, 3).Value = rol.Email ?? "Sin email";
                        worksheet.Cell(row, 4).Value = "Sin permisos asignados";
                        worksheet.Cell(row, 5).Value = "";
                        worksheet.Cell(row, 6).Value = "";
                        row++;
                    }
                }

                // Autoajustar columnas
                worksheet.Columns().AdjustToContents();

                // Convertir a stream y luego a bytes
                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    var bytes = stream.ToArray();
                    return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Reporte_Permisos_Roles.xlsx");
                }
            }
        }
    }
}
