using DuLich.Models;
using DuLich.Models.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using System;
using System.Collections.Generic;
using System.IO;

namespace DuLich.Controllers.admin
{
    [Authorize(Roles = "ROLE_ADMIN")]
    [Route("TourManagement/[action]")]
    [Route("admin/TourManagement/[action]")]
    public class TourManagementController : Controller
    {
        private readonly ApplicationDbContext _db;

        public TourManagementController(ApplicationDbContext db)
        {
            _db = db;
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
            if (!model.ThoiGian.HasValue)
            {
                ModelState.AddModelError("ThoiGian", "Ngày khởi hành không được để trống.");
            }

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

            // save new images if any - lưu dữ liệu BLOB
            if (images != null && images.Count > 0)
            {
                foreach (var f in images)
                {
                    if (f.Length <= 0) continue;
                    
                    using (var ms = new MemoryStream())
                    {
                        await f.CopyToAsync(ms);
                        var imageData = ms.ToArray();
                        
                        _db.AnhTours.Add(new AnhTour 
                        { 
                            MaTour = t.MaTour, 
                            DuLieuAnh = imageData,
                            LoaiAnh = f.ContentType,
                            MoTa = f.FileName, 
                            NgayTaiLen = DateTime.UtcNow 
                        });
                    }
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

            // Delete associated images records
            var images = _db.AnhTours.Where(a => a.MaTour == id).ToList();
            if (images.Any())
            {
                _db.AnhTours.RemoveRange(images);
            }

            _db.Tours.Remove(t);

            try
            {
                await _db.SaveChangesAsync();

                // Delete QR code file after successful DB transaction
                TempData["Success"] = "Xóa tour thành công";
            }
            catch (DbUpdateException ex)
            {
                var innerException = ex.InnerException;
                if (innerException is Oracle.ManagedDataAccess.Client.OracleException oracleEx && oracleEx.Number == 2292)
                {
                    TempData["Error"] = "Không thể xóa tour. Có thể do đã có khách hàng đặt tour này hoặc có dữ liệu liên quan khác.";
                }
                else
                {
                    TempData["Error"] = "Đã xảy ra lỗi khi xóa tour: " + (innerException?.Message ?? ex.Message);
                }
            }

            return RedirectToAction("Tours", "Admin");
        }

        private async Task<string> GenerateQRCodeForTour(int maTour)
        {
            // Build URL
            var url = Url.Action("Detail", "TourManagement", new { id = maTour }, Request.Scheme) ?? $"/TourManagement/Detail/{maTour}";

            // Create QR image using QRCoder
            using var qrGen = new QRCodeGenerator();
            var data = qrGen.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new PngByteQRCode(data);
            var bytes = qrCode.GetGraphic(20);

            // Store base64 data URI in DB
            return $"data:image/png;base64,{Convert.ToBase64String(bytes)}";
        }
    }
}
