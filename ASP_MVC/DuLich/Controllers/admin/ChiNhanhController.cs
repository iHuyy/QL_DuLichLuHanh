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
            // 1. Get all branches
            var chiNhanhs = await _context.ChiNhanhs
                                          .Include(c => c.NhanViens)
                                          .Include(c => c.Tours)
                                          .ToListAsync();

            // 2. Get all revenue grouped by branch
            var revenues = await _context.HoaDons
                .Where(hd => hd.DatTour != null && hd.DatTour.Tour != null && hd.DatTour.Tour.MaChiNhanh != null)
                .GroupBy(hd => hd.DatTour!.Tour!.MaChiNhanh)
                .Select(g => new
                {
                    MaChiNhanh = g.Key!.Value,
                    DoanhThu = g.Sum(hd => hd.SoTien ?? 0)
                })
                .ToDictionaryAsync(r => r.MaChiNhanh, r => r.DoanhThu);

            // 3. Create ViewModels
            var viewModels = chiNhanhs.Select(cn => new ChiNhanhViewModel
            {
                ChiNhanh = cn,
                DoanhThu = revenues.ContainsKey(cn.MaChiNhanh) ? revenues[cn.MaChiNhanh] : 0,
                SoNhanVien = cn.NhanViens.Count,
                SoTour = cn.Tours.Count
            }).ToList();

            return View("~/Views/admin/ChiNhanh/Index.cshtml", viewModels);
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
            if (chiNhanh != null)
            {
                _context.ChiNhanhs.Remove(chiNhanh);
                await _context.SaveChangesAsync();
            }
            return Redirect("/admin/ChiNhanh");
        }

        private bool ChiNhanhExists(int id)
        {
            return _context.ChiNhanhs.Any(e => e.MaChiNhanh == id);
        }
    }
}
