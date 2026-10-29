using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PasadorScript.DbModels;
using PasadorScript.Services.Email;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PasadorScript.Controllers
{
    public class TiposCambiosController : Controller
    {
        private readonly NuevoP229Context _context;
        private readonly IEmailService _email;

        public TiposCambiosController(NuevoP229Context context, IEmailService email)
        {
            _context = context;
            _email = email;
        }

        // GET: TiposCambios
        public async Task<IActionResult> Index()
        {
            return View(await _context.TiposCambios.ToListAsync());
        }

        // GET: TiposCambios/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tiposCambio = await _context.TiposCambios
                .FirstOrDefaultAsync(m => m.IdTipoCambio == id);
            if (tiposCambio == null)
            {
                return NotFound();
            }

            return View(tiposCambio);
        }

        // GET: TiposCambios/Create
        public IActionResult Create()
        {
            _email.SendPullRequestNotificationDBAAsync("DBA", "juan.gaffuri@bpn.com.ar", "34", "Vamolakd", "Bantotal", "https://localhost:80722/lakd");
            return View();
        }

        // POST: TiposCambios/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("IdTipoCambio,Descripcion")] TiposCambio tiposCambio)
        {
            if (ModelState.IsValid)
            {
                _context.Add(tiposCambio);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(tiposCambio);
        }

        // GET: TiposCambios/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tiposCambio = await _context.TiposCambios.FindAsync(id);
            if (tiposCambio == null)
            {
                return NotFound();
            }
            return View(tiposCambio);
        }

        // POST: TiposCambios/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("IdTipoCambio,Descripcion")] TiposCambio tiposCambio)
        {
            if (id != tiposCambio.IdTipoCambio)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(tiposCambio);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TiposCambioExists(tiposCambio.IdTipoCambio))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(tiposCambio);
        }

        // GET: TiposCambios/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tiposCambio = await _context.TiposCambios
                .FirstOrDefaultAsync(m => m.IdTipoCambio == id);
            if (tiposCambio == null)
            {
                return NotFound();
            }

            return View(tiposCambio);
        }

        // POST: TiposCambios/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var tiposCambio = await _context.TiposCambios.FindAsync(id);
            if (tiposCambio != null)
            {
                _context.TiposCambios.Remove(tiposCambio);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool TiposCambioExists(int id)
        {
            return _context.TiposCambios.Any(e => e.IdTipoCambio == id);
        }
    }
}
