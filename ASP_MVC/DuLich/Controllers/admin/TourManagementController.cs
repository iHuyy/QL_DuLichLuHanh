using DuLich.Models;
using DuLich.Models.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QRCoder;

namespace DuLich.Controllers.admin
{
    [Authorize(Roles = "ROLE_ADMIN")]
    [Route("TourManagement/[action]")]
    [Route("admin/TourManagement/[action]")]
    public class TourManagementController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IWebHostEnvironment _env;

        public TourManagementController(ApplicationDbContext db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View(new Tour());
        }

        [HttpPost]
        public async Task<IActionResult> Create(Tour model, List<IFormFile>? images)
        {
            if (!ModelState.IsValid)
            {
                // expose modelstate errors to view for debugging
                TempData["ModelErrors"] = string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                return View(model);
            }

            // 1) Save tour to get MaTour
            _db.Tours.Add(model);
            await _db.SaveChangesAsync();

            // 2) Save images if provided
            if (images != null && images.Count > 0)
            {
                var imgFolder = Path.Combine(_env.WebRootPath, "images", "tour");
                Directory.CreateDirectory(imgFolder);
                foreach (var f in images)
                {
                    if (f.Length <= 0) continue;
                    var fileName = $"tour_{model.MaTour}_{Guid.NewGuid()}{Path.GetExtension(f.FileName)}";
                    var full = Path.Combine(imgFolder, fileName);
                    using (var fs = System.IO.File.Create(full))
                    {
                        await f.CopyToAsync(fs);
                    }
                    var rel = $"/images/tour/{fileName}";
                    _db.AnhTours.Add(new AnhTour { MaTour = model.MaTour, DuongDanAnh = rel, MoTa = f.FileName, NgayTaiLen = DateTime.UtcNow });
                }
                await _db.SaveChangesAsync();
            }

            // 3) Generate QR and save
            var qr = await GenerateQRCodeForTour(model.MaTour);
            model.QR = qr;
            await _db.SaveChangesAsync();

            TempData["Success"] = "Tạo tour thành công";
            return RedirectToAction("Detail", new { id = model.MaTour });
        }

        [HttpGet]
        public async Task<IActionResult> Detail(int id)
        {
            var t = await _db.Tours.FindAsync(id);
            if (t == null) return NotFound();

            // If QR empty, generate and save
            if (string.IsNullOrEmpty(t.QR))
            {
                t.QR = await GenerateQRCodeForTour(t.MaTour);
                await _db.SaveChangesAsync();
            }

            var images = await _db.AnhTours.Where(a => a.MaTour == id).ToListAsync();
            ViewBag.Images = images;
            return View(t);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var t = await _db.Tours.FindAsync(id);
            if (t == null) return NotFound();
            return View(t);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(Tour model, List<IFormFile>? images)
        {
            if (!ModelState.IsValid) return View(model);

            var t = await _db.Tours.FindAsync(model.MaTour);
            if (t == null) return NotFound();

            // update fields
            t.TieuDe = model.TieuDe;
            t.MoTa = model.MoTa;
            t.NoiKhoiHanh = model.NoiKhoiHanh;
            t.NoiDen = model.NoiDen;
            t.ThanhPho = model.ThanhPho;
            t.ThoiGian = model.ThoiGian;
            t.GiaNguoiLon = model.GiaNguoiLon;
            t.GiaTreEm = model.GiaTreEm;
            t.TrangThai = model.TrangThai;
            t.SoLuong = model.SoLuong;

            // save new images if any
            if (images != null && images.Count > 0)
            {
                var imgFolder = Path.Combine(_env.WebRootPath, "images", "tour");
                Directory.CreateDirectory(imgFolder);
                foreach (var f in images)
                {
                    if (f.Length <= 0) continue;
                    var fileName = $"tour_{t.MaTour}_{Guid.NewGuid()}{Path.GetExtension(f.FileName)}";
                    var full = Path.Combine(imgFolder, fileName);
                    using (var fs = System.IO.File.Create(full))
                    {
                        await f.CopyToAsync(fs);
                    }
                    var rel = $"/images/tour/{fileName}";
                    _db.AnhTours.Add(new AnhTour { MaTour = t.MaTour, DuongDanAnh = rel, MoTa = f.FileName, NgayTaiLen = DateTime.UtcNow });
                }
            }

            await _db.SaveChangesAsync();

            TempData["Success"] = "Cập nhật tour thành công";
            return RedirectToAction("Detail", new { id = t.MaTour });
        }

        [HttpPost]
        public async Task<IActionResult> GenerateQRCode(int id)
        {
            var t = await _db.Tours.FindAsync(id);
            if (t == null) return NotFound();

            t.QR = await GenerateQRCodeForTour(id);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Đã tạo lại QR code";
            return RedirectToAction("Detail", new { id });
        }

        [HttpPost]
        public async Task<IActionResult> ChangeStatus(int id, string status)
        {
            var t = await _db.Tours.FindAsync(id);
            if (t == null) return NotFound();
            t.TrangThai = status;
            await _db.SaveChangesAsync();
            TempData["Success"] = "Đã cập nhật trạng thái";
            return RedirectToAction("Tours", "Admin");
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var t = await _db.Tours.FindAsync(id);
            if (t == null) return NotFound();
            _db.Tours.Remove(t);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Xóa tour thành công";
            return RedirectToAction("Tours", "Admin");
        }

        private async Task<string> GenerateQRCodeForTour(int maTour)
        {
            // Build URL
            var url = Url.Action("Detail", "Tour", new { id = maTour }, Request.Scheme) ?? $"/Tour/Detail/{maTour}";

            // Create QR image using QRCoder
            using var qrGen = new QRCodeGenerator();
            var data = qrGen.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new PngByteQRCode(data);
            var bytes = qrCode.GetGraphic(20);

            // Save file
            var qrfolder = Path.Combine(_env.WebRootPath, "images", "qrcode");
            Directory.CreateDirectory(qrfolder);
            var fileName = $"qrcode_{maTour}_{DateTime.UtcNow.Ticks}.png";
            var full = Path.Combine(qrfolder, fileName);
            await System.IO.File.WriteAllBytesAsync(full, bytes);

            // Return relative path
            return $"/images/qrcode/{fileName}";
        }
    }
}
