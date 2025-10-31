using DuLich.Models;
using DuLich.Models.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace DuLich.Controllers.admin
{
    public class ChiNhanhController : Controller
    {   
        private readonly ApplicationDbContext _context;

        public ChiNhanhController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: ChiNhanh
        [Route("admin/ChiNhanh")]
        public async Task<IActionResult> Index()
        {
            var chiNhanhs = await _context.ChiNhanhs
                .Include(c => c.NhanViens)
                .Include(c => c.Tours)
                .ToListAsync();
            return View("~/Views/admin/ChiNhanh/Index.cshtml", chiNhanhs);
        }

        // GET: ChiNhanh/Create
        [Route("admin/ChiNhanh/Create")]
        public IActionResult Create()
        {   
            return View("~/Views/admin/ChiNhanh/Create.cshtml");
        }

        // POST: ChiNhanh/Create
        [HttpPost]
        [Route("admin/ChiNhanh/Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("TenChiNhanh,DiaChi,SoDienThoai")] ChiNhanh chiNhanh)
        {
            if (ModelState.IsValid)
            {
                _context.Add(chiNhanh);
                await _context.SaveChangesAsync();
                return Redirect("/admin/ChiNhanh");
            }
            return View("~/Views/admin/ChiNhanh/Create.cshtml", chiNhanh);
        }

        // GET: ChiNhanh/Edit/5
        [Route("admin/ChiNhanh/Edit/{id}")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var chiNhanh = await _context.ChiNhanhs.FindAsync(id);
            if (chiNhanh == null)
            {
                return NotFound();
            }
            return View("~/Views/admin/ChiNhanh/Edit.cshtml", chiNhanh);
        }

        // POST: ChiNhanh/Edit/5
        [HttpPost]
        [Route("admin/ChiNhanh/Edit/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("MaChiNhanh,TenChiNhanh,DiaChi,SoDienThoai")] ChiNhanh chiNhanh)
        {
            if (id != chiNhanh.MaChiNhanh)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(chiNhanh);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ChiNhanhExists(chiNhanh.MaChiNhanh))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return Redirect("/admin/ChiNhanh");
            }
            return View("~/Views/admin/ChiNhanh/Edit.cshtml", chiNhanh);
        }

        // GET: ChiNhanh/Delete/5
        [Route("admin/ChiNhanh/Delete/{id}")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var chiNhanh = await _context.ChiNhanhs
                .FirstOrDefaultAsync(m => m.MaChiNhanh == id);
            if (chiNhanh == null)
            {
                return NotFound();
            }

            return View("~/Views/admin/ChiNhanh/Delete.cshtml", chiNhanh);
        }

        // POST: ChiNhanh/Delete/5
        [HttpPost, ActionName("Delete")]
        [Route("admin/ChiNhanh/Delete/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var chiNhanh = await _context.ChiNhanhs.FindAsync(id);
            _context.ChiNhanhs.Remove(chiNhanh);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool ChiNhanhExists(int id)
        {
            return _context.ChiNhanhs.Any(e => e.MaChiNhanh == id);
        }
    }
}
