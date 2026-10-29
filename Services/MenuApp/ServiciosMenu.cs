using Microsoft.EntityFrameworkCore;
using PasadorScript.DbModels;
using PasadorScript.DTO;
using PasadorScript.DTO.Sugus;
using System;
using System.Linq;

namespace PasadorScript.Services.MenuApp
{
    public class ServiciosMenu : IServiciosMenu
    {
        private readonly NuevoP229Context _context;
        public ServiciosMenu(NuevoP229Context context)
        {
            _context = context;
        }

        public async Task<ICollection<Menu>> GetMenu(Aplicativo aplicativo)
        {

            var res = new List<Menu>();
            var perfilesSugus = aplicativo.Perfiles.Select(x => x.Id).ToList();
            var permisosSugus = new List<Submenu>();
            var listadoDB = await _context.Menues.Where(x => x.IdPerfil.HasValue && perfilesSugus.Contains(x.IdPerfil.Value)).ToListAsync();
            foreach (var ldb in listadoDB)
            {
                var m = new Menu
                {
                    IdMenu = ldb.IdMenu,
                    Descripcion = ldb.Descripcion,
                    AspAction = ldb.AspAction!,
                    AspController = ldb.AspController!,
                    IClass = ldb.IClass!
                };
                var perf = aplicativo.Perfiles.FirstOrDefault(x => x.Id == ldb.IdPerfil);

                foreach (var sm in ldb.SubMenues)
                {
                    var sbmenu = new Submenu()
                    {
                        IdPerfilSugus = perf!.Id,
                        IdSubmenu = sm.IdSubmenu,
                        AspAction = sm.AspAction!,
                        AspController = sm.AspController!,
                        Descripcion = sm.Descripcion!
                    };
                    m.Submenues.Add(sbmenu);
                }
                res.Add(m);
            }

            return res;
        }

        public async Task<ICollection<Menu>> GetMenu()
        {
            var res = new List<Menu>();
            var listadoDB = await _context.Menues.ToListAsync();
            foreach (var ldb in listadoDB)
            {
                var m = new Menu
                {
                    IdMenu = ldb.IdMenu,
                    Descripcion = ldb.Descripcion,
                    AspAction = ldb.AspAction!,
                    AspController = ldb.AspController!,
                    IClass = ldb.IClass!
                };
                
                foreach (var sm in ldb.SubMenues)
                {
                    var sbmenu = new Submenu()
                    {
                        IdSubmenu = sm.IdSubmenu,
                        AspAction = sm.AspAction!,
                        AspController = sm.AspController!,
                        Descripcion = sm.Descripcion!
                    };
                    m.Submenues.Add(sbmenu);
                }
                res.Add(m);
            }

            return res;
        }
    }
}
